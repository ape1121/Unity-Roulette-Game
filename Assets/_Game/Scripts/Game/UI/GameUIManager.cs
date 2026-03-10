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
        [Header("Action Buttons")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _cashOutButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _inventoryButton;

        [SerializeField] private TextMeshProUGUI _continueButtonLabel;
        [SerializeField] private RectTransform _gameOverRoot;
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
        [SerializeField] private GameObject _spinningBlockerRoot;

        private bool _gameSubscribed;
        private bool _profileSubscribed;
        private Tween _gameOverSlideTween;
        private Vector2 _gameOverAnchoredPos;

        private void OnEnable()
        {
            _inventoryWindow ??= GetComponentInChildren<InventoryUIWindow>(true);

            if (_gameOverRoot != null)
            {
                _gameOverAnchoredPos = _gameOverRoot.anchoredPosition;
                _gameOverRoot.gameObject.SetActive(false);
            }

            BindButtons();
            SubscribeToManagers();
            RefreshAll();
        }

        private void OnDisable()
        {
            KillGameOverTween();
            UnsubscribeFromManagers();
            UnbindButtons();
        }

        private void OnValidate()
        {
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

            SetText(_continueButtonLabel, BuildContinueButtonLabel());

            if (_spinningBlockerRoot != null)
                _spinningBlockerRoot.SetActive(state.Phase == GameRunPhase.Spinning);

            bool isGameOver = state.Phase == GameRunPhase.Busted
                              || state.Phase == GameRunPhase.CashedOut
                              || state.Phase == GameRunPhase.Completed;

            bool showContinueButton = !isGameOver || state.CanContinue;
            SetButtonVisible(_continueButton, showContinueButton);

            if (isGameOver)
                ShowGameOver(instant);
            else
                HideGameOver(instant);
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

            if (spinResult.SelectedSlice.Reward.RewardData == null)
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

        private void ShowGameOver(bool instant)
        {
            if (_gameOverRoot == null)
                return;

            KillGameOverTween();
            _gameOverRoot.gameObject.SetActive(true);

            if (instant)
            {
                _gameOverRoot.anchoredPosition = _gameOverAnchoredPos;
                return;
            }

            // Start off-screen below, slide up to cached position.
            _gameOverRoot.anchoredPosition = GetHiddenGameOverAnchoredPosition();
            _gameOverSlideTween = _gameOverRoot.DOAnchorPos(_gameOverAnchoredPos, _gameOverSlideDuration)
                .SetEase(_gameOverSlideEase)
                .SetLink(_gameOverRoot.gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _gameOverSlideTween = null);
        }

        private void HideGameOver(bool instant)
        {
            if (_gameOverRoot == null)
                return;

            KillGameOverTween();

            if (!_gameOverRoot.gameObject.activeSelf)
                return;

            if (instant)
            {
                _gameOverRoot.anchoredPosition = GetHiddenGameOverAnchoredPosition();
                _gameOverRoot.gameObject.SetActive(false);
                return;
            }

            _gameOverSlideTween = _gameOverRoot.DOAnchorPos(GetHiddenGameOverAnchoredPosition(), _gameOverSlideDuration)
                .SetEase(_gameOverHideEase)
                .SetLink(_gameOverRoot.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => _gameOverRoot.gameObject.SetActive(false))
                .OnKill(() => _gameOverSlideTween = null);
        }

        private void KillGameOverTween()
        {
            if (_gameOverSlideTween != null && _gameOverSlideTween.IsActive())
            {
                _gameOverSlideTween.Kill();
                _gameOverSlideTween = null;
            }
        }

        private Vector2 GetHiddenGameOverAnchoredPosition()
        {
            return _gameOverAnchoredPos + Vector2.down * _gameOverRoot.rect.height;
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
                button.interactable = isInteractable;
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
