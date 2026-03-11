using System.Collections.Generic;
using Ape.Core;
using Ape.Game.UI;
using Ape.Profile;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryUIWindow : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _backdropButton;
        [SerializeField] private Button _pendingTabButton;
        [SerializeField] private Button _bankedTabButton;
        
        [Header("Structure")]
        [SerializeField] private RectTransform _windowRoot;
        [SerializeField] private CanvasGroup _windowCanvasGroup;
        [SerializeField] private RectTransform _panelRoot;

        [Header("Tabs")]
        [SerializeField] private GameObject _pendingTabBadgeRoot;
        [SerializeField] private TextMeshProUGUI _pendingTabBadgeText;
        [SerializeField] private TextMeshProUGUI _tabTitleText;
        [SerializeField] private string _pendingTabTitle = "PENDING";
        [SerializeField] private string _bankedTabTitle = "BANKED";

        [Header("Sections")]
        [SerializeField] private Transform _pendingContentRoot;
        [SerializeField] private Transform _pendingCardsContentRoot;
        [SerializeField] private Transform _bankedContentRoot;
        [SerializeField] private Transform _bankedCardsContentRoot;
        [SerializeField] private GameObject _emptyStateRoot;
        [SerializeField] private RewardCardUI _rewardCardPrefab;
        [SerializeField] private CaseOpenUI _caseOpenUI;

        [Header("Animation")]
        [SerializeField] private float _fadeDuration = 0.18f;
        [SerializeField] private float _panelDuration = 0.28f;
        [SerializeField] private float _hiddenPanelOffset = 48f;
        [SerializeField] private float _hiddenPanelScale = 0.96f;
        [SerializeField] private Ease _openEase = Ease.OutCubic;
        [SerializeField] private Ease _closeEase = Ease.InCubic;

        private readonly List<RewardCardUI> _pendingCards = new List<RewardCardUI>();
        private readonly List<RewardCardUI> _bankedCards = new List<RewardCardUI>();
        private readonly List<InventoryRewardEntry> _pendingRewards = new List<InventoryRewardEntry>();
        private readonly List<InventoryRewardEntry> _bankedRewards = new List<InventoryRewardEntry>();

        private Sequence _transitionSequence;
        private bool _isProfileSubscribed;
        private bool _isGameSubscribed;
        private bool _isOpen;
        private bool _hasCachedPanelPosition;
        private Vector2 _panelOpenAnchoredPosition;
        private InventoryTab _activeTab = InventoryTab.Pending;
        private InventoryTab _appliedTab = InventoryTab.Pending;
        private int _pendingRewardsSignature = int.MinValue;
        private int _bankedRewardsSignature = int.MinValue;
        private int _lastPendingBadgeCount = int.MinValue;
        private int _lastVisibleRewardCount = int.MinValue;
        private bool _hasAppliedTabState;
        private bool _pendingCardsInitialized;
        private bool _bankedCardsInitialized;
        private Transform _resolvedPendingCardsContentRoot;
        private Transform _resolvedBankedCardsContentRoot;

        public bool IsOpen => _isOpen;

        private enum InventoryTab
        {
            Pending,
            Banked
        }

        private void Awake()
        {
            CacheReferences();
            BindButtons();
            CachePanelOpenPosition();
            ApplyClosedState(force: true);
        }

        private void OnEnable()
        {
            CacheReferences();
            BindButtons();
            CachePanelOpenPosition();
            SubscribeToSources();
            Refresh();

            if (_isOpen)
                ApplyOpenState(force: true);
            else
                ApplyClosedState(force: true);
        }

        private void OnDisable()
        {
            KillTransition();
            StopCaseOpenPresentation(refresh: false);
            UnsubscribeFromSources();
        }

        private void OnValidate()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            _windowCanvasGroup ??= GetComponent<CanvasGroup>();
            _caseOpenUI ??= GetComponentInChildren<CaseOpenUI>(true);
            ResolveButtonReferences();
        }

        private void ResolveButtonReferences()
        {
            _pendingTabButton ??= UIReferenceUtility.FindButtonByName(this, "Pending");
            _bankedTabButton ??= UIReferenceUtility.FindButtonByName(this, "Banked");
            _backdropButton ??= UIReferenceUtility.FindButtonByName(this, "BackgroundBlocker");
        }

        public void Refresh()
        {
            CacheReferences();

            int pendingSignature = BuildPendingRewards();
            int bankedSignature = BuildBankedRewards();
            bool pendingChanged = pendingSignature != _pendingRewardsSignature;
            bool bankedChanged = bankedSignature != _bankedRewardsSignature;

            if (_rewardCardPrefab != null)
            {
                if (!_pendingCardsInitialized || pendingChanged)
                    _pendingCardsInitialized = SyncSection(_pendingRewards, _pendingCards, _resolvedPendingCardsContentRoot);

                if (!_bankedCardsInitialized || bankedChanged)
                    _bankedCardsInitialized = SyncSection(_bankedRewards, _bankedCards, _resolvedBankedCardsContentRoot);
            }

            _pendingRewardsSignature = pendingSignature;
            _bankedRewardsSignature = bankedSignature;
            RefreshPendingBadge();
            ApplyActiveTab();
        }

        public void Open(bool instant = false)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            CacheReferences();
            BindButtons();
            CachePanelOpenPosition();
            Refresh();

            _isOpen = true;
            SetInteractionState(true);
            SetCaseOpenUiVisible(false);
            PlayTransition(show: true, instant);
        }

        public void Close(bool instant = false)
        {
            if (!gameObject.activeSelf)
                return;

            CacheReferences();
            StopCaseOpenPresentation(refresh: false);

            _isOpen = false;
            SetInteractionState(false);
            PlayTransition(show: false, instant);
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void SubscribeToSources()
        {
            if (!_isProfileSubscribed && App.Profile != null)
            {
                App.Profile.DataChanged += HandleProfileDataChanged;
                _isProfileSubscribed = true;
            }

            if (!_isGameSubscribed && App.Game != null)
            {
                App.Game.StateChanged += HandleGameStateChanged;
                _isGameSubscribed = true;
            }
        }

        private void UnsubscribeFromSources()
        {
            if (_isProfileSubscribed && App.Profile != null)
                App.Profile.DataChanged -= HandleProfileDataChanged;

            if (_isGameSubscribed && App.Game != null)
                App.Game.StateChanged -= HandleGameStateChanged;

            _isProfileSubscribed = false;
            _isGameSubscribed = false;
        }

        private void HandleProfileDataChanged(SaveData _)
        {
            Refresh();
        }

        private void HandleGameStateChanged(GameStateSnapshot _)
        {
            Refresh();
        }

        private void HandleBackdropClicked()
        {
            Close();
        }

        private void HandleCloseClicked()
        {
            Close();
        }

        private void HandlePendingTabClicked()
        {
            SetActiveTab(InventoryTab.Pending);
        }

        private void HandleBankedTabClicked()
        {
            SetActiveTab(InventoryTab.Banked);
        }

        private void CacheReferences()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            _windowCanvasGroup ??= GetComponent<CanvasGroup>();
            _caseOpenUI ??= GetComponentInChildren<CaseOpenUI>(true);
            _resolvedPendingCardsContentRoot = ResolveCardsContentRoot(_pendingCardsContentRoot, _pendingContentRoot, _resolvedPendingCardsContentRoot);
            _resolvedBankedCardsContentRoot = ResolveCardsContentRoot(_bankedCardsContentRoot, _bankedContentRoot, _resolvedBankedCardsContentRoot);
        }

        private void BindButtons()
        {
            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(HandleBackdropClicked);
                _backdropButton.onClick.AddListener(HandleBackdropClicked);
            }

            if (_pendingTabButton != null)
            {
                _pendingTabButton.onClick.RemoveListener(HandlePendingTabClicked);
                _pendingTabButton.onClick.AddListener(HandlePendingTabClicked);
            }

            if (_bankedTabButton != null)
            {
                _bankedTabButton.onClick.RemoveListener(HandleBankedTabClicked);
                _bankedTabButton.onClick.AddListener(HandleBankedTabClicked);
            }
        }

        private void CachePanelOpenPosition()
        {
            if (_panelRoot == null || _hasCachedPanelPosition)
                return;

            _panelOpenAnchoredPosition = _panelRoot.anchoredPosition;
            _hasCachedPanelPosition = true;
        }

        private void PlayTransition(bool show, bool instant)
        {
            if (_windowCanvasGroup == null || _panelRoot == null)
            {
                if (!show)
                    gameObject.SetActive(false);

                return;
            }

            KillTransition();

            if (instant)
            {
                if (show)
                    ApplyOpenState(force: true);
                else
                {
                    ApplyClosedState(force: true);
                    gameObject.SetActive(false);
                }

                return;
            }

            if (show)
            {
                ApplyClosedVisualState();
                _transitionSequence = DOTween.Sequence()
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnKill(() => _transitionSequence = null);
                _transitionSequence.Join(_windowCanvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutCubic));
                _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition, _panelDuration).SetEase(_openEase));
                _transitionSequence.Join(_panelRoot.DOScale(1f, _panelDuration).SetEase(_openEase));
            }
            else
            {
                _transitionSequence = DOTween.Sequence()
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() =>
                    {
                        ApplyClosedState(force: true);
                        gameObject.SetActive(false);
                    })
                    .OnKill(() => _transitionSequence = null);
                _transitionSequence.Join(_windowCanvasGroup.DOFade(0f, _fadeDuration).SetEase(Ease.InCubic));
                _transitionSequence.Join(_panelRoot.DOAnchorPos(_panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset, _panelDuration).SetEase(_closeEase));
                _transitionSequence.Join(_panelRoot.DOScale(_hiddenPanelScale, _panelDuration).SetEase(_closeEase));
            }
        }

        private void ApplyOpenState(bool force)
        {
            if (!force && _isOpen)
                return;

            if (_windowCanvasGroup != null)
                _windowCanvasGroup.alpha = 1f;

            if (_panelRoot != null)
            {
                _panelRoot.anchoredPosition = _panelOpenAnchoredPosition;
                _panelRoot.localScale = Vector3.one;
            }
        }

        private void ApplyClosedState(bool force)
        {
            if (!force && !_isOpen)
                return;

            ApplyClosedVisualState();
            SetInteractionState(false);
        }

        private void ApplyClosedVisualState()
        {
            if (_windowCanvasGroup != null)
                _windowCanvasGroup.alpha = 0f;

            if (_panelRoot != null)
            {
                _panelRoot.anchoredPosition = _panelOpenAnchoredPosition + Vector2.down * _hiddenPanelOffset;
                _panelRoot.localScale = new Vector3(_hiddenPanelScale, _hiddenPanelScale, 1f);
            }
        }

        private void SetInteractionState(bool isInteractive)
        {
            if (_windowCanvasGroup == null)
                return;

            _windowCanvasGroup.interactable = isInteractive;
            _windowCanvasGroup.blocksRaycasts = isInteractive;
        }

        private void KillTransition()
        {
            if (_transitionSequence != null && _transitionSequence.IsActive())
                _transitionSequence.Kill();

            _transitionSequence = null;
        }

        private int BuildPendingRewards()
        {
            _pendingRewards.Clear();

            if (App.Game == null || App.Game.Inventory == null)
                return CalculateRewardsSignature(_pendingRewards);

            App.Game.Inventory.GetPendingRewards(_pendingRewards);
            return CalculateRewardsSignature(_pendingRewards);
        }

        private int BuildBankedRewards()
        {
            _bankedRewards.Clear();

            if (App.Game == null || App.Game.Inventory == null)
                return CalculateRewardsSignature(_bankedRewards);

            App.Game.Inventory.GetBankedRewards(_bankedRewards);
            return CalculateRewardsSignature(_bankedRewards);
        }

        private bool SyncSection(
            List<InventoryRewardEntry> rewards,
            List<RewardCardUI> cards,
            Transform contentRoot)
        {
            if (contentRoot == null || _rewardCardPrefab == null)
                return false;

            for (int i = 0; i < rewards.Count; i++)
            {
                RewardCardUI card = GetOrCreateCard(i, cards, contentRoot);
                if (card == null)
                    continue;

                InventoryRewardEntry rewardEntry = rewards[i];
                ResolvedReward reward = rewardEntry.Reward;
                Color rarityColor = reward.HasReward && App.Game != null
                    ? App.Game.Rewards.GetRarityColor(reward.Rarity, Color.white)
                    : Color.white;

                card.Bind(reward, rarityColor);
                ConfigureCardAction(card, rewardEntry);
                card.gameObject.SetActive(true);
            }

            for (int i = rewards.Count; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].ClearAction();
                    cards[i].gameObject.SetActive(false);
                }
            }

            return true;
        }

        private RewardCardUI GetOrCreateCard(int index, List<RewardCardUI> cards, Transform contentRoot)
        {
            while (cards.Count <= index)
            {
                RewardCardUI card = Instantiate(_rewardCardPrefab, contentRoot);
                card.gameObject.SetActive(false);
                cards.Add(card);
            }

            return cards[index];
        }

        private void SetActiveTab(InventoryTab tab)
        {
            if (_activeTab == tab)
                return;

            _activeTab = tab;
            ApplyActiveTab();
        }

        private void ApplyActiveTab()
        {
            bool showPending = _activeTab == InventoryTab.Pending;
            int visibleRewardCount = showPending ? _pendingRewards.Count : _bankedRewards.Count;

            if (_hasAppliedTabState && _appliedTab == _activeTab && _lastVisibleRewardCount == visibleRewardCount)
                return;

            Transform visibleContentRoot = showPending ? _pendingContentRoot : _bankedContentRoot;
            Transform visibleCardsContentRoot = showPending ? _resolvedPendingCardsContentRoot : _resolvedBankedCardsContentRoot;

            SetSectionVisualState(_pendingContentRoot, false);
            SetSectionVisualState(_bankedContentRoot, false);
            ApplySectionVisibility(_pendingContentRoot, showPending);
            ApplySectionVisibility(_bankedContentRoot, !showPending);
            ForceSectionLayout(visibleContentRoot, visibleCardsContentRoot);
            SetSectionVisualState(visibleContentRoot, true);
            SetButtonInteractable(_pendingTabButton, !showPending);
            SetButtonInteractable(_bankedTabButton, showPending);
            RefreshTabTexts(showPending);
            SetEmptyStateVisible(visibleRewardCount == 0);

            _appliedTab = _activeTab;
            _lastVisibleRewardCount = visibleRewardCount;
            _hasAppliedTabState = true;
        }

        private void ApplySectionVisibility(Transform contentRoot, bool isVisible)
        {
            if (contentRoot != null)
                contentRoot.gameObject.SetActive(isVisible);
        }

        private static void SetSectionVisualState(Transform contentRoot, bool isVisible)
        {
            if (contentRoot == null)
                return;

            CanvasGroup canvasGroup = contentRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
        }

        private static void ForceSectionLayout(Transform sectionRoot, Transform cardsContentRoot)
        {
            if (sectionRoot == null)
                return;

            Canvas.ForceUpdateCanvases();

            DynamicScrollableGrid dynamicGrid = sectionRoot.GetComponent<DynamicScrollableGrid>();
            if (dynamicGrid == null)
                dynamicGrid = sectionRoot.GetComponentInChildren<DynamicScrollableGrid>(true);

            if (dynamicGrid != null)
                dynamicGrid.RefreshLayout();

            ScrollRect scrollRect = sectionRoot.GetComponentInChildren<ScrollRect>(true);
            RectTransform scrollContentRect = scrollRect != null ? scrollRect.content : null;
            RectTransform cardsRect = cardsContentRoot as RectTransform;
            RectTransform sectionRect = sectionRoot as RectTransform;

            if (scrollContentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);

            if (cardsRect != null && cardsRect != scrollContentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRect);

            if (sectionRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);

            Canvas.ForceUpdateCanvases();
        }

        private void SetEmptyStateVisible(bool isVisible)
        {
            if (_emptyStateRoot != null)
                _emptyStateRoot.SetActive(isVisible);
        }

        private void RefreshPendingBadge()
        {
            int pendingItemCount = GetRewardAmountTotal(_pendingRewards);

            if (_lastPendingBadgeCount == pendingItemCount)
                return;

            if (_pendingTabBadgeText != null)
                _pendingTabBadgeText.text = pendingItemCount.ToString();

            GameObject badgeRoot = _pendingTabBadgeRoot;
            if (badgeRoot == null && _pendingTabBadgeText != null)
                badgeRoot = _pendingTabBadgeText.gameObject;

            if (badgeRoot != null)
                badgeRoot.SetActive(pendingItemCount > 0);

            _lastPendingBadgeCount = pendingItemCount;
        }

        private void RefreshTabTexts(bool showPending)
        {
            if (_tabTitleText != null)
                _tabTitleText.text = showPending ? _pendingTabTitle : _bankedTabTitle;
        }

        private static int GetRewardAmountTotal(List<InventoryRewardEntry> rewards)
        {
            int total = 0;

            for (int i = 0; i < rewards.Count; i++)
                total += rewards[i].Amount;

            return total;
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
                button.interactable = isInteractable;
        }

        private void ConfigureCardAction(RewardCardUI card, InventoryRewardEntry rewardEntry)
        {
            if (card == null)
                return;

            if (!rewardEntry.CanOpenCase || _caseOpenUI == null || App.Game == null || App.Game.Inventory == null)
            {
                card.ClearAction();
                return;
            }

            string rewardId = rewardEntry.RewardId;
            card.BindAction(() => HandleCaseActionClicked(rewardId));
        }

        private void HandleCaseActionClicked(string rewardId)
        {
            if (string.IsNullOrWhiteSpace(rewardId)
                || _caseOpenUI == null
                || App.Game == null
                || App.Game.Inventory == null
                || !App.Game.Inventory.Cases.TryOpenCase(rewardId, out CaseOpenResult caseOpenResult))
                return;

            SetCaseOpenUiVisible(true);
            Refresh();
            _caseOpenUI.Play(caseOpenResult, HandleCaseOpenCompleted);
        }

        private void HandleCaseOpenCompleted(CaseOpenResult _)
        {
            CompleteCasePresentation(refresh: true);
        }

        private void StopCaseOpenPresentation(bool refresh)
        {
            if (_caseOpenUI != null)
            {
                _caseOpenUI.StopAnimation();
                SetCaseOpenUiVisible(false);
            }

            CompleteCasePresentation(refresh);
        }

        private void CompleteCasePresentation(bool refresh)
        {
            if (App.Game != null && App.Game.Inventory != null)
                App.Game.Inventory.Cases.CompletePresentation();

            if (refresh && isActiveAndEnabled)
                Refresh();
        }

        private void SetCaseOpenUiVisible(bool isVisible)
        {
            if (_caseOpenUI != null)
                _caseOpenUI.gameObject.SetActive(isVisible);
        }

        private static int CalculateRewardsSignature(List<InventoryRewardEntry> rewards)
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < rewards.Count; i++)
                {
                    InventoryRewardEntry reward = rewards[i];
                    hash = (hash * 31) + reward.Amount;
                    hash = (hash * 31) + (int)reward.Rarity;
                    hash = (hash * 31) + (int)reward.Action;
                    hash = (hash * 31) + (reward.RewardId != null ? reward.RewardId.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private static Transform ResolveCardsContentRoot(Transform explicitContentRoot, Transform sectionRoot, Transform cachedContentRoot)
        {
            if (explicitContentRoot != null)
                return explicitContentRoot;

            if (cachedContentRoot != null)
                return cachedContentRoot;

            if (sectionRoot == null)
                return null;

            ScrollRect scrollRect = sectionRoot.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null && scrollRect.content != null)
                return scrollRect.content;

            return sectionRoot;
        }
    }
}
