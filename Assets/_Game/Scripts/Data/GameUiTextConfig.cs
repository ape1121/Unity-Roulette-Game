using Ape.Game;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "GameUiTextConfig", menuName = "CriticalShot/Core/Game UI Text Config")]
    public sealed class GameUiTextConfig : ScriptableObject
    {
        [Header("General")]
        [SerializeField] private string _emptyValueLabel = "-";
        [SerializeField] private string _zoneLabelFormat = "FLOOR {0}";
        [SerializeField] private string _zeroPendingItemsLabel = "0";
        [SerializeField] private string _pendingItemsKindsFormat = "{0} ({1} kinds)";
        [SerializeField] private string _continueLabel = "CONTINUE";
        [SerializeField] private string _continueLabelWithCostFormat = "CONTINUE ({0} CASH)";
        [SerializeField] private string _pendingInventoryTabTitle = "PENDING";
        [SerializeField] private string _bankedInventoryTabTitle = "BANKED";
        [SerializeField] private string _caseWinnerLabelFormat = "{0} {1}";
        [SerializeField] private string _caseRollButtonLabel = "ROLL";
        [SerializeField] private string _caseBackButtonLabel = "BACK";
        [SerializeField] private string _caseTakeButtonLabel = "TAKE";
        [SerializeField] private string _caseFreeCostLabel = "OPEN COST: FREE";
        [SerializeField] private string _caseCashCostLabelFormat = "OPEN COST: {0} CASH";
        [SerializeField] private string _caseGoldCostLabelFormat = "OPEN COST: {0} GOLD";
        [SerializeField] private string _caseInventoryCostLabelFormat = "OPEN COST: {0} {1}";
        [SerializeField] private string _caseRollingLabel = "ROLLING...";

        [Header("Phase Labels")]
        [SerializeField] private string _awaitingSpinPhaseLabel = "Awaiting Spin";
        [SerializeField] private string _safeZonePhaseLabel = "SAFE ZONE";
        [SerializeField] private string _superZonePhaseLabel = "SUPER ZONE";
        [SerializeField] private string _blockedByBuyInPhaseLabel = "Buy-In Blocked";
        [SerializeField] private string _cashedOutPhaseLabel = "Cashed Out";

        [Header("Status Labels")]
        [SerializeField] private string _awaitingSpinCashOutStatusLabel = "Spin again or cash out.";
        [SerializeField] private string _awaitingSpinStatusLabel = "Spin to continue the run.";
        [SerializeField] private string _spinningStatusLabel = "Wheel spinning...";
        [SerializeField] private string _bustedContinueStatusLabel = "Bomb hit. Continue or restart.";
        [SerializeField] private string _bustedRestartStatusLabel = "Bomb hit. Restart to begin a new run.";
        [SerializeField] private string _cashedOutStatusLabel = "Rewards banked. Restart for a new run.";
        [SerializeField] private string _completedStatusLabel = "Run complete. Rewards banked.";
        [SerializeField] private string _blockedByBuyInStatusLabel = "Not enough cash for the buy-in.";
        [SerializeField] private string _waitingForSceneBootstrapStatusLabel = "Waiting for scene bootstrap.";

        public string PendingInventoryTabTitle => _pendingInventoryTabTitle;
        public string BankedInventoryTabTitle => _bankedInventoryTabTitle;
        public string CaseRollButtonLabel => _caseRollButtonLabel;
        public string CaseBackButtonLabel => _caseBackButtonLabel;
        public string CaseTakeButtonLabel => _caseTakeButtonLabel;
        public string CaseRollingLabel => _caseRollingLabel;

        public string FormatZoneLabel(int zone)
        {
            return zone > 0
                ? SafeFormat(_zoneLabelFormat, zone)
                : _emptyValueLabel;
        }

        public string FormatPendingItems(int pendingInventoryRewardCount, int pendingInventoryRewardKinds)
        {
            return pendingInventoryRewardKinds > 0
                ? SafeFormat(_pendingItemsKindsFormat, pendingInventoryRewardCount, pendingInventoryRewardKinds)
                : _zeroPendingItemsLabel;
        }

        public string FormatContinueLabel(int continueCost)
        {
            return continueCost > 0
                ? SafeFormat(_continueLabelWithCostFormat, continueCost)
                : _continueLabel;
        }

        public string FormatCaseWinnerLabel(ResolvedReward reward)
        {
            if (!reward.HasReward)
                return string.Empty;

            return SafeFormat(_caseWinnerLabelFormat, reward.RewardName, reward.FormatAmountLabel());
        }

        public string FormatCaseOpenCostLabel(ResolvedReward reward)
        {
            if (!reward.HasReward || reward.Amount <= 0)
                return _caseFreeCostLabel;

            return reward.RewardKind switch
            {
                RewardType.Cash => SafeFormat(_caseCashCostLabelFormat, reward.Amount),
                RewardType.Gold => SafeFormat(_caseGoldCostLabelFormat, reward.Amount),
                _ => SafeFormat(_caseInventoryCostLabelFormat, reward.Amount, reward.RewardName)
            };
        }

        public string GetStatusLabel(GameStateSnapshot state)
        {
            return state.Phase switch
            {
                GameRunPhase.AwaitingSpin => state.CanCashOut
                    ? _awaitingSpinCashOutStatusLabel
                    : _awaitingSpinStatusLabel,
                GameRunPhase.Spinning => _spinningStatusLabel,
                GameRunPhase.Busted => state.CanContinue
                    ? _bustedContinueStatusLabel
                    : _bustedRestartStatusLabel,
                GameRunPhase.CashedOut => _cashedOutStatusLabel,
                GameRunPhase.Completed => _completedStatusLabel,
                GameRunPhase.BlockedByBuyIn => _blockedByBuyInStatusLabel,
                _ => _waitingForSceneBootstrapStatusLabel
            };
        }

        public string GetPhaseLabel(GameStateSnapshot state)
        {
            if (state.Phase == GameRunPhase.AwaitingSpin)
            {
                return state.CurrentZoneType switch
                {
                    RouletteZoneType.Safe => _safeZonePhaseLabel,
                    RouletteZoneType.Super => _superZonePhaseLabel,
                    _ => _awaitingSpinPhaseLabel
                };
            }

            return state.Phase switch
            {
                GameRunPhase.BlockedByBuyIn => _blockedByBuyInPhaseLabel,
                GameRunPhase.CashedOut => _cashedOutPhaseLabel,
                _ => state.Phase.ToString()
            };
        }

        private static string SafeFormat(string template, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            return string.Format(template, args);
        }
    }
}
