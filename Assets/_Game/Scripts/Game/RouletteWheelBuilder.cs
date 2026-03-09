using System;
using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    public static class RouletteWheelBuilder
    {
        public static RouletteResolvedWheel BuildWheel(GameConfig config, int zone, System.Random random)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (random == null)
                throw new ArgumentNullException(nameof(random));

            GameConfig.ZoneType zoneType = config.GetZoneType(zone);
            RouletteWheelData wheelData = config.GetWheelData(zoneType);
            if (wheelData == null)
                throw new InvalidOperationException($"GameConfig is missing a wheel asset for zone type {zoneType}.");

            RouletteSliceData[] sliceDefinitions = wheelData.SliceDefinitions;
            if (sliceDefinitions.Length == 0)
                throw new InvalidOperationException($"Roulette wheel asset '{wheelData.name}' has no slice definitions.");

            List<RouletteResolvedSlice> slices = new List<RouletteResolvedSlice>(sliceDefinitions.Length);
            HashSet<string> usedRewardIds = wheelData.AllowDuplicateRewards ? null : new HashSet<string>();

            for (int i = 0; i < sliceDefinitions.Length; i++)
            {
                RouletteSliceData sliceRule = sliceDefinitions[i];
                if (sliceRule == null)
                    throw new InvalidOperationException($"Roulette wheel asset '{wheelData.name}' contains a null slice rule.");

                if (sliceRule.IsBomb)
                {
                    slices.Add(new RouletteResolvedSlice(i, sliceRule, default));
                    continue;
                }

                RewardData rewardData = SelectReward(config.GetRewardCatalog(), sliceRule, usedRewardIds, random);
                if (rewardData == null)
                    throw new InvalidOperationException($"No rewards matched slice rule '{sliceRule.name}' while building wheel '{wheelData.name}'.");

                if (usedRewardIds != null)
                    usedRewardIds.Add(rewardData.RewardId);

                ResolvedReward resolvedReward = new ResolvedReward(
                    rewardData,
                    rewardData.ResolveAmount(zone, sliceRule.AmountMultiplier, sliceRule.FlatAmountBonus, random));

                slices.Add(new RouletteResolvedSlice(i, sliceRule, resolvedReward));
            }

            return new RouletteResolvedWheel(wheelData, zoneType, slices);
        }

        private static RewardData SelectReward(RewardData[] rewardCatalog, RouletteSliceData sliceRule, HashSet<string> usedRewardIds, System.Random random)
        {
            List<RewardData> matchingRewards = CollectMatchingRewards(rewardCatalog, sliceRule, usedRewardIds, allowDuplicateFallback: false);

            if (matchingRewards.Count == 0 && usedRewardIds != null)
                matchingRewards = CollectMatchingRewards(rewardCatalog, sliceRule, usedRewardIds, allowDuplicateFallback: true);

            if (matchingRewards.Count == 0)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < matchingRewards.Count; i++)
                totalWeight += sliceRule.GetRarityWeight(matchingRewards[i].Rarity);

            if (totalWeight <= 0f)
                return matchingRewards[random.Next(matchingRewards.Count)];

            double roll = random.NextDouble() * totalWeight;
            float cumulativeWeight = 0f;

            for (int i = 0; i < matchingRewards.Count; i++)
            {
                cumulativeWeight += sliceRule.GetRarityWeight(matchingRewards[i].Rarity);
                if (roll <= cumulativeWeight)
                    return matchingRewards[i];
            }

            return matchingRewards[matchingRewards.Count - 1];
        }

        private static List<RewardData> CollectMatchingRewards(RewardData[] rewardCatalog, RouletteSliceData sliceRule, HashSet<string> usedRewardIds, bool allowDuplicateFallback)
        {
            List<RewardData> rewards = new List<RewardData>();

            for (int i = 0; i < rewardCatalog.Length; i++)
            {
                RewardData rewardData = rewardCatalog[i];
                if (rewardData == null || !sliceRule.MatchesReward(rewardData))
                    continue;

                if (!allowDuplicateFallback && usedRewardIds != null && usedRewardIds.Contains(rewardData.RewardId))
                    continue;

                rewards.Add(rewardData);
            }

            return rewards;
        }
    }
}
