using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
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

        private readonly List<InventoryRewardEntry> _pendingRewards = new List<InventoryRewardEntry>();
        private readonly List<InventoryRewardEntry> _bankedRewards = new List<InventoryRewardEntry>();
        private readonly InventoryWindowAnimationController _animationController = new InventoryWindowAnimationController();
        private readonly InventoryRewardSectionController _pendingSectionController = new InventoryRewardSectionController();
        private readonly InventoryRewardSectionController _bankedSectionController = new InventoryRewardSectionController();
        private GameManager _gameManager;
        private ProfileManager _profileManager;
        private GameUiTextConfig _uiTextConfig;
        private bool _isProfileSubscribed;
        private bool _isGameSubscribed;
        private bool _isOpen;
        private InventoryTab _activeTab = InventoryTab.Pending;
        private InventoryTab _appliedTab = InventoryTab.Pending;
        private int _lastPendingBadgeCount = int.MinValue;
        private int _lastVisibleRewardCount = int.MinValue;
        private bool _hasAppliedTabState;
        private CaseOpenSession _activeCaseSession;
        private bool _hasActiveCaseSession;
        private bool _caseRollCommitted;

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
            _animationController.ApplyClosedState();
        }

        private void OnEnable()
        {
            CacheReferences();
            BindButtons();
            SubscribeToSources();
            Refresh();

            if (_isOpen)
                _animationController.ApplyOpenState();
            else
                _animationController.ApplyClosedState();
        }

        private void OnDisable()
        {
            _animationController.KillTransition();
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

        public void Bind(GameManager gameManager, ProfileManager profileManager, GameUiTextConfig textConfig)
        {
            UnsubscribeFromSources();
            _gameManager = gameManager;
            _profileManager = profileManager;
            _uiTextConfig = textConfig;
            ConfigureCaseOpenUi();

            if (!isActiveAndEnabled)
                return;

            SubscribeToSources();
            Refresh();
        }

        public void Unbind()
        {
            UnsubscribeFromSources();
            _gameManager = null;
            _profileManager = null;
            _uiTextConfig = null;
            ConfigureCaseOpenUi();
        }

        public void Refresh()
        {
            CacheReferences();
            BuildPendingRewards();
            BuildBankedRewards();
            SyncSections();
            RefreshPendingBadge();
            ApplyActiveTab();
            RefreshCaseOpenUi();
        }

        public void Open(bool instant = false)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            CacheReferences();
            BindButtons();
            Refresh();

            _isOpen = true;
            _animationController.SetInteractionState(true);
            SetCaseOpenUiVisible(false);
            _animationController.PlayTransition(gameObject, show: true, instant, onHidden: null);
        }

        public void Close(bool instant = false)
        {
            if (!gameObject.activeSelf)
                return;

            CacheReferences();
            StopCaseOpenPresentation(refresh: false);

            _isOpen = false;
            _animationController.SetInteractionState(false);
            _animationController.PlayTransition(gameObject, show: false, instant, () => gameObject.SetActive(false));
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void CacheReferences()
        {
            _windowRoot ??= GetComponent<RectTransform>();
            _windowCanvasGroup ??= GetComponent<CanvasGroup>();
            _caseOpenUI ??= GetComponentInChildren<CaseOpenUI>(true);
            ConfigureCaseOpenUi();

            _animationController.Configure(
                _windowCanvasGroup,
                _panelRoot,
                _fadeDuration,
                _panelDuration,
                _hiddenPanelOffset,
                _hiddenPanelScale,
                _openEase,
                _closeEase);
            _animationController.CachePanelOpenPosition();

            _pendingSectionController.Configure(_pendingContentRoot, _pendingCardsContentRoot, _rewardCardPrefab);
            _bankedSectionController.Configure(_bankedContentRoot, _bankedCardsContentRoot, _rewardCardPrefab);
        }

        private void ResolveButtonReferences()
        {
            _pendingTabButton ??= UIReferenceUtility.FindButtonByName(this, "Pending");
            _bankedTabButton ??= UIReferenceUtility.FindButtonByName(this, "Banked");
            _backdropButton ??= UIReferenceUtility.FindButtonByName(this, "BackgroundBlocker");
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

        private void SubscribeToSources()
        {
            if (!_isProfileSubscribed && _profileManager != null)
            {
                _profileManager.DataChanged += HandleProfileDataChanged;
                _isProfileSubscribed = true;
            }

            if (!_isGameSubscribed && _gameManager != null)
            {
                _gameManager.StateChanged += HandleGameStateChanged;
                _isGameSubscribed = true;
            }
        }

        private void UnsubscribeFromSources()
        {
            if (_isProfileSubscribed && _profileManager != null)
                _profileManager.DataChanged -= HandleProfileDataChanged;

            if (_isGameSubscribed && _gameManager != null)
                _gameManager.StateChanged -= HandleGameStateChanged;

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

        private void HandlePendingTabClicked()
        {
            SetActiveTab(InventoryTab.Pending);
        }

        private void HandleBankedTabClicked()
        {
            SetActiveTab(InventoryTab.Banked);
        }

        private void BuildPendingRewards()
        {
            _pendingRewards.Clear();

            if (_gameManager == null || _gameManager.Inventory == null)
                return;

            _gameManager.Inventory.GetPendingRewards(_pendingRewards);
        }

        private void BuildBankedRewards()
        {
            _bankedRewards.Clear();

            if (_gameManager == null || _gameManager.Inventory == null)
                return;

            _gameManager.Inventory.GetBankedRewards(_bankedRewards);
        }

        private void SyncSections()
        {
            if (_rewardCardPrefab == null)
                return;

            _pendingSectionController.Sync(_pendingRewards, ResolveRarityColor, ConfigureCardAction);
            _bankedSectionController.Sync(_bankedRewards, ResolveRarityColor, ConfigureCardAction);
        }

        private Color ResolveRarityColor(InventoryRewardEntry rewardEntry)
        {
            return rewardEntry.HasReward && _gameManager != null
                ? _gameManager.Rewards.GetRarityColor(rewardEntry.Rarity, Color.white)
                : Color.white;
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

            _pendingSectionController.SetVisualState(false);
            _bankedSectionController.SetVisualState(false);
            _pendingSectionController.SetVisible(showPending);
            _bankedSectionController.SetVisible(!showPending);

            InventoryRewardSectionController visibleSection = showPending ? _pendingSectionController : _bankedSectionController;
            visibleSection.ForceLayout();
            visibleSection.SetVisualState(true);

            SetButtonInteractable(_pendingTabButton, !showPending);
            SetButtonInteractable(_bankedTabButton, showPending);
            RefreshTabTexts(showPending);
            SetEmptyStateVisible(visibleRewardCount == 0);

            _appliedTab = _activeTab;
            _lastVisibleRewardCount = visibleRewardCount;
            _hasAppliedTabState = true;
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
                _tabTitleText.text = _uiTextConfig == null
                    ? string.Empty
                    : showPending
                        ? _uiTextConfig.PendingInventoryTabTitle
                        : _uiTextConfig.BankedInventoryTabTitle;
        }

        private void SetEmptyStateVisible(bool isVisible)
        {
            if (_emptyStateRoot != null)
                _emptyStateRoot.SetActive(isVisible);
        }

        private void ConfigureCardAction(RewardCardUI card, InventoryRewardEntry rewardEntry)
        {
            if (card == null)
                return;

            if (!rewardEntry.CanOpenCase || _caseOpenUI == null || _gameManager == null || _gameManager.Inventory == null)
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
                || _gameManager == null
                || _gameManager.Inventory == null
                || !_gameManager.Inventory.Cases.TryPrepareCaseOpen(rewardId, out CaseOpenSession caseOpenSession))
                return;

            _activeCaseSession = caseOpenSession;
            _hasActiveCaseSession = true;
            _caseRollCommitted = false;
            SetCaseOpenUiVisible(true);
            _caseOpenUI.ShowPreview(
                caseOpenSession,
                _gameManager.Inventory.Cases.CanRollPreparedCase(caseOpenSession),
                HandleCaseRollRequested,
                HandleCasePresentationClosed);
            Refresh();
        }

        private void HandleCaseRollRequested(CaseOpenSession caseOpenSession)
        {
            if (!_hasActiveCaseSession
                || !_activeCaseSession.IsValid
                || _activeCaseSession.SessionId != caseOpenSession.SessionId
                || _gameManager == null
                || _gameManager.Inventory == null)
            {
                RefreshCaseOpenUi();
                return;
            }

            _caseRollCommitted = true;

            if (!_gameManager.Inventory.Cases.TryStartCaseRoll(caseOpenSession, out CaseOpenResult caseOpenResult))
            {
                _caseRollCommitted = false;
                Refresh();
                return;
            }

            Refresh();
            _caseOpenUI.ShowRollResult(caseOpenResult);
        }

        private void HandleCasePresentationClosed()
        {
            StopCaseOpenPresentation(refresh: true);
        }

        private void StopCaseOpenPresentation(bool refresh)
        {
            if (_caseOpenUI != null)
            {
                _caseOpenUI.StopAnimation();
                _caseOpenUI.HidePresentation();
                SetCaseOpenUiVisible(false);
            }

            CompleteCasePresentation(refresh);
        }

        private void CompleteCasePresentation(bool refresh)
        {
            if (_gameManager != null && _gameManager.Inventory != null)
                _gameManager.Inventory.Cases.CompletePresentation();

            _activeCaseSession = default;
            _hasActiveCaseSession = false;
            _caseRollCommitted = false;

            if (refresh && isActiveAndEnabled)
                Refresh();
        }

        private void SetCaseOpenUiVisible(bool isVisible)
        {
            if (_caseOpenUI != null)
                _caseOpenUI.gameObject.SetActive(isVisible);
        }

        private void ConfigureCaseOpenUi()
        {
            if (_caseOpenUI != null)
                _caseOpenUI.SetPresentationContext(_gameManager != null ? _gameManager.Rewards : null, _uiTextConfig);
        }

        private void RefreshCaseOpenUi()
        {
            if (_caseOpenUI == null || !_hasActiveCaseSession || _caseRollCommitted || _gameManager == null || _gameManager.Inventory == null)
                return;

            _caseOpenUI.SetRollInteractable(_gameManager.Inventory.Cases.CanRollPreparedCase(_activeCaseSession));
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
    }
}
