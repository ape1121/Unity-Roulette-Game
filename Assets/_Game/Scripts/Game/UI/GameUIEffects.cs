using System;
using DG.Tweening;
using UnityEngine;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameUIEffects : MonoBehaviour
    {
        [Serializable]
        private struct ShakeSettings
        {
            [Min(0f)] public float duration;
            public Vector2 strength;
            [Min(1)] public int vibrato;
            [Range(0f, 180f)] public float randomness;
            public bool snapping;
            public bool fadeOut;
        }

        [SerializeField] private RectTransform _shakeTarget;
        [SerializeField] private ShakeSettings _spinStartShake = new ShakeSettings
        {
            duration = 0.12f,
            strength = new Vector2(10f, 10f),
            vibrato = 18,
            randomness = 90f,
            snapping = false,
            fadeOut = true
        };
        [SerializeField] private ShakeSettings _bombShake = new ShakeSettings
        {
            duration = 0.25f,
            strength = new Vector2(24f, 24f),
            vibrato = 24,
            randomness = 90f,
            snapping = false,
            fadeOut = true
        };

        private Tween _shakeTween;
        private Vector2 _baseAnchoredPosition;
        private bool _hasBaseAnchoredPosition;

        public RectTransform ShakeTarget => _shakeTarget;

        private void OnEnable()
        {
            _shakeTarget ??= transform as RectTransform;
            CacheBaseAnchoredPosition();
            StopShake(resetPosition: true);
        }

        private void OnDisable()
        {
            StopShake(resetPosition: true);
        }

        private void OnValidate()
        {
            _shakeTarget ??= transform as RectTransform;

            if (!Application.isPlaying)
                CacheBaseAnchoredPosition();
        }

        public void PlaySpinStartShake()
        {
            PlayShake(_spinStartShake);
        }

        public void PlayBombShake()
        {
            PlayShake(_bombShake);
        }

        public void StopShake(bool resetPosition = true)
        {
            if (_shakeTween != null && _shakeTween.IsActive())
                _shakeTween.Kill();

            _shakeTween = null;

            if (resetPosition)
                ResetShakeTarget();
        }

        private void PlayShake(ShakeSettings settings)
        {
            if (_shakeTarget == null || settings.duration <= 0f)
                return;

            CacheBaseAnchoredPosition();
            StopShake(resetPosition: true);

            _shakeTween = _shakeTarget.DOShakeAnchorPos(
                    settings.duration,
                    settings.strength,
                    settings.vibrato,
                    settings.randomness,
                    settings.snapping,
                    settings.fadeOut)
                .SetLink(_shakeTarget.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(ResetShakeTarget)
                .OnKill(() => _shakeTween = null);
        }

        private void CacheBaseAnchoredPosition()
        {
            if (_shakeTarget == null)
                return;

            _baseAnchoredPosition = _shakeTarget.anchoredPosition;
            _hasBaseAnchoredPosition = true;
        }

        private void ResetShakeTarget()
        {
            if (_shakeTarget != null && _hasBaseAnchoredPosition)
                _shakeTarget.anchoredPosition = _baseAnchoredPosition;
        }

    }
}
