using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class UISpriteSequencePlayer : MonoBehaviour
    {
        [SerializeField] private Image _targetImage;
        [SerializeField] private Sprite[] _frames;
        [Min(1f)] [SerializeField] private float _framesPerSecond = 18f;
        [SerializeField] private bool _hideWhenStopped = true;

        private Coroutine _playRoutine;

        private void OnDisable()
        {
            Stop();
        }

        private void OnValidate()
        {
            _targetImage ??= GetComponent<Image>();
        }

        public void Play()
        {
            if (_targetImage == null || _frames == null || _frames.Length == 0)
                return;

            Stop();
            _playRoutine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            _playRoutine = null;

            if (_targetImage == null)
                return;

            if (_hideWhenStopped)
                _targetImage.enabled = false;
        }

        private IEnumerator PlayRoutine()
        {
            _targetImage.enabled = true;

            float frameDuration = 1f / Mathf.Max(1f, _framesPerSecond);
            for (int i = 0; i < _frames.Length; i++)
            {
                _targetImage.sprite = _frames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            _playRoutine = null;

            if (_hideWhenStopped)
                _targetImage.enabled = false;
        }
    }
}
