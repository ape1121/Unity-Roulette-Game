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
        public RewardType RewardKind => RewardData != null ? RewardData.Kind : RewardType.ItemCard;
        public RarityType Rarity => RewardData != null ? RewardData.Rarity : RarityType.Common;
        public bool IsCurrency => RewardKind == RewardType.Cash || RewardKind == RewardType.Gold;
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
