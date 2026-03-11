using System;
using System.Collections.Generic;
using Ape.Game.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    public sealed class InventoryRewardSectionController
    {
        private Transform _sectionRoot;
        private Transform _explicitCardsContentRoot;
        private Transform _resolvedCardsContentRoot;
        private RewardCardUI _rewardCardPrefab;
        private readonly List<RewardCardUI> _cards = new List<RewardCardUI>();
        private readonly List<string> _activeRewardIds = new List<string>();
        private Tween _scrollTween;

        public Transform SectionRoot => _sectionRoot;
        public Transform CardsContentRoot => _resolvedCardsContentRoot;

        public void Configure(Transform sectionRoot, Transform explicitCardsContentRoot, RewardCardUI rewardCardPrefab)
        {
            _sectionRoot = sectionRoot;
            _explicitCardsContentRoot = explicitCardsContentRoot;
            _rewardCardPrefab = rewardCardPrefab;
            ResolveCardsContentRoot();
        }

        public bool Sync(
            IReadOnlyList<InventoryRewardEntry> rewards,
            Func<InventoryRewardEntry, Color> rarityColorResolver,
            Action<RewardCardUI, InventoryRewardEntry> actionBinder)
        {
            KillScrollTween();
            ResolveCardsContentRoot();

            if (_resolvedCardsContentRoot == null || _rewardCardPrefab == null)
                return false;

            int rewardCount = rewards != null ? rewards.Count : 0;
            _activeRewardIds.Clear();

            for (int i = 0; i < rewardCount; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                InventoryRewardEntry rewardEntry = rewards[i];
                _activeRewardIds.Add(rewardEntry.RewardId);
                card.Bind(rewardEntry.Reward, rarityColorResolver != null ? rarityColorResolver(rewardEntry) : Color.white);
                actionBinder?.Invoke(card, rewardEntry);
                card.gameObject.SetActive(true);
            }

            for (int i = rewardCount; i < _cards.Count; i++)
            {
                if (_cards[i] == null)
                    continue;

                _cards[i].ClearAction();
                _cards[i].gameObject.SetActive(false);
            }

            return true;
        }

        public void SetVisible(bool isVisible)
        {
            if (!isVisible)
                KillScrollTween();

            if (_sectionRoot != null)
                _sectionRoot.gameObject.SetActive(isVisible);
        }

        public void SetVisualState(bool isVisible)
        {
            if (_sectionRoot == null)
                return;

            if (!isVisible)
                KillScrollTween();

            CanvasGroup canvasGroup = _sectionRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _sectionRoot.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        public void ForceLayout()
        {
            if (_sectionRoot == null)
                return;

            Canvas.ForceUpdateCanvases();

            DynamicScrollableGrid dynamicGrid = _sectionRoot.GetComponent<DynamicScrollableGrid>();
            if (dynamicGrid == null)
                dynamicGrid = _sectionRoot.GetComponentInChildren<DynamicScrollableGrid>(true);

            if (dynamicGrid != null)
                dynamicGrid.RefreshLayout();

            ScrollRect scrollRect = _sectionRoot.GetComponentInChildren<ScrollRect>(true);
            RectTransform scrollContentRect = scrollRect != null ? scrollRect.content : null;
            RectTransform cardsRect = _resolvedCardsContentRoot as RectTransform;
            RectTransform sectionRect = _sectionRoot as RectTransform;

            if (scrollContentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);

            if (cardsRect != null && cardsRect != scrollContentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRect);

            if (sectionRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);

            Canvas.ForceUpdateCanvases();
        }

        public bool ScrollToReward(string rewardId, bool animate = true, float duration = 0.28f)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            int cardIndex = FindCardIndexByRewardId(rewardId);
            if (cardIndex < 0 || cardIndex >= _cards.Count || _cards[cardIndex] == null)
                return false;

            RewardCardUI targetCard = _cards[cardIndex];

            ScrollRect scrollRect = ResolveScrollRect();
            RectTransform contentRect = scrollRect != null ? scrollRect.content : null;
            RectTransform viewportRect = scrollRect != null
                ? (scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform)
                : null;
            RectTransform targetRect = targetCard.transform as RectTransform;

            if (scrollRect == null || contentRect == null || viewportRect == null || targetRect == null)
                return false;

            ForceLayout();
            scrollRect.StopMovement();
            KillScrollTween();

            Sequence scrollSequence = null;

            if (scrollRect.horizontal)
            {
                float horizontalTarget = ResolveHorizontalNormalizedPosition(contentRect, viewportRect, targetRect);
                if (animate)
                {
                    scrollSequence ??= DOTween.Sequence().OnKill(() => _scrollTween = null);
                    scrollSequence.Join(
                        DOTween.To(
                                () => scrollRect.horizontalNormalizedPosition,
                                value => scrollRect.horizontalNormalizedPosition = value,
                                horizontalTarget,
                                duration)
                            .SetEase(Ease.OutCubic));
                }
                else
                {
                    scrollRect.horizontalNormalizedPosition = horizontalTarget;
                }
            }

            if (scrollRect.vertical)
            {
                float verticalTarget = ResolveVerticalNormalizedPosition(contentRect, viewportRect, targetRect);
                if (animate)
                {
                    scrollSequence ??= DOTween.Sequence().OnKill(() => _scrollTween = null);
                    scrollSequence.Join(
                        DOTween.To(
                                () => scrollRect.verticalNormalizedPosition,
                                value => scrollRect.verticalNormalizedPosition = value,
                                verticalTarget,
                                duration)
                            .SetEase(Ease.OutCubic));
                }
                else
                {
                    scrollRect.verticalNormalizedPosition = verticalTarget;
                }
            }

            if (scrollSequence != null)
            {
                scrollSequence.OnComplete(() =>
                {
                    _scrollTween = null;
                    targetCard.PlayHighlightPulse();
                });
            }
            else
            {
                targetCard.PlayHighlightPulse();
            }

            _scrollTween = scrollSequence;
            return true;
        }

        public void KillScrollTween()
        {
            if (_scrollTween != null && _scrollTween.IsActive())
                _scrollTween.Kill();

            _scrollTween = null;
        }

        private RewardCardUI GetOrCreateCard(int index)
        {
            while (_cards.Count <= index)
            {
                RewardCardUI card = UnityEngine.Object.Instantiate(_rewardCardPrefab, _resolvedCardsContentRoot);
                card.gameObject.SetActive(false);
                _cards.Add(card);
            }

            return _cards[index];
        }

        private void ResolveCardsContentRoot()
        {
            if (_explicitCardsContentRoot != null)
            {
                _resolvedCardsContentRoot = _explicitCardsContentRoot;
                return;
            }

            if (_resolvedCardsContentRoot != null)
                return;

            if (_sectionRoot == null)
            {
                _resolvedCardsContentRoot = null;
                return;
            }

            ScrollRect scrollRect = _sectionRoot.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null && scrollRect.content != null)
            {
                _resolvedCardsContentRoot = scrollRect.content;
                return;
            }

            _resolvedCardsContentRoot = _sectionRoot;
        }

        private int FindCardIndexByRewardId(string rewardId)
        {
            for (int i = 0; i < _activeRewardIds.Count; i++)
            {
                if (string.Equals(_activeRewardIds[i], rewardId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private ScrollRect ResolveScrollRect()
        {
            return _sectionRoot != null ? _sectionRoot.GetComponentInChildren<ScrollRect>(true) : null;
        }

        private static float ResolveHorizontalNormalizedPosition(
            RectTransform contentRect,
            RectTransform viewportRect,
            RectTransform targetRect)
        {
            float hiddenWidth = contentRect.rect.width - viewportRect.rect.width;
            if (hiddenWidth <= 0.01f)
                return 0f;

            Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(contentRect, targetRect);
            float contentLeft = -contentRect.rect.width * contentRect.pivot.x;
            float contentRight = contentRect.rect.width * (1f - contentRect.pivot.x);
            float minCenter = contentLeft + (viewportRect.rect.width * 0.5f);
            float maxCenter = contentRight - (viewportRect.rect.width * 0.5f);

            if (maxCenter <= minCenter + 0.01f)
                return 0f;

            float desiredCenter = Mathf.Clamp(targetBounds.center.x, minCenter, maxCenter);
            return Mathf.InverseLerp(minCenter, maxCenter, desiredCenter);
        }

        private static float ResolveVerticalNormalizedPosition(
            RectTransform contentRect,
            RectTransform viewportRect,
            RectTransform targetRect)
        {
            float hiddenHeight = contentRect.rect.height - viewportRect.rect.height;
            if (hiddenHeight <= 0.01f)
                return 1f;

            Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(contentRect, targetRect);
            float contentBottom = -contentRect.rect.height * contentRect.pivot.y;
            float contentTop = contentRect.rect.height * (1f - contentRect.pivot.y);
            float minCenter = contentBottom + (viewportRect.rect.height * 0.5f);
            float maxCenter = contentTop - (viewportRect.rect.height * 0.5f);

            if (maxCenter <= minCenter + 0.01f)
                return 1f;

            float desiredCenter = Mathf.Clamp(targetBounds.center.y, minCenter, maxCenter);
            return Mathf.InverseLerp(minCenter, maxCenter, desiredCenter);
        }
    }
}
