using System.Collections.Generic;
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
        private enum OverlayRootState
        {
            Hidden,
            Showing,
            Visible,
            Hiding
        }

        [Header("Action Buttons")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _cashOutButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _inventoryButton;

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
        [FormerlySerializedAs("_wheelThemeValueText")]
        [SerializeField] private TextMeshProUGUI _statusValueText;
        [SerializeField] private TextMeshProUGUI _lastRewardValueText;

        [Header("Run Rewards")]
        [SerializeField] private TextMeshProUGUI _pendingCashValueText;
        [SerializeField] private TextMeshProUGUI _pendingGoldValueText;
        [SerializeField] private TextMeshProUGUI _pendingItemsValueText;

        [Header("Profile")]
        [SerializeField] private TextMeshProUGUI _savedCashValueText;
        [SerializeField] private TextMeshProUGUI _savedGoldValueText;
        [SerializeField] private GameObject _inventoryPendingBadgeRoot;
        [SerializeField] private TextMeshProUGUI _inventoryPendingCountText;
        [FormerlySerializedAs("_inventoryList")]
        [SerializeField] private InventoryUIWindow _inventoryWindow;

        [Header("Feedback")]
        [SerializeField] private GameUIEffects _effects;
        [SerializeField] private GameObject _spinningBlockerRoot;

        private bool _gameSubscribed;
        private bool _profileSubscribed;
        private readonly Dictionary<RectTransform, Tween> _overlayTweens = new Dictionary<RectTransform, Tween>();
        private readonly Dictionary<RectTransform, OverlayRootState> _overlayRootStates = new Dictionary<RectTransform, OverlayRootState>();
        private readonly Dictionary<RectTransform, Vector2> _overlayShownAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector2> _companionRootBaseAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private RectTransform _desiredOverlayRoot;

        public GameUIEffects Effects => _effects;

        private void OnEnable()
        {
            InitializeSlidingRoot(_gameOverRoot);
            InitializeSlidingRoot(_cashOutOverlayRoot);
            InitializeCompanionRoots(_overlaySlideCompanionRoots);

            BindButtons();
            SubscribeToManagers();
            RefreshAll();
        }

        private void OnDisable()
        {
            KillOverlayTweens();
            UnsubscribeFromManagers();
            UnbindButtons();
        }

        private void OnValidate()
        {
            _effects ??= GetComponent<GameUIEffects>();
            _inventoryWindow ??= GetComponentInChildren<InventoryUIWindow>(true);
        }

        private void BindButtons()
        {
            UnbindButtons();

            if (_spinButton != null)
                _spinButton.onClick.AddListener(HandleSpinClicked);

            if (_cashOutButton != null)
                _cashOutButton.onClick.AddListener(HandleCashOutClicked);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(HandleContinueClicked);

            if (_restartButton != null)
                _restartButton.onClick.AddListener(HandleRestartClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(HandleInventoryClicked);
        }

        private void UnbindButtons()
        {
            if (_spinButton != null)
                _spinButton.onClick.RemoveListener(HandleSpinClicked);

            if (_cashOutButton != null)
                _cashOutButton.onClick.RemoveListener(HandleCashOutClicked);

            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(HandleContinueClicked);

            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(HandleRestartClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(HandleInventoryClicked);
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
            SetText(_lastRewardValueText, BuildSpinResultLabel(spinResult));
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

            if (App.Game != null)
                SetText(_lastRewardValueText, BuildSpinResultLabel(App.Game.LastSpinResult));
            else
                SetText(_lastRewardValueText, "-");
        }

        private void RefreshState(GameStateSnapshot state, bool instant)
        {
            SetText(_zoneValueText, state.CurrentZone > 0 ? "FLOOR " + state.CurrentZone.ToString() : "-");
            SetText(_zoneTypeValueText, string.Empty);
            SetText(_phaseValueText, BuildPhaseLabel(state));
            SetText(_pendingCashValueText, state.PendingCash.ToString());
            SetText(_pendingGoldValueText, state.PendingGold.ToString());
            SetText(_pendingItemsValueText, FormatPendingItems(state));
            SetText(_savedCashValueText, state.SavedCash.ToString());
            SetText(_savedGoldValueText, state.SavedGold.ToString());
            SetText(_statusValueText, BuildStatusLabel(state));
            RefreshInventoryPendingUi(state.PendingInventoryRewardCount);

            if (_zoneTypeValueText != null)
                _zoneTypeValueText.gameObject.SetActive(false);

            SetButtonInteractable(_spinButton, state.CanSpin);
            SetButtonInteractable(_cashOutButton, state.CanCashOut);
            SetButtonInteractable(_continueButton, state.CanContinue);
            SetButtonInteractable(_restartButton, state.CanRestart);
            SetWheelIdlePresentationState(state.CanSpin, IsBeforeFirstSpinOfRun());

            SetText(_continueButtonLabel, BuildContinueButtonLabel());

            if (_spinningBlockerRoot != null)
                _spinningBlockerRoot.SetActive(state.Phase == GameRunPhase.Spinning);

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

            bool showContinueButton = ResolveContinueButtonVisibility(state.CanContinue, desiredOverlayRoot, instant);
            SetButtonVisible(_continueButton, showContinueButton);

            UpdateOverlaySlot(desiredOverlayRoot, instant);
            UpdateCompanionRoots(desiredOverlayRoot != null, desiredOverlayRoot, instant);
        }

        private void RefreshInventoryPendingUi(int pendingItemCount)
        {
            int clampedPendingItemCount = Mathf.Max(0, pendingItemCount);

            if (_inventoryPendingBadgeRoot != null)
                _inventoryPendingBadgeRoot.SetActive(clampedPendingItemCount > 0);

            SetText(_inventoryPendingCountText, clampedPendingItemCount.ToString());
        }

        private string BuildContinueButtonLabel()
        {
            if (App.Game == null || App.Game.Config == null)
                return "CONTINUE";

            int continueCost = Mathf.Max(0, App.Game.Config.continueCost);
            return continueCost > 0 ? $"CONTINUE ({continueCost} CASH)" : "CONTINUE";
        }

        private static string FormatPendingItems(GameStateSnapshot state)
        {
            return state.PendingInventoryRewardKinds > 0
                ? $"{state.PendingInventoryRewardCount} ({state.PendingInventoryRewardKinds} kinds)"
                : "0";
        }

        private static string BuildStatusLabel(GameStateSnapshot state)
        {
            switch (state.Phase)
            {
                case GameRunPhase.AwaitingSpin:
                    return state.CanCashOut
                        ? "Spin again or cash out."
                        : "Spin to continue the run.";

                case GameRunPhase.Spinning:
                    return "Wheel spinning...";

                case GameRunPhase.Busted:
                    return state.CanContinue
                        ? "Bomb hit. Continue or restart."
                        : "Bomb hit. Restart to begin a new run.";

                case GameRunPhase.CashedOut:
                    return "Rewards banked. Restart for a new run.";

                case GameRunPhase.Completed:
                    return "Run complete. Rewards banked.";

                case GameRunPhase.BlockedByBuyIn:
                    return "Not enough cash for the buy-in.";

                default:
                    return "Waiting for scene bootstrap.";
            }
        }

        private static string BuildSpinResultLabel(RouletteSpinResult spinResult)
        {
            if (spinResult.SelectedSlice.SliceRule == null)
                return "-";

            if (spinResult.WasBomb)
                return "Bomb";

            if (!spinResult.SelectedSlice.Reward.HasReward)
                return spinResult.SelectedSlice.DisplayName;

            return $"{spinResult.SelectedSlice.Reward.RewardName} {spinResult.SelectedSlice.Reward.FormatAmountLabel()}";
        }

        private static string BuildPhaseLabel(GameStateSnapshot state)
        {
            if (state.Phase == GameRunPhase.AwaitingSpin)
            {
                return state.CurrentZoneType switch
                {
                    RouletteZoneType.Safe => "SAFE ZONE",
                    RouletteZoneType.Super => "SUPER ZONE",
                    _ => "Awaiting Spin"
                };
            }

            return state.Phase switch
            {
                GameRunPhase.BlockedByBuyIn => "Buy-In Blocked",
                GameRunPhase.CashedOut => "Cashed Out",
                _ => state.Phase.ToString()
            };
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

        private bool ResolveContinueButtonVisibility(bool canContinue, RectTransform desiredOverlayRoot, bool instant)
        {
            if (_continueButton == null)
                return false;

            if (desiredOverlayRoot == _gameOverRoot)
                return canContinue;

            if (instant || GetOverlayRootState(_gameOverRoot) == OverlayRootState.Hidden)
                return false;

            // Preserve the current game-over layout while the overlay is still animating out.
            return _continueButton.gameObject.activeSelf;
        }

        private void InitializeSlidingRoot(RectTransform root)
        {
            if (root == null)
                return;

            CacheShownOverlayAnchoredPosition(root);
            _overlayRootStates[root] = OverlayRootState.Hidden;
            root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);
            root.gameObject.SetActive(false);
        }

        private void InitializeCompanionRoots(RectTransform[] roots)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
            {
                RectTransform root = roots[i];

                if (root == _gameOverRoot || root == _cashOutOverlayRoot)
                    continue;

                if (root == null)
                    continue;

                _companionRootBaseAnchoredPositions[root] = root.anchoredPosition;
            }
        }

        private void UpdateOverlaySlot(RectTransform desiredRoot, bool instant)
        {
            _desiredOverlayRoot = desiredRoot;

            if (instant)
            {
                SetOverlayRootVisibleImmediate(_gameOverRoot, desiredRoot == _gameOverRoot);
                SetOverlayRootVisibleImmediate(_cashOutOverlayRoot, desiredRoot == _cashOutOverlayRoot);
                return;
            }

            RectTransform occupyingRoot = GetOccupyingOverlayRoot();

            if (occupyingRoot != null && occupyingRoot != desiredRoot)
            {
                HideOverlayRoot(occupyingRoot);
                return;
            }

            if (desiredRoot == null)
                return;

            ShowOverlayRoot(desiredRoot);
        }

        private RectTransform GetOccupyingOverlayRoot()
        {
            if (IsOverlayRootOccupyingSlot(_gameOverRoot))
                return _gameOverRoot;

            if (IsOverlayRootOccupyingSlot(_cashOutOverlayRoot))
                return _cashOutOverlayRoot;

            return null;
        }

        private bool IsOverlayRootOccupyingSlot(RectTransform root)
        {
            return root != null && GetOverlayRootState(root) != OverlayRootState.Hidden;
        }

        private OverlayRootState GetOverlayRootState(RectTransform root)
        {
            return root != null && _overlayRootStates.TryGetValue(root, out OverlayRootState state)
                ? state
                : OverlayRootState.Hidden;
        }

        private void SetOverlayRootState(RectTransform root, OverlayRootState state)
        {
            if (root != null)
                _overlayRootStates[root] = state;
        }

        private void SetOverlayRootVisibleImmediate(RectTransform root, bool isVisible)
        {
            if (root == null)
                return;

            KillOverlayTween(root);

            if (isVisible)
            {
                root.gameObject.SetActive(true);
                root.anchoredPosition = GetShownOverlayAnchoredPosition(root);
                SetOverlayRootState(root, OverlayRootState.Visible);
                return;
            }

            root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);
            root.gameObject.SetActive(false);
            SetOverlayRootState(root, OverlayRootState.Hidden);
        }

        private void ShowOverlayRoot(RectTransform root)
        {
            if (root == null)
                return;

            OverlayRootState state = GetOverlayRootState(root);

            if (state == OverlayRootState.Visible || state == OverlayRootState.Showing)
                return;

            KillOverlayTween(root);
            root.gameObject.SetActive(true);

            if (state == OverlayRootState.Hidden)
                root.anchoredPosition = GetHiddenOverlayAnchoredPosition(root);

            SetOverlayRootState(root, OverlayRootState.Showing);
            Tween showTween = root.DOAnchorPos(GetShownOverlayAnchoredPosition(root), _gameOverSlideDuration)
                .SetEase(_gameOverSlideEase)
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => SetOverlayRootState(root, OverlayRootState.Visible))
                .OnKill(() => _overlayTweens.Remove(root));
            _overlayTweens[root] = showTween;
        }

        private void HideOverlayRoot(RectTransform root)
        {
            if (root == null)
                return;

            OverlayRootState state = GetOverlayRootState(root);

            if (state == OverlayRootState.Hidden || state == OverlayRootState.Hiding)
                return;

            KillOverlayTween(root);
            SetOverlayRootState(root, OverlayRootState.Hiding);

            Tween hideTween = root.DOAnchorPos(GetHiddenOverlayAnchoredPosition(root), _gameOverSlideDuration)
                .SetEase(_gameOverHideEase)
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    root.gameObject.SetActive(false);
                    SetOverlayRootState(root, OverlayRootState.Hidden);

                    if (_desiredOverlayRoot != null && _desiredOverlayRoot != root && GetOccupyingOverlayRoot() == null)
                        ShowOverlayRoot(_desiredOverlayRoot);
                })
                .OnKill(() => _overlayTweens.Remove(root));
            _overlayTweens[root] = hideTween;
        }

        private void UpdateCompanionRoots(bool shiftUp, RectTransform referenceOverlayRoot, bool instant)
        {
            if (_overlaySlideCompanionRoots == null)
                return;

            Vector2 offset = shiftUp ? GetOverlaySlideOffset(referenceOverlayRoot) : Vector2.zero;

            for (int i = 0; i < _overlaySlideCompanionRoots.Length; i++)
            {
                RectTransform root = _overlaySlideCompanionRoots[i];

                if (root == null)
                    continue;

                if (!_companionRootBaseAnchoredPositions.TryGetValue(root, out Vector2 baseAnchoredPosition))
                {
                    baseAnchoredPosition = root.anchoredPosition;
                    _companionRootBaseAnchoredPositions[root] = baseAnchoredPosition;
                }

                Vector2 targetAnchoredPosition = baseAnchoredPosition + offset;

                KillOverlayTween(root);

                if (instant)
                {
                    root.anchoredPosition = targetAnchoredPosition;
                    continue;
                }

                if (root.anchoredPosition == targetAnchoredPosition)
                    continue;

                Tween companionTween = root.DOAnchorPos(targetAnchoredPosition, _gameOverSlideDuration)
                    .SetEase(shiftUp ? _gameOverSlideEase : _gameOverHideEase)
                    .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnKill(() => _overlayTweens.Remove(root));
                _overlayTweens[root] = companionTween;
            }
        }

        private Vector2 GetOverlaySlideOffset(RectTransform root)
        {
            return root == null
                ? Vector2.zero
                : GetShownOverlayAnchoredPosition(root) - GetHiddenOverlayAnchoredPosition(root);
        }

        private void KillOverlayTweens()
        {
            KillOverlayTween(_gameOverRoot);
            KillOverlayTween(_cashOutOverlayRoot);

            if (_overlaySlideCompanionRoots == null)
                return;

            for (int i = 0; i < _overlaySlideCompanionRoots.Length; i++)
                KillOverlayTween(_overlaySlideCompanionRoots[i]);
        }

        private void KillOverlayTween(RectTransform root)
        {
            if (root == null)
                return;

            if (_overlayTweens.TryGetValue(root, out Tween overlayTween) && overlayTween != null && overlayTween.IsActive())
                overlayTween.Kill();

            _overlayTweens.Remove(root);
        }

        private void CacheShownOverlayAnchoredPosition(RectTransform root)
        {
            if (root == null)
                return;

            _overlayShownAnchoredPositions[root] = ResolveShownOverlayAnchoredPosition(root);
        }

        private Vector2 GetShownOverlayAnchoredPosition(RectTransform root)
        {
            if (root == null)
                return Vector2.zero;

            if (_overlayShownPositionSource != null)
                return _overlayShownPositionSource.anchoredPosition;

            if (_overlayShownAnchoredPositions.TryGetValue(root, out Vector2 shownAnchoredPosition))
                return shownAnchoredPosition;

            shownAnchoredPosition = root.anchoredPosition;
            _overlayShownAnchoredPositions[root] = shownAnchoredPosition;
            return shownAnchoredPosition;
        }

        private Vector2 ResolveShownOverlayAnchoredPosition(RectTransform root)
        {
            return _overlayShownPositionSource != null
                ? _overlayShownPositionSource.anchoredPosition
                : root.anchoredPosition;
        }

        private Vector2 GetHiddenOverlayAnchoredPosition(RectTransform root)
        {
            return GetShownOverlayAnchoredPosition(root) + Vector2.down * root.rect.height;
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
                button.interactable = isInteractable;
        }

        private static bool IsBeforeFirstSpinOfRun()
        {
            return App.Game != null && App.Game.LastSpinResult.SelectedSlice.SliceRule == null;
        }

        private static void SetWheelIdlePresentationState(bool isSpinButtonIdleActive, bool isWheelIdleRotationActive)
        {
            if (App.Game == null)
                return;

            RouletteWheelUI rouletteWheel = App.Game.SceneDependencies.RouletteWheel;
            if (rouletteWheel != null)
                rouletteWheel.SetIdlePresentationActive(isSpinButtonIdleActive, isSpinButtonIdleActive && isWheelIdleRotationActive);
        }

        private static void SetButtonVisible(Button button, bool isVisible)
        {
            if (button != null)
                button.gameObject.SetActive(isVisible);
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
