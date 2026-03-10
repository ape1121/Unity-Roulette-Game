using UnityEngine;

namespace Ape.Sounds
{
    [CreateAssetMenu(fileName = "New Sound", menuName = "CriticalShot/Sounds/Sound")]
    public class Sound : ScriptableObject
    {
        public string Name;
        public AudioClip Clip;
        [Range(0f, 1f)]
        public float Volume = 1f;
        [Range(0.1f, 3f)]
        public float Pitch = 1f;
        public bool Loop = false;
    }
}
