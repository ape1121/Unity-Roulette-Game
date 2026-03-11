using System.Collections.Generic;

namespace Ape.Game
{
    public sealed class ContinueRewardSnapshot
    {
        private readonly List<ResolvedReward> _inventoryRewards = new List<ResolvedReward>();

        public int Zone { get; private set; }
        public int PendingCash { get; private set; }
        public int PendingGold { get; private set; }
        public IReadOnlyList<ResolvedReward> InventoryRewards => _inventoryRewards;
        public bool HasValue => Zone > 0;

        public void Capture(int zone, RunRewardLedger ledger)
        {
            Zone = zone;
            PendingCash = ledger != null ? ledger.PendingCash : 0;
            PendingGold = ledger != null ? ledger.PendingGold : 0;
            _inventoryRewards.Clear();

            if (ledger == null || ledger.InventoryRewards == null)
                return;

            for (int i = 0; i < ledger.InventoryRewards.Count; i++)
                _inventoryRewards.Add(ledger.InventoryRewards[i]);
        }

        public void Clear()
        {
            Zone = 0;
            PendingCash = 0;
            PendingGold = 0;
            _inventoryRewards.Clear();
        }
    }
}
