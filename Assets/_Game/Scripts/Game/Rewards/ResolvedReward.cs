using Ape.Data;
using UnityEngine;

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

        public bool HasReward => RewardData != null;
        public string RewardId => HasReward ? RewardData.RewardId : string.Empty;
        public string RewardName => HasReward ? RewardData.RewardName : string.Empty;
        public Sprite Icon => HasReward ? RewardData.Icon : null;
        public RewardType RewardKind => HasReward ? RewardData.Kind : RewardType.ItemCard;
        public RarityType Rarity => HasReward ? RewardData.Rarity : RarityType.Common;
        public bool IsCurrency => HasReward && (RewardKind == RewardType.Cash || RewardKind == RewardType.Gold);
        public bool IsInventoryReward => HasReward && !IsCurrency;

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
