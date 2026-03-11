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
        [SerializeField] private RectTransform _rouletteEffectsRoot;
        [SerializeField] private RouletteRewardSliceUI _rewardGhostPrefab;
        [Min(0f)] [SerializeField] private float _rewardGhostRiseDistance = 110f;
        [Min(0.05f)] [SerializeField] private float _rewardGhostDuration = 0.45f;
        [Min(1f)] [SerializeField] private float _rewardGhostPunchScale = 1.08f;
        [Min(0.05f)] [SerializeField] private float _rewardGhostPunchDuration = 0.16f;
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
        private Sequence _rewardGhostSequence;
        private RouletteRewardSliceUI _rewardGhostInstance;
        private CanvasGroup _rewardGhostCanvasGroup;
        private Vector2 _baseAnchoredPosition;
        private bool _hasBaseAnchoredPosition;

        public RectTransform ShakeTarget => _shakeTarget;

        private void OnEnable()
        {
            _shakeTarget ??= transform as RectTransform;
            _rouletteEffectsRoot ??= transform as RectTransform;
            CacheBaseAnchoredPosition();
            StopShake(resetPosition: true);
            StopRouletteRewardGhost();
        }

        private void OnDisable()
        {
            StopShake(resetPosition: true);
            StopRouletteRewardGhost();
        }

        private void OnValidate()
        {
            _shakeTarget ??= transform as RectTransform;
            _rouletteEffectsRoot ??= transform as RectTransform;

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

        public void PlayRouletteRewardGhost(RouletteRewardSliceUI sourceSliceView, RouletteResolvedSlice slice, Color rarityColor)
        {
            if (_rouletteEffectsRoot == null || _rewardGhostPrefab == null || sourceSliceView == null)
                return;

            RouletteRewardSliceUI ghost = GetOrCreateRewardGhost();
            if (ghost == null)
                return;

            RectTransform sourceRect = sourceSliceView.RootRect;
            RectTransform ghostRect = ghost.RootRect;
            if (sourceRect == null || ghostRect == null)
                return;

            StopRouletteRewardGhost();
            ghost.Bind(slice, rarityColor);
            ghost.gameObject.SetActive(true);
            ghostRect.SetAsLastSibling();
            ghostRect.localScale = Vector3.one;
            ghostRect.localRotation = Quaternion.identity;

            Vector2 sourceAnchoredPosition = ResolveAnchoredPositionInEffectsRoot(sourceRect);
            ghostRect.anchoredPosition = sourceAnchoredPosition;

            if (_rewardGhostCanvasGroup != null)
                _rewardGhostCanvasGroup.alpha = 1f;

            _rewardGhostSequence = DOTween.Sequence()
                .SetLink(ghost.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (ghost != null)
                        ghost.gameObject.SetActive(false);

                    _rewardGhostSequence = null;
                })
                .OnKill(() => _rewardGhostSequence = null);

            _rewardGhostSequence.Join(
                ghostRect.DOAnchorPosY(sourceAnchoredPosition.y + _rewardGhostRiseDistance, _rewardGhostDuration)
                    .SetEase(Ease.OutQuad));

            _rewardGhostSequence.Join(
                ghostRect.DOScale(_rewardGhostPunchScale, _rewardGhostPunchDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.OutQuad));

            if (_rewardGhostCanvasGroup != null)
            {
                _rewardGhostSequence.Join(
                    _rewardGhostCanvasGroup.DOFade(0f, _rewardGhostDuration)
                        .SetEase(Ease.OutQuad));
            }
        }

        public void StopShake(bool resetPosition = true)
        {
            if (_shakeTween != null && _shakeTween.IsActive())
                _shakeTween.Kill();

            _shakeTween = null;

            if (resetPosition)
                ResetShakeTarget();
        }

        public void StopRouletteRewardGhost()
        {
            if (_rewardGhostSequence != null && _rewardGhostSequence.IsActive())
                _rewardGhostSequence.Kill();

            _rewardGhostSequence = null;

            if (_rewardGhostInstance != null)
                _rewardGhostInstance.gameObject.SetActive(false);
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

        private RouletteRewardSliceUI GetOrCreateRewardGhost()
        {
            if (_rewardGhostInstance != null)
                return _rewardGhostInstance;

            _rewardGhostInstance = Instantiate(_rewardGhostPrefab, _rouletteEffectsRoot);
            _rewardGhostInstance.gameObject.SetActive(false);
            _rewardGhostCanvasGroup = _rewardGhostInstance.GetComponent<CanvasGroup>();
            if (_rewardGhostCanvasGroup == null)
                _rewardGhostCanvasGroup = _rewardGhostInstance.gameObject.AddComponent<CanvasGroup>();

            return _rewardGhostInstance;
        }

        private Vector2 ResolveAnchoredPositionInEffectsRoot(RectTransform sourceRect)
        {
            Vector3 worldPoint = sourceRect.TransformPoint(sourceRect.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rouletteEffectsRoot, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }
    }
}
