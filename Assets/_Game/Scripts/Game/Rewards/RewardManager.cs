using System;
using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RewardManager : IManager
    {
        private GameConfig Config => App.Config != null ? App.Config.GameConfig : null;
        private Dictionary<RarityType, RarityData> rarityDataCache = new Dictionary<RarityType, RarityData>();

        public void Initialize()
        {
            RebuildRarityCache();
        }

        private void RebuildRarityCache()
        {
            rarityDataCache.Clear();
            if (Config == null)
                return;
            var rarities = Config.RarityCollection?.Rarities;
            if (rarities == null)
                return;
            for (int i = 0; i < rarities.Length; i++)
            {
                RarityData rarityData = rarities[i];
                rarityDataCache.Add(rarityData.Rarity, rarityData);
            }
        }

        public RarityData GetRarityData(RarityType rarity)
        {
            return rarityDataCache.TryGetValue(rarity, out var data) ? data : default;
        }

        public Color GetRarityColor(RarityType rarity, Color fallback)
        {
            return rarityDataCache.TryGetValue(rarity, out var data) ? data.Color : fallback;
        }

        public void ResetState()
        {
        }

        public bool TryResolveReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;
            return Config != null && Config.TryGetReward(rewardId, out rewardData);
        }

        public void GrantReward(ResolvedReward reward, bool saveImmediately = true)
        {
            if (!reward.HasReward || reward.Amount <= 0)
                return;

            EnsureProfile();

            switch (reward.RewardKind)
            {
                case RewardType.Cash:
                    App.Profile.AddCash(reward.Amount, saveImmediately);
                    return;

                case RewardType.Gold:
                    App.Profile.AddGold(reward.Amount, saveImmediately);
                    return;

                default:
                    App.Profile.AddInventoryReward(reward.RewardId, reward.Amount, saveImmediately);
                    return;
            }
        }

        public void GrantRewards(int cash, int gold, IReadOnlyList<ResolvedReward> inventoryRewards, bool saveImmediately = true)
        {
            EnsureProfile();
            bool hasChanges = false;

            int resolvedCash = Mathf.Max(0, cash);
            if (resolvedCash > 0)
            {
                App.Profile.AddCash(resolvedCash, saveImmediately: false);
                hasChanges = true;
            }

            int resolvedGold = Mathf.Max(0, gold);
            if (resolvedGold > 0)
            {
                App.Profile.AddGold(resolvedGold, saveImmediately: false);
                hasChanges = true;
            }

            if (inventoryRewards != null)
            {
                for (int i = 0; i < inventoryRewards.Count; i++)
                {
                    ResolvedReward reward = inventoryRewards[i];
                    if (!reward.HasReward || !reward.IsInventoryReward || reward.Amount <= 0)
                        continue;

                    App.Profile.AddInventoryReward(reward.RewardId, reward.Amount, saveImmediately: false);
                    hasChanges = true;
                }
            }

            if (hasChanges && saveImmediately)
                App.Profile.Save();
        }

        private static void EnsureProfile()
        {
            if (App.Profile == null)
                throw new InvalidOperationException("RewardManager requires ProfileManager before rewards can be granted.");
        }
    }
}
