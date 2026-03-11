using Ape.Data;
using TMPro;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameUIHudPresenter
    {
        private TextMeshProUGUI _zoneValueText;
        private TextMeshProUGUI _zoneTypeValueText;
        private TextMeshProUGUI _phaseValueText;
        private TextMeshProUGUI _statusValueText;
        private TextMeshProUGUI _pendingCashValueText;
        private TextMeshProUGUI _pendingGoldValueText;
        private TextMeshProUGUI _pendingItemsValueText;
        private TextMeshProUGUI _savedCashValueText;
        private TextMeshProUGUI _savedGoldValueText;
        private GameObject _inventoryPendingBadgeRoot;
        private TextMeshProUGUI _inventoryPendingCountText;

        public void Configure(
            TextMeshProUGUI zoneValueText,
            TextMeshProUGUI zoneTypeValueText,
            TextMeshProUGUI phaseValueText,
            TextMeshProUGUI statusValueText,
            TextMeshProUGUI pendingCashValueText,
            TextMeshProUGUI pendingGoldValueText,
            TextMeshProUGUI pendingItemsValueText,
            TextMeshProUGUI savedCashValueText,
            TextMeshProUGUI savedGoldValueText,
            GameObject inventoryPendingBadgeRoot,
            TextMeshProUGUI inventoryPendingCountText)
        {
            _zoneValueText = zoneValueText;
            _zoneTypeValueText = zoneTypeValueText;
            _phaseValueText = phaseValueText;
            _statusValueText = statusValueText;
            _pendingCashValueText = pendingCashValueText;
            _pendingGoldValueText = pendingGoldValueText;
            _pendingItemsValueText = pendingItemsValueText;
            _savedCashValueText = savedCashValueText;
            _savedGoldValueText = savedGoldValueText;
            _inventoryPendingBadgeRoot = inventoryPendingBadgeRoot;
            _inventoryPendingCountText = inventoryPendingCountText;
        }

        public void Refresh(GameStateSnapshot state)
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
        }

        private void RefreshInventoryPendingUi(int pendingItemCount)
        {
            int clampedPendingItemCount = Mathf.Max(0, pendingItemCount);

            if (_inventoryPendingBadgeRoot != null)
                _inventoryPendingBadgeRoot.SetActive(clampedPendingItemCount > 0);

            SetText(_inventoryPendingCountText, clampedPendingItemCount.ToString());
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

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
