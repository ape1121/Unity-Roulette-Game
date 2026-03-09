using System;

namespace Ape.Profile
{
    [Serializable]
    public struct RewardInventoryEntry
    {
        public string RewardId;
        public int Amount;

        public RewardInventoryEntry(string rewardId, int amount)
        {
            RewardId = rewardId;
            Amount = amount;
        }
    }
}
