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
    public sealed class GameUIManager : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _cashOutButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _inventoryButton;

        [Header("Overlays")]
        [SerializeField] private TextMeshProUGUI _continueButtonLabel;
        [SerializeField] private RectTransform _gameOverRoot;
        [SerializeField] private RectTransform _cashOutOverlayRoot;
        [SerializeField] private RectTransform _overlayShownPositionSource;
        [SerializeField] private RectTransform[] _overlaySlideCompanionRoots;
        [SerializeField] private float _gameOverSlideDuration = 0.4f;
        [SerializeField] private Ease _gameOverSlideEase = Ease.OutBack;
        [SerializeField] private Ease _gameOverHideEase = Ease.InBack;

        [Header("Run State")]
        [SerializeField] private TextMeshProUGUI _zoneValueText;
        [SerializeField] private TextMeshProUGUI _zoneTypeValueText;
        [SerializeField] private TextMeshProUGUI _phaseValueText;
        [SerializeField] private TextMeshProUGUI _statusValueText;

        [Header("Run Rewards")]
        [SerializeField] private TextMeshProUGUI _pendingCashValueText;
        [SerializeField] private TextMeshProUGUI _pendingGoldValueText;
        [SerializeField] private TextMeshProUGUI _pendingItemsValueText;

        [Header("Profile")]
        [SerializeField] private TextMeshProUGUI _savedCashValueText;
        [SerializeField] private TextMeshProUGUI _savedGoldValueText;
        [SerializeField] private GameObject _inventoryPendingBadgeRoot;
        [SerializeField] private TextMeshProUGUI _inventoryPendingCountText;
        [SerializeField] private InventoryUIWindow _inventoryWindow;

        [Header("Presentation")]
        [SerializeField] private RouletteWheelUI _rouletteWheel;

        [Header("Feedback")]
        [SerializeField] private GameUIEffects _effects;

        private readonly GameUIActionButtonsController _actionButtonsController = new GameUIActionButtonsController();
        private readonly GameUIHudPresenter _hudPresenter = new GameUIHudPresenter();
        private readonly GameUIOverlayController _overlayController = new GameUIOverlayController();
        private GameManager _gameManager;
        private ProfileManager _profileManager;
        private GameUiTextConfig _uiTextConfig;
        private bool _gameSubscribed;
        private bool _profileSubscribed;

        public GameUIEffects Effects => _effects;

        private void OnEnable()
        {
            ConfigureControllers();
            BindButtons();
            SubscribeToManagers();
            RefreshAll();
        }

        private void OnDisable()
        {
            _overlayController.KillTweens();
            UnsubscribeFromManagers();
            UnbindButtons();
        }

        private void OnValidate()
        {
            _effects ??= GetComponent<GameUIEffects>();
            _inventoryWindow ??= GetComponentInChildren<InventoryUIWindow>(true);
            _rouletteWheel ??= GetComponentInChildren<RouletteWheelUI>(true);
            ResolveButtonReferences();
        }

        public void Bind(GameManager gameManager, ProfileManager profileManager, GameUiTextConfig textConfig)
        {
            UnsubscribeFromManagers();
            _gameManager = gameManager;
            _profileManager = profileManager;
            _uiTextConfig = textConfig;

            ConfigureControllers();
            ApplyBindingContext();

            if (!isActiveAndEnabled)
                return;

            SubscribeToManagers();
            RefreshAll();
        }

        public void Unbind()
        {
            UnsubscribeFromManagers();
            _gameManager = null;
            _profileManager = null;
            _uiTextConfig = null;

            if (_inventoryWindow != null)
                _inventoryWindow.Unbind();
        }

        private void ConfigureControllers()
        {
            _actionButtonsController.Configure(
                _spinButton,
                _cashOutButton,
                _continueButton,
                _restartButton,
                _inventoryButton,
                _continueButtonLabel);

            _hudPresenter.Configure(
                _zoneValueText,
                _zoneTypeValueText,
                _phaseValueText,
                _statusValueText,
                _pendingCashValueText,
                _pendingGoldValueText,
                _pendingItemsValueText,
                _savedCashValueText,
                _savedGoldValueText,
                _inventoryPendingBadgeRoot,
                _inventoryPendingCountText,
                _uiTextConfig);

            _overlayController.Configure(
                _gameOverRoot,
                _cashOutOverlayRoot,
                _overlayShownPositionSource,
                _overlaySlideCompanionRoots,
                _gameOverSlideDuration,
                _gameOverSlideEase,
                _gameOverHideEase);

            _overlayController.Initialize();
            ApplyBindingContext();
        }

        private void ApplyBindingContext()
        {
            if (_inventoryWindow != null)
                _inventoryWindow.Bind(_gameManager, _profileManager, _uiTextConfig);
        }

        private void ResolveButtonReferences()
        {
            _spinButton ??= UIReferenceUtility.FindButtonByName(this, "Spin");
            _cashOutButton ??= UIReferenceUtility.FindButtonByName(this, "CashOut");
            _continueButton ??= UIReferenceUtility.FindButtonByName(this, "Continue");
            _restartButton ??= UIReferenceUtility.FindButtonByName(this, "Restart");
            _inventoryButton ??= UIReferenceUtility.FindButtonByName(this, "Inventory");
        }

        private void BindButtons()
        {
            _actionButtonsController.Bind(
                HandleSpinClicked,
                HandleCashOutClicked,
                HandleContinueClicked,
                HandleRestartClicked,
                HandleInventoryClicked);
        }

        private void UnbindButtons()
        {
            _actionButtonsController.Unbind(
                HandleSpinClicked,
                HandleCashOutClicked,
                HandleContinueClicked,
                HandleRestartClicked,
                HandleInventoryClicked);
        }

        private void SubscribeToManagers()
        {
            if (!_gameSubscribed && _gameManager != null)
            {
                _gameManager.StateChanged += HandleGameStateChanged;
                _gameSubscribed = true;
            }

            if (!_profileSubscribed && _profileManager != null)
            {
                _profileManager.DataChanged += HandleProfileDataChanged;
                _profileSubscribed = true;
            }
        }

        private void UnsubscribeFromManagers()
        {
            if (_gameSubscribed && _gameManager != null)
            {
                _gameManager.StateChanged -= HandleGameStateChanged;
            }

            if (_profileSubscribed && _profileManager != null)
                _profileManager.DataChanged -= HandleProfileDataChanged;

            _gameSubscribed = false;
            _profileSubscribed = false;
        }

        private void HandleSpinClicked()
        {
            if (_gameManager == null)
                return;

            _gameManager.TrySpin(out _);
        }

        private void HandleCashOutClicked()
        {
            if (_gameManager == null)
                return;

            _gameManager.TryCashOut();
        }

        private void HandleContinueClicked()
        {
            if (_gameManager == null)
                return;

            _gameManager.TryContinue();
        }

        private void HandleRestartClicked()
        {
            if (_gameManager == null)
                return;

            _gameManager.TryRestartRun();
        }

        private void HandleInventoryClicked()
        {
            _inventoryWindow ??= GetComponentInChildren<InventoryUIWindow>(true);
            ApplyBindingContext();

            if (_inventoryWindow == null)
                return;

            _inventoryWindow.Toggle();
        }

        private void HandleGameStateChanged(GameStateSnapshot snapshot)
        {
            RefreshState(snapshot, instant: false);
        }

        private void HandleProfileDataChanged(SaveData _)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshState(_gameManager != null ? _gameManager.CurrentState : default, instant: true);

            if (_inventoryWindow != null)
                _inventoryWindow.Refresh();
        }

        private void RefreshState(GameStateSnapshot state, bool instant)
        {
            _hudPresenter.Refresh(state);

            bool isSpinRevealPending = IsSpinRevealPending();
            SetWheelIdlePresentationState(state.CanSpin && !isSpinRevealPending, IsBeforeFirstSpinOfRun());

            bool isGameOver = state.Phase == GameRunPhase.Busted
                              || state.Phase == GameRunPhase.CashedOut
                              || state.Phase == GameRunPhase.Completed;
            bool isAwaitingSpin = state.Phase == GameRunPhase.AwaitingSpin;
            bool isSafeZone = state.CurrentZoneType == RouletteZoneType.Safe
                              || state.CurrentZoneType == RouletteZoneType.Super;
            bool cashOutAnytimeEnabled = IsCashOutAnytimeEnabled();
            bool showCashOutOverlay = (isAwaitingSpin && (state.CanCashOut || isSafeZone))
                                      || (isGameOver && cashOutAnytimeEnabled);
            RectTransform desiredOverlayRoot = ResolveDesiredOverlayRoot(isGameOver, showCashOutOverlay);

            bool showContinueButton = _overlayController.ResolveContinueButtonVisibility(
                _continueButton,
                state.CanContinue,
                desiredOverlayRoot,
                instant);

            _actionButtonsController.ApplyState(
                state.CanSpin && !isSpinRevealPending,
                state.CanCashOut,
                state.CanContinue,
                state.CanRestart,
                showContinueButton,
                BuildContinueButtonLabel());

            _overlayController.Update(desiredOverlayRoot, instant);
            _overlayController.UpdateCompanionRoots(desiredOverlayRoot != null, desiredOverlayRoot, instant);
        }

        private string BuildContinueButtonLabel()
        {
            if (_gameManager == null || _gameManager.Config == null || _uiTextConfig == null)
                return string.Empty;

            int continueCost = Mathf.Max(0, _gameManager.Config.continueCost);
            return _uiTextConfig.FormatContinueLabel(continueCost);
        }

        private bool IsCashOutAnytimeEnabled()
        {
            return _gameManager != null
                   && _gameManager.Config != null
                   && !_gameManager.Config.cashOutOnSafeZoneOnly;
        }

        private RectTransform ResolveDesiredOverlayRoot(bool showGameOverOverlay, bool showCashOutOverlay)
        {
            if (showGameOverOverlay)
                return _gameOverRoot;

            if (showCashOutOverlay)
                return _cashOutOverlayRoot;

            return null;
        }

        private bool IsBeforeFirstSpinOfRun()
        {
            return _gameManager != null && _gameManager.Roulette.LastSpinResult.SelectedSlice.SliceRule == null;
        }

        private bool IsSpinRevealPending()
        {
            return _gameManager != null
                && _gameManager.IsSceneBound
                && _rouletteWheel != null
                && _rouletteWheel.IsPostSpinRevealPending;
        }

        private void SetWheelIdlePresentationState(bool isSpinButtonIdleActive, bool isWheelIdleRotationActive)
        {
            if (_rouletteWheel != null)
                _rouletteWheel.SetIdlePresentationActive(isSpinButtonIdleActive, isSpinButtonIdleActive && isWheelIdleRotationActive);
        }
    }
}
