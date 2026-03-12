using Ape.Data;

namespace Ape.Game
{
    public readonly struct InventoryRewardEntry
    {
        public InventoryRewardEntry(ResolvedReward reward, InventoryRewardAction action)
        {
            Reward = reward;
            Action = action;
        }

        public ResolvedReward Reward { get; }
        public InventoryRewardAction Action { get; }

        public bool HasReward => Reward.HasReward;
        public string RewardId => Reward.RewardId;
        public string RewardName => Reward.RewardName;
        public int Amount => Reward.Amount;
        public RarityType Rarity => Reward.Rarity;
        public RewardType RewardKind => Reward.RewardKind;
        public bool IsActionable => Action != InventoryRewardAction.None;
        public bool CanOpenCase => Action == InventoryRewardAction.OpenCase;
    }
}
