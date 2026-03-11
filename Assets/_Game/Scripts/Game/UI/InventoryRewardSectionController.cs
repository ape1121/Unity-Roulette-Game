using System;
using System.Collections.Generic;
using Ape.Game.UI;
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
            ResolveCardsContentRoot();

            if (_resolvedCardsContentRoot == null || _rewardCardPrefab == null)
                return false;

            int rewardCount = rewards != null ? rewards.Count : 0;

            for (int i = 0; i < rewardCount; i++)
            {
                RewardCardUI card = GetOrCreateCard(i);
                if (card == null)
                    continue;

                InventoryRewardEntry rewardEntry = rewards[i];
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
            if (_sectionRoot != null)
                _sectionRoot.gameObject.SetActive(isVisible);
        }

        public void SetVisualState(bool isVisible)
        {
            if (_sectionRoot == null)
                return;

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
    }
}
