using Ape.Data;

namespace Ape.Game
{
    public readonly struct ResolvedReward
    {
        public RewardData RewardData { get; }
        public int Amount { get; }

        public ResolvedReward(RewardData rewardData, int amount)
        {
            RewardData = rewardData;
            Amount = amount;
        }

        public string RewardId => RewardData != null ? RewardData.RewardId : string.Empty;
        public string RewardName => RewardData != null ? RewardData.RewardName : string.Empty;
        public RewardData.RewardKind RewardKind => RewardData != null ? RewardData.Kind : RewardData.RewardKind.ItemCard;
        public RewardData.RewardRarity Rarity => RewardData != null ? RewardData.Rarity : RewardData.RewardRarity.Common;
        public bool IsCurrency => RewardKind == RewardData.RewardKind.Cash || RewardKind == RewardData.RewardKind.Gold;
        public bool IsInventoryReward => !IsCurrency;

        public ResolvedReward WithAmount(int amount)
        {
            return new ResolvedReward(RewardData, amount);
        }

        public string FormatAmountLabel()
        {
            return IsCurrency ? $"+{Amount}" : $"x{Amount}";
        }
    }
}
