using Ape.Data;
using TMPro;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameUIHudPresenter
    {
        private GameUiTextConfig _textConfig;
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
            TextMeshProUGUI inventoryPendingCountText,
            GameUiTextConfig textConfig)
        {
            _textConfig = textConfig;
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
            SetText(_zoneValueText, _textConfig != null ? _textConfig.FormatZoneLabel(state.CurrentZone) : state.CurrentZone.ToString());
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

        private string FormatPendingItems(GameStateSnapshot state)
        {
            return _textConfig != null
                ? _textConfig.FormatPendingItems(state.PendingInventoryRewardCount, state.PendingInventoryRewardKinds)
                : state.PendingInventoryRewardCount.ToString();
        }

        private string BuildStatusLabel(GameStateSnapshot state)
        {
            return _textConfig != null ? _textConfig.GetStatusLabel(state) : state.Phase.ToString();
        }

        private string BuildPhaseLabel(GameStateSnapshot state)
        {
            return _textConfig != null ? _textConfig.GetPhaseLabel(state) : state.Phase.ToString();
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
