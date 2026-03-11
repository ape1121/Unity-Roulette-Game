using System.Collections;
using System.Collections.Generic;
using Ape.Core;
using UnityEngine;

namespace Ape.Sounds
{
    public class SoundManager : MonoBehaviour, IManager
    {
        public AllSounds allSounds => App.Config.SoundsConfig;

        [SerializeField]
        private int poolSize = 10;

        private Queue<AudioSource> audioSourcePool;
        private List<AudioSource> activeAudioSources;
        private AudioSource currentBGM;

        [SerializeField]
        private float uiVolume = 1f;

        private float masterVolume = 1f;
        private float sfxVolume = 1f;
        private float musicVolume = 1f;

        private Dictionary<string, Sound> soundDictionary;
        private Dictionary<string, float> soundCooldowns = new Dictionary<string, float>();
        private Dictionary<AudioSource, Sound> audioSourceSounds = new Dictionary<AudioSource, Sound>();
        private Dictionary<AudioSource, int> audioSourcePlaybackIds = new Dictionary<AudioSource, int>();
        private const float DEFAULT_COOLDOWN = 0.05f;
        private int nextPlaybackId = 1;

        private Dictionary<string, Coroutine> activeFadeOuts = new Dictionary<string, Coroutine>();
        private Dictionary<string, Coroutine> activeDelayedSounds = new Dictionary<string, Coroutine>();

    public void Initialize()
    {
        InitializeSoundSystem();
    }

    private void InitializeSoundSystem()
    {
        soundDictionary = new Dictionary<string, Sound>();
        audioSourcePool = new Queue<AudioSource>();
        activeAudioSources = new List<AudioSource>();

        GameObject audioSourceContainer = new GameObject("AudioSources");
        audioSourceContainer.transform.SetParent(transform);

        if (allSounds == null || allSounds.sounds == null || allSounds.sounds.Length == 0)
            return;

        foreach (Sound sound in allSounds.sounds)
            soundDictionary.TryAdd(sound.Name, sound);

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = audioSourceContainer.AddComponent<AudioSource>();
            ConfigureAudioSource(source);
            audioSourcePool.Enqueue(source);
        }
    }

    private void ConfigureAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f; // Will be overridden for 3D sounds
        source.priority = 128;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 50f;
    }


    public void PlaySound(string soundName, bool isUI = false, float pitchMultiplier = 1f)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound))
            return;

        PlaySound(sound, isUI, pitchMultiplier);
    }

    public void PlaySound(Sound sound, bool isUI = false, float pitchMultiplier = 1f)
    {
        if (sound == null || sound.Clip == null)
            return;

        string soundKey = ResolveSoundKey(sound);
        if (soundCooldowns.TryGetValue(soundKey, out float lastPlayTime) && Time.time - lastPlayTime < DEFAULT_COOLDOWN)
            return;

        AudioSource audioSource = GetAudioSource();
        if (audioSource == null)
            return;

        soundCooldowns[soundKey] = Time.time;
        int playbackId = StartPlayback(
            audioSource,
            sound,
            sound.Volume * (isUI ? uiVolume : sfxVolume) * masterVolume,
            sound.Pitch * pitchMultiplier,
            sound.Loop,
            spatialBlend: 0f);

        if (!sound.Loop)
            StartCoroutine(ReleaseWhenFinished(audioSource, playbackId, ResolvePlaybackDuration(sound.Clip, sound.Pitch * pitchMultiplier)));
    }

    public void TryPlaySound(string soundName, bool isUI = false)
    {
        if (activeFadeOuts.TryGetValue(soundName, out Coroutine fadeCoroutine))
        {
            StopCoroutine(fadeCoroutine);
            activeFadeOuts.Remove(soundName);

            if (soundDictionary.TryGetValue(soundName, out Sound sound))
            {
                foreach (AudioSource source in activeAudioSources)
                {
                    if (audioSourceSounds.TryGetValue(source, out Sound activeSound) && activeSound == sound)
                    {
                        source.volume = sound.Volume * (isUI ? uiVolume : sfxVolume) * masterVolume;
                    }
                }
            }
            return;
        }

        if (IsPlaying(soundName)) return;
        PlaySound(soundName, isUI);
    }

    private bool IsPlaying(string soundName)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound)) return false;

        foreach (AudioSource source in activeAudioSources)
        {
            if (source.isPlaying
                && audioSourceSounds.TryGetValue(source, out Sound activeSound)
                && activeSound == sound)
            {
                return true;
            }
        }
        return false;
    }

    public void PlayBGM(string soundName)
    {
        if (currentBGM != null && currentBGM.isPlaying)
        {
            ReleaseAudioSource(currentBGM);
            currentBGM = null;
        }
        if (!soundDictionary.TryGetValue(soundName, out Sound soundData)) return;

        // Play BGM with music volume
        if (soundCooldowns.TryGetValue(soundName, out float lastPlayTime))
        {
            if (Time.time - lastPlayTime < DEFAULT_COOLDOWN)
            {
                return;
            }
        }

        AudioSource audioSource = GetAudioSource();
        if (audioSource == null) return;

        soundCooldowns[soundName] = Time.time;
        int playbackId = StartPlayback(
            audioSource,
            soundData,
            soundData.Volume * musicVolume * masterVolume,
            soundData.Pitch,
            soundData.Loop,
            spatialBlend: 0f);

        currentBGM = audioSource;

        if (!soundData.Loop)
        {
            StartCoroutine(ReleaseWhenFinished(audioSource, playbackId, ResolvePlaybackDuration(soundData.Clip, soundData.Pitch)));
        }
    }

    public void PlayRandomBGM()
    {
        string randomBGM = UnityEngine.Random.value < 0.5f ? "bgm1" : "bgm2";
        PlayBGM(randomBGM);
    }

    public void StopSound(string soundName)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound)) return;

        StopSound(sound);
    }

    public void StopSound(Sound sound)
    {
        if (sound == null || sound.Clip == null) return;

        for (int i = activeAudioSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = activeAudioSources[i];
            if (audioSourceSounds.TryGetValue(source, out Sound activeSound) && activeSound == sound)
            {
                ReleaseAudioSource(source);
                if (source == currentBGM)
                {
                    currentBGM = null;
                }
            }
        }
    }

    public void FadeOutSound(string soundName, float duration = 0.5f)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound)) return;

        if (activeFadeOuts.TryGetValue(soundName, out Coroutine existingFade))
        {
            StopCoroutine(existingFade);
            activeFadeOuts.Remove(soundName);
        }

        foreach (AudioSource source in activeAudioSources)
        {
            if (source.isPlaying
                && audioSourceSounds.TryGetValue(source, out Sound activeSound)
                && activeSound == sound)
            {
                if (!audioSourcePlaybackIds.TryGetValue(source, out int playbackId))
                    continue;

                Coroutine fadeCoroutine = StartCoroutine(FadeOut(source, duration, soundName, playbackId));
                activeFadeOuts[soundName] = fadeCoroutine;
            }
        }
    }

    private IEnumerator FadeOut(AudioSource audioSource, float duration, string soundName, int playbackId)
    {
        float startVolume = audioSource.volume;
        float timer = 0;

        while (timer < duration)
        {
            if (audioSource == null)
            {
                activeFadeOuts.Remove(soundName);
                yield break;
            }

            if (!audioSourcePlaybackIds.TryGetValue(audioSource, out int activePlaybackId) || activePlaybackId != playbackId)
            {
                activeFadeOuts.Remove(soundName);
                yield break;
            }

            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0, timer / duration);
            yield return null;
        }

        activeFadeOuts.Remove(soundName);

        if (!audioSourcePlaybackIds.TryGetValue(audioSource, out int completedPlaybackId) || completedPlaybackId != playbackId)
            yield break;

        ReleaseAudioSource(audioSource);
    }

    public void PlaySoundDelayed(string soundName, int repeatCount = 1, float delayTime = 1f, float volumeMultiplier = 1f, bool isUI = false)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound)) return;

        if (activeDelayedSounds.TryGetValue(soundName, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);
            activeDelayedSounds.Remove(soundName);
        }

        Coroutine delayedCoroutine = StartCoroutine(PlaySoundWithDelay(sound, repeatCount, delayTime, volumeMultiplier, isUI));
        activeDelayedSounds[soundName] = delayedCoroutine;
    }

    private IEnumerator PlaySoundWithDelay(Sound sound, int repeatCount, float delayTime, float volumeMultiplier, bool isUI)
    {
        for (int i = 0; i < repeatCount; i++)
        {
            AudioSource audioSource = GetAudioSource();
            if (audioSource == null) yield break;

            int playbackId = StartPlayback(
                audioSource,
                sound,
                sound.Volume * (isUI ? uiVolume : sfxVolume) * masterVolume * volumeMultiplier,
                sound.Pitch,
                loop: false,
                spatialBlend: 0f);

            StartCoroutine(ReleaseWhenFinished(audioSource, playbackId, ResolvePlaybackDuration(sound.Clip, sound.Pitch)));

            if (i < repeatCount - 1)
                yield return new WaitForSeconds(delayTime);
        }

        activeDelayedSounds.Remove(ResolveSoundKey(sound));
    }

    public void StopDelayedSound(string soundName)
    {
        if (activeDelayedSounds.TryGetValue(soundName, out Coroutine delayedCoroutine))
        {
            StopCoroutine(delayedCoroutine);
            activeDelayedSounds.Remove(soundName);
        }
    }

    public void PlaySoundAtPosition(string soundName, Vector3 position, float pitchMultiplier = 1f)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound sound)) return;

        if (soundCooldowns.TryGetValue(soundName, out float lastPlayTime))
        {
            if (Time.time - lastPlayTime < DEFAULT_COOLDOWN)
            {
                return;
            }
        }

        AudioSource audioSource = GetAudioSource();
        if (audioSource == null) return;

        soundCooldowns[soundName] = Time.time;
        int playbackId = StartPlayback(
            audioSource,
            sound,
            sound.Volume * sfxVolume * masterVolume,
            sound.Pitch * pitchMultiplier,
            sound.Loop,
            spatialBlend: 1f);
        audioSource.transform.position = position;

        if (!sound.Loop)
        {
            StartCoroutine(ReleaseWhenFinished(audioSource, playbackId, ResolvePlaybackDuration(sound.Clip, sound.Pitch * pitchMultiplier)));
        }
    }

    private AudioSource GetAudioSource()
    {
        if (audioSourcePool.Count == 0)
        {
            for (int i = activeAudioSources.Count - 1; i >= 0; i--)
            {
                if (!activeAudioSources[i].isPlaying)
                {
                    AudioSource reclaimedSource = activeAudioSources[i];
                    activeAudioSources.RemoveAt(i);
                    audioSourceSounds.Remove(reclaimedSource);
                    audioSourcePlaybackIds.Remove(reclaimedSource);
                    return reclaimedSource;
                }
            }
            return null;
        }

        AudioSource availableSource = audioSourcePool.Dequeue();
        activeAudioSources.Add(availableSource);
        return availableSource;
    }

    private void ReleaseAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.loop = false;
        source.spatialBlend = 0f;
        audioSourceSounds.Remove(source);
        audioSourcePlaybackIds.Remove(source);
        activeAudioSources.Remove(source);
        audioSourcePool.Enqueue(source);
    }

    private IEnumerator ReleaseWhenFinished(AudioSource audioSource, int playbackId, float delay)
    {
        if (audioSource == null) yield break;

        yield return new WaitForSeconds(delay + 0.1f);

        if (audioSource == null)
            yield break;

        if (!audioSourcePlaybackIds.TryGetValue(audioSource, out int activePlaybackId) || activePlaybackId != playbackId)
            yield break;

        ReleaseAudioSource(audioSource);
    }

    private static string ResolveSoundKey(Sound sound)
    {
        if (sound == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(sound.Name)
            ? sound.Name
            : sound.GetInstanceID().ToString();
    }

    private int StartPlayback(
        AudioSource audioSource,
        Sound sound,
        float volume,
        float pitch,
        bool loop,
        float spatialBlend)
    {
        int playbackId = nextPlaybackId++;
        audioSourcePlaybackIds[audioSource] = playbackId;
        audioSourceSounds[audioSource] = sound;
        audioSource.clip = sound.Clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        audioSource.spatialBlend = spatialBlend;
        audioSource.Play();
        return playbackId;
    }

    private static float ResolvePlaybackDuration(AudioClip clip, float pitch)
    {
        if (clip == null)
            return 0f;

        float absolutePitch = Mathf.Abs(pitch);
        if (absolutePitch < 0.01f)
            absolutePitch = 1f;

        return clip.length / absolutePitch;
    }
    }
}
