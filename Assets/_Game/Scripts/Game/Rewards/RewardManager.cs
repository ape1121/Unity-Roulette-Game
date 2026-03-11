using System;
using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RewardManager : IManager
    {
        private readonly Dictionary<RarityType, RarityData> _rarityDataCache = new Dictionary<RarityType, RarityData>();
        private GameConfig _config;
        private ProfileManager _profile;

        private GameConfig Config => _config;

        public void Configure(GameConfig config, ProfileManager profile)
        {
            _config = config;
            _profile = profile;
        }

        public void Initialize()
        {
            RebuildRarityCache();
        }

        private void RebuildRarityCache()
        {
            _rarityDataCache.Clear();
            if (Config == null)
                return;
            var rarities = Config.RarityCollection?.Rarities;
            if (rarities == null)
                return;
            for (int i = 0; i < rarities.Length; i++)
            {
                RarityData rarityData = rarities[i];
                _rarityDataCache[rarityData.Rarity] = rarityData;
            }
        }

        public RarityData GetRarityData(RarityType rarity)
        {
            return _rarityDataCache.TryGetValue(rarity, out var data) ? data : default;
        }

        public Color GetRarityColor(RarityType rarity, Color fallback)
        {
            return _rarityDataCache.TryGetValue(rarity, out var data) ? data.Color : fallback;
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
                    _profile.AddCash(reward.Amount, saveImmediately);
                    return;

                case RewardType.Gold:
                    _profile.AddGold(reward.Amount, saveImmediately);
                    return;

                default:
                    _profile.AddInventoryReward(reward.RewardId, reward.Amount, saveImmediately);
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
                _profile.AddCash(resolvedCash, saveImmediately: false);
                hasChanges = true;
            }

            int resolvedGold = Mathf.Max(0, gold);
            if (resolvedGold > 0)
            {
                _profile.AddGold(resolvedGold, saveImmediately: false);
                hasChanges = true;
            }

            if (inventoryRewards != null)
            {
                for (int i = 0; i < inventoryRewards.Count; i++)
                {
                    ResolvedReward reward = inventoryRewards[i];
                    if (!reward.HasReward || !reward.IsInventoryReward || reward.Amount <= 0)
                        continue;

                    _profile.AddInventoryReward(reward.RewardId, reward.Amount, saveImmediately: false);
                    hasChanges = true;
                }
            }

            if (hasChanges && saveImmediately)
                _profile.Save();
        }

        private void EnsureProfile()
        {
            if (_profile == null)
                throw new InvalidOperationException("RewardManager requires ProfileManager before rewards can be granted.");
        }
    }
}
