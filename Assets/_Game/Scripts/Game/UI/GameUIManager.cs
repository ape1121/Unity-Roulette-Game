using Ape.Core;
using Ape.Data;
using Ape.Profile;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
                _inventoryPendingCountText);

            _overlayController.Configure(
                _gameOverRoot,
                _cashOutOverlayRoot,
                _overlayShownPositionSource,
                _overlaySlideCompanionRoots,
                _gameOverSlideDuration,
                _gameOverSlideEase,
                _gameOverHideEase);

            _overlayController.Initialize();
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
            if (!_gameSubscribed && App.Game != null)
            {
                App.Game.StateChanged += HandleGameStateChanged;
                App.Game.SpinResolved += HandleSpinResolved;
                _gameSubscribed = true;
            }

            if (!_profileSubscribed && App.Profile != null)
            {
                App.Profile.DataChanged += HandleProfileDataChanged;
                _profileSubscribed = true;
            }
        }

        private void UnsubscribeFromManagers()
        {
            if (_gameSubscribed && App.Game != null)
            {
                App.Game.StateChanged -= HandleGameStateChanged;
                App.Game.SpinResolved -= HandleSpinResolved;
            }

            if (_profileSubscribed && App.Profile != null)
                App.Profile.DataChanged -= HandleProfileDataChanged;

            _gameSubscribed = false;
            _profileSubscribed = false;
        }

        private void HandleSpinClicked()
        {
            if (App.Game == null)
                return;

            App.Game.TrySpin(out _);
        }

        private void HandleCashOutClicked()
        {
            if (App.Game == null)
                return;

            App.Game.TryCashOut();
        }

        private void HandleContinueClicked()
        {
            if (App.Game == null)
                return;

            App.Game.TryContinue();
        }

        private void HandleRestartClicked()
        {
            if (App.Game == null)
                return;

            App.Game.TryRestartRun();
        }

        private void HandleInventoryClicked()
        {
            _inventoryWindow ??= GetComponentInChildren<InventoryUIWindow>(true);

            if (_inventoryWindow == null)
                return;

            _inventoryWindow.Toggle();
        }

        private void HandleGameStateChanged(GameStateSnapshot snapshot)
        {
            RefreshState(snapshot, instant: false);
        }

        private void HandleSpinResolved(RouletteSpinResult spinResult)
        {
            // spawn spin result effect prefab
        }

        private void HandleProfileDataChanged(SaveData _)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshState(App.Game != null ? App.Game.CurrentState : default, instant: true);

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
            if (App.Game == null || App.Game.Config == null)
                return "CONTINUE";

            int continueCost = Mathf.Max(0, App.Game.Config.continueCost);
            return continueCost > 0 ? $"CONTINUE ({continueCost} CASH)" : "CONTINUE";
        }

        private bool IsCashOutAnytimeEnabled()
        {
            return App.Game != null
                   && App.Game.Config != null
                   && !App.Game.Config.cashOutOnSafeZoneOnly;
        }

        private RectTransform ResolveDesiredOverlayRoot(bool showGameOverOverlay, bool showCashOutOverlay)
        {
            if (showGameOverOverlay)
                return _gameOverRoot;

            if (showCashOutOverlay)
                return _cashOutOverlayRoot;

            return null;
        }

        private static bool IsBeforeFirstSpinOfRun()
        {
            return App.Game != null && App.Game.LastSpinResult.SelectedSlice.SliceRule == null;
        }

        private bool IsSpinRevealPending()
        {
            return App.Game != null
                && App.Game.IsSceneBound
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
