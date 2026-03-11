using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    public sealed class InventoryManager
    {
        public CaseManager Cases { get; } = new CaseManager();

        public void Initialize(RewardManager rewardManager)
        {
            Cases.Initialize(rewardManager);
        }

        public void ResetState()
        {
            Cases.ResetState();
        }

        public void Shutdown()
        {
            Cases.Shutdown();
        }

        public void GetPendingRewards(List<InventoryRewardEntry> destination)
        {
            destination?.Clear();

            if (destination == null || App.Game == null)
                return;

            IReadOnlyList<ResolvedReward> rewards = App.Game.PendingInventoryRewards;
            if (rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                ResolvedReward reward = rewards[i];
                if (!reward.HasReward || !reward.IsInventoryReward || reward.Amount <= 0)
                    continue;

                destination.Add(new InventoryRewardEntry(reward, InventoryRewardAction.None));
            }

            destination.Sort(CompareRewards);
        }

        public void GetBankedRewards(List<InventoryRewardEntry> destination)
        {
            destination?.Clear();

            if (destination == null || App.Profile == null)
                return;

            GameConfig gameConfig = App.Config != null ? App.Config.GameConfig : null;
            IReadOnlyList<RewardInventoryEntry> inventory = App.Profile.Inventory;

            if (gameConfig == null || inventory == null)
                return;

            bool casePresentationActive = Cases.IsPresentationActive;

            for (int i = 0; i < inventory.Count; i++)
            {
                RewardInventoryEntry entry = inventory[i];
                if (entry.Amount <= 0)
                    continue;

                if (!gameConfig.TryGetReward(entry.RewardId, out RewardData rewardData) || rewardData == null)
                {
                    Debug.LogWarning($"Reward inventory entry '{entry.RewardId}' could not be resolved from the reward catalog.");
                    continue;
                }

                if (rewardData.Kind == RewardType.Cash || rewardData.Kind == RewardType.Gold)
                    continue;

                InventoryRewardAction action = !casePresentationActive && rewardData.Kind == RewardType.Case
                    ? InventoryRewardAction.OpenCase
                    : InventoryRewardAction.None;

                destination.Add(new InventoryRewardEntry(new ResolvedReward(rewardData, entry.Amount), action));
            }

            destination.Sort(CompareRewards);
        }

        private static int CompareRewards(InventoryRewardEntry left, InventoryRewardEntry right)
        {
            int rarityComparison = right.Rarity.CompareTo(left.Rarity);
            if (rarityComparison != 0)
                return rarityComparison;

            int nameComparison = string.Compare(left.RewardName, right.RewardName, System.StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return right.Amount.CompareTo(left.Amount);
        }
    }
}
