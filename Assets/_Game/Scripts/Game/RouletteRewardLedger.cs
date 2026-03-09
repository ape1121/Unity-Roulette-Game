using System.Collections.Generic;
using Ape.Data;
using Ape.Profile;

namespace Ape.Game
{
    public sealed class RouletteRewardLedger
    {
        private readonly List<ResolvedReward> _itemRewards = new List<ResolvedReward>();

        public IReadOnlyList<ResolvedReward> ItemRewards => _itemRewards;
        public int PendingCash { get; private set; }
        public int PendingGold { get; private set; }

        public int PendingItemCardCount
        {
            get
            {
                int total = 0;

                for (int i = 0; i < _itemRewards.Count; i++)
                    total += _itemRewards[i].Amount;

                return total;
            }
        }

        public void Clear()
        {
            PendingCash = 0;
            PendingGold = 0;
            _itemRewards.Clear();
        }

        public void AddReward(ResolvedReward reward)
        {
            switch (reward.RewardKind)
            {
                case RewardData.RewardKind.Cash:
                    PendingCash += reward.Amount;
                    return;

                case RewardData.RewardKind.Gold:
                    PendingGold += reward.Amount;
                    return;
            }

            for (int i = 0; i < _itemRewards.Count; i++)
            {
                if (_itemRewards[i].RewardId != reward.RewardId)
                    continue;

                _itemRewards[i] = _itemRewards[i].WithAmount(_itemRewards[i].Amount + reward.Amount);
                return;
            }

            _itemRewards.Add(reward);
        }

        public List<RewardInventoryEntry> CreateInventorySnapshot()
        {
            List<RewardInventoryEntry> snapshot = new List<RewardInventoryEntry>(_itemRewards.Count);

            for (int i = 0; i < _itemRewards.Count; i++)
                snapshot.Add(new RewardInventoryEntry(_itemRewards[i].RewardId, _itemRewards[i].Amount));

            return snapshot;
        }

        public void Restore(int pendingCash, int pendingGold, IReadOnlyList<ResolvedReward> itemRewards)
        {
            PendingCash = pendingCash;
            PendingGold = pendingGold;
            _itemRewards.Clear();

            if (itemRewards == null)
                return;

            for (int i = 0; i < itemRewards.Count; i++)
                _itemRewards.Add(itemRewards[i]);
        }
    }
}
