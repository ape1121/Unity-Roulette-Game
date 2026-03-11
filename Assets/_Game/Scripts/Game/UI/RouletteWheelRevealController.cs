using System;
using Ape.Core;
using Ape.Data;
using DG.Tweening;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RouletteWheelRevealController
    {
        private const string SmokeSoundName = "poof";

        private RouletteWheelLayoutController _layoutController;
        private RectTransform _ghostEffectsRoot;
        private RouletteRewardSliceUI _rewardGhostPrefab;
        private float _replaceSmokeChainInterval = 0.08f;
        private float _replaceSwapDelay = 0.1f;
        private float _replaceChainStartAngle = 90f;
        private float _rewardGhostRiseDistance = 110f;
        private float _rewardGhostDuration = 0.45f;
        private float _rewardGhostFadeDuration = 0.65f;
        private float _rewardGhostEndScale = 1.3f;
        private Ease _rewardGhostMoveEase = Ease.OutQuad;
        private Ease _rewardGhostScaleEase = Ease.OutCubic;
        private Ease _rewardGhostFadeEase = Ease.InQuad;

        private Sequence _postSpinRevealSequence;
        private Sequence _rewardGhostSequence;
        private RouletteRewardSliceUI _rewardGhostInstance;
        private CanvasGroup _rewardGhostCanvasGroup;

        public bool IsRevealPending => _postSpinRevealSequence != null && _postSpinRevealSequence.IsActive();

        public void Configure(
            RouletteWheelLayoutController layoutController,
            RectTransform ghostEffectsRoot,
            RouletteRewardSliceUI rewardGhostPrefab,
            float replaceSmokeChainInterval,
            float replaceSwapDelay,
            float replaceChainStartAngle,
            float rewardGhostRiseDistance,
            float rewardGhostDuration,
            float rewardGhostFadeDuration,
            float rewardGhostEndScale,
            Ease rewardGhostMoveEase,
            Ease rewardGhostScaleEase,
            Ease rewardGhostFadeEase)
        {
            _layoutController = layoutController;
            _ghostEffectsRoot = ghostEffectsRoot;
            _rewardGhostPrefab = rewardGhostPrefab;
            _replaceSmokeChainInterval = replaceSmokeChainInterval;
            _replaceSwapDelay = replaceSwapDelay;
            _replaceChainStartAngle = replaceChainStartAngle;
            _rewardGhostRiseDistance = rewardGhostRiseDistance;
            _rewardGhostDuration = rewardGhostDuration;
            _rewardGhostFadeDuration = rewardGhostFadeDuration;
            _rewardGhostEndScale = rewardGhostEndScale;
            _rewardGhostMoveEase = rewardGhostMoveEase;
            _rewardGhostScaleEase = rewardGhostScaleEase;
            _rewardGhostFadeEase = rewardGhostFadeEase;
        }

        public void StopAnimation()
        {
            if (_postSpinRevealSequence != null && _postSpinRevealSequence.IsActive())
                _postSpinRevealSequence.Kill();

            _postSpinRevealSequence = null;
            StopRewardGhost();
        }

        public void PlayPostSpinReveal(
            RouletteResolvedWheel nextWheel,
            int winningSliceIndex,
            RouletteResolvedSlice winningSlice,
            float revealDelay,
            float currentRotationDegrees,
            Func<RouletteResolvedSlice, Color> rarityColorResolver,
            Action<RouletteResolvedWheel> onWheelApplied,
            Action onComplete = null)
        {
            if (nextWheel == null)
                return;

            if (_postSpinRevealSequence != null && _postSpinRevealSequence.IsActive())
                _postSpinRevealSequence.Kill();

            StopRewardGhost();

            if (revealDelay <= 0f || _layoutController == null || _layoutController.SliceCount == 0)
            {
                _layoutController?.BuildWheel(nextWheel, rarityColorResolver);
                onWheelApplied?.Invoke(nextWheel);
                onComplete?.Invoke();
                return;
            }

            int clampedWinningSliceIndex = Mathf.Clamp(winningSliceIndex, 0, _layoutController.SliceCount - 1);
            PlayWinningSliceGhost(clampedWinningSliceIndex, winningSlice, rarityColorResolver);

            _postSpinRevealSequence = DOTween.Sequence()
                .OnComplete(() =>
                {
                    _layoutController.FinalizePostSpinReveal(nextWheel, rarityColorResolver);
                    onWheelApplied?.Invoke(nextWheel);
                    _postSpinRevealSequence = null;
                    onComplete?.Invoke();
                })
                .OnKill(() => _postSpinRevealSequence = null);

            _postSpinRevealSequence.AppendInterval(revealDelay);
            AppendSliceReplaceChain(_postSpinRevealSequence, nextWheel, currentRotationDegrees, rarityColorResolver);
        }

        private void PlayWinningSliceGhost(
            int sliceIndex,
            RouletteResolvedSlice slice,
            Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            if (_layoutController == null)
                return;

            RouletteRewardSliceUI sourceSliceView = _layoutController.GetSliceView(sliceIndex);
            if (sourceSliceView == null)
                return;

            Color rarityColor = rarityColorResolver != null ? rarityColorResolver(slice) : Color.white;
            PlayRewardGhost(sourceSliceView, slice, rarityColor);
        }

        private void AppendSliceReplaceChain(
            Sequence sequence,
            RouletteResolvedWheel nextWheel,
            float currentRotationDegrees,
            Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            if (sequence == null || _layoutController == null || nextWheel == null || nextWheel.Slices == null || nextWheel.Slices.Count == 0)
                return;

            float chainInterval = Mathf.Max(0f, _replaceSmokeChainInterval);
            float swapDelay = Mathf.Max(0f, _replaceSwapDelay);
            int swapCount = Mathf.Min(_layoutController.SliceCount, nextWheel.Slices.Count);
            if (swapCount <= 0)
                return;

            float baseTime = sequence.Duration(false);
            float totalDuration = ((swapCount - 1) * chainInterval) + swapDelay;
            if (totalDuration > 0f)
                sequence.AppendInterval(totalDuration);

            int startIndex = ResolveReplaceChainStartIndex(swapCount, currentRotationDegrees);
            for (int orderIndex = 0; orderIndex < swapCount; orderIndex++)
            {
                int capturedIndex = (startIndex + orderIndex) % swapCount;
                RouletteRewardSliceUI sliceView = _layoutController.GetSliceView(capturedIndex);
                if (sliceView == null)
                    continue;

                RouletteRewardSliceUI capturedSliceView = sliceView;
                RouletteResolvedSlice capturedSlice = nextWheel.Slices[capturedIndex];
                float triggerTime = baseTime + (orderIndex * chainInterval);
                float bindTime = triggerTime + swapDelay;

                sequence.InsertCallback(triggerTime, () => TriggerSliceReplaceSmoke(capturedSliceView));
                sequence.InsertCallback(bindTime, () =>
                    _layoutController.BindSliceView(
                        capturedSliceView,
                        capturedIndex,
                        capturedSlice,
                        nextWheel.Slices.Count,
                        rarityColorResolver));
            }
        }

        private int ResolveReplaceChainStartIndex(int sliceCount, float currentRotationDegrees)
        {
            if (sliceCount <= 1)
                return 0;

            float sliceAngle = 360f / sliceCount;
            int closestIndex = 0;
            float closestDelta = float.MaxValue;

            for (int i = 0; i < sliceCount; i++)
            {
                float visibleAngle = Mathf.Repeat((90f - (i * sliceAngle)) + currentRotationDegrees, 360f);
                float delta = Mathf.Abs(Mathf.DeltaAngle(visibleAngle, _replaceChainStartAngle));
                if (delta < closestDelta)
                {
                    closestDelta = delta;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void TriggerSliceReplaceSmoke(RouletteRewardSliceUI sliceView)
        {
            if (sliceView == null || sliceView.ReplaceSmokeDuration <= 0f)
                return;

            sliceView.PlayReplaceSmoke();
            PlayUISound(SmokeSoundName);
        }

        private void PlayRewardGhost(RouletteRewardSliceUI sourceSliceView, RouletteResolvedSlice slice, Color rarityColor)
        {
            if (_ghostEffectsRoot == null || _rewardGhostPrefab == null || sourceSliceView == null)
                return;

            RouletteRewardSliceUI ghost = GetOrCreateRewardGhost();
            if (ghost == null)
                return;

            RectTransform sourceRect = sourceSliceView.RootRect;
            RectTransform ghostRect = ghost.RootRect;
            if (sourceRect == null || ghostRect == null)
                return;

            StopRewardGhost();
            ghost.Bind(slice, rarityColor);
            ghost.gameObject.SetActive(true);
            ghostRect.SetAsLastSibling();
            ghostRect.localRotation = Quaternion.identity;

            Vector2 sourceAnchoredPosition = ResolveAnchoredPositionInGhostRoot(sourceRect);
            ghostRect.anchoredPosition = sourceAnchoredPosition;
            ghostRect.localScale = Vector3.one;

            if (_rewardGhostCanvasGroup != null)
                _rewardGhostCanvasGroup.alpha = 1f;

            _rewardGhostSequence = DOTween.Sequence()
                .SetLink(ghost.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    ghost.gameObject.SetActive(false);
                    _rewardGhostSequence = null;
                })
                .OnKill(() => _rewardGhostSequence = null);

            _rewardGhostSequence.Join(
                ghostRect.DOAnchorPosY(sourceAnchoredPosition.y + _rewardGhostRiseDistance, _rewardGhostDuration)
                    .SetEase(_rewardGhostMoveEase));

            _rewardGhostSequence.Join(
                ghostRect.DOScale(_rewardGhostEndScale, _rewardGhostDuration)
                    .SetEase(_rewardGhostScaleEase));

            if (_rewardGhostCanvasGroup != null)
            {
                _rewardGhostSequence.Join(
                    _rewardGhostCanvasGroup.DOFade(0f, _rewardGhostFadeDuration)
                        .SetEase(_rewardGhostFadeEase));
            }
        }

        private void StopRewardGhost()
        {
            if (_rewardGhostSequence != null && _rewardGhostSequence.IsActive())
                _rewardGhostSequence.Kill();

            _rewardGhostSequence = null;

            if (_rewardGhostInstance != null)
                _rewardGhostInstance.gameObject.SetActive(false);
        }

        private RouletteRewardSliceUI GetOrCreateRewardGhost()
        {
            if (_rewardGhostInstance != null)
                return _rewardGhostInstance;

            _rewardGhostInstance = UnityEngine.Object.Instantiate(_rewardGhostPrefab, _ghostEffectsRoot);
            _rewardGhostInstance.gameObject.SetActive(false);
            _rewardGhostCanvasGroup = _rewardGhostInstance.GetComponent<CanvasGroup>();
            if (_rewardGhostCanvasGroup == null)
                _rewardGhostCanvasGroup = _rewardGhostInstance.gameObject.AddComponent<CanvasGroup>();

            return _rewardGhostInstance;
        }

        private Vector2 ResolveAnchoredPositionInGhostRoot(RectTransform sourceRect)
        {
            Vector3 worldPoint = sourceRect.TransformPoint(sourceRect.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostEffectsRoot, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        private static void PlayUISound(string soundName, float pitchMultiplier = 1f)
        {
            if (App.Sound == null)
                return;

            App.Sound.PlaySound(soundName, isUI: true, pitchMultiplier: pitchMultiplier);
        }
    }
}
