using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    public sealed class RunRewardLedger
    {
        private readonly List<ResolvedReward> _inventoryRewards = new List<ResolvedReward>();

        public IReadOnlyList<ResolvedReward> InventoryRewards => _inventoryRewards;
        public int PendingCash { get; private set; }
        public int PendingGold { get; private set; }
        public int PendingInventoryRewardKinds => _inventoryRewards.Count;

        public int PendingInventoryRewardCount
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _inventoryRewards.Count; i++)
                    total += _inventoryRewards[i].Amount;

                return total;
            }
        }

        public void Clear()
        {
            PendingCash = 0;
            PendingGold = 0;
            _inventoryRewards.Clear();
        }

        public void AddReward(ResolvedReward reward)
        {
            switch (reward.RewardKind)
            {
                case RewardType.Cash:
                    PendingCash += reward.Amount;
                    return;

                case RewardType.Gold:
                    PendingGold += reward.Amount;
                    return;
            }

            for (int i = 0; i < _inventoryRewards.Count; i++)
            {
                if (_inventoryRewards[i].RewardId != reward.RewardId)
                    continue;

                _inventoryRewards[i] = _inventoryRewards[i].WithAmount(_inventoryRewards[i].Amount + reward.Amount);
                return;
            }

            _inventoryRewards.Add(reward);
        }

        public void Restore(int pendingCash, int pendingGold, IReadOnlyList<ResolvedReward> inventoryRewards)
        {
            PendingCash = pendingCash;
            PendingGold = pendingGold;
            _inventoryRewards.Clear();

            if (inventoryRewards == null)
                return;

            for (int i = 0; i < inventoryRewards.Count; i++)
                _inventoryRewards.Add(inventoryRewards[i]);
        }
    }
}
