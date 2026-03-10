using System;
using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RewardManager
    {
        private int _caseOpenCounter;

        private GameConfig Config => App.Config != null ? App.Config.GameConfig : null;

        public void ResetState()
        {
            _caseOpenCounter = 0;
        }

        public bool TryResolveReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;
            return Config != null && Config.TryGetReward(rewardId, out rewardData);
        }

        public void GrantReward(ResolvedReward reward, bool saveImmediately = true)
        {
            if (reward.RewardData == null || reward.Amount <= 0)
                return;

            EnsureProfile();

            switch (reward.RewardKind)
            {
                case RewardData.RewardKind.Cash:
                    App.Profile.AddCash(reward.Amount, saveImmediately);
                    return;

                case RewardData.RewardKind.Gold:
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
                    if (reward.RewardData == null || !reward.IsInventoryReward || reward.Amount <= 0)
                        continue;

                    App.Profile.AddInventoryReward(reward.RewardId, reward.Amount, saveImmediately: false);
                    hasChanges = true;
                }
            }

            if (hasChanges && saveImmediately)
                App.Profile.Save();
        }

        public bool TryOpenCase(string caseRewardId, out CaseOpenResult caseOpenResult)
        {
            caseOpenResult = default;

            if (string.IsNullOrWhiteSpace(caseRewardId) || Config == null)
                return false;

            if (!Config.TryGetCaseDefinition(caseRewardId, out CaseRewardsConfig.CaseDefinition caseDefinition))
                return false;

            return TryOpenCase(caseDefinition.CaseReward, out caseOpenResult);
        }

        public bool TryOpenCase(RewardData caseReward, out CaseOpenResult caseOpenResult)
        {
            caseOpenResult = default;

            if (caseReward == null || caseReward.Kind != RewardData.RewardKind.Case || Config == null)
                return false;

            EnsureProfile();

            if (!Config.TryGetCaseDefinition(caseReward.RewardId, out CaseRewardsConfig.CaseDefinition caseDefinition))
                return false;

            if (!TryBuildCaseOpenResult(caseDefinition, out caseOpenResult))
                return false;

            if (!App.Profile.TrySpendInventoryReward(caseReward.RewardId, 1, saveImmediately: false))
                return false;

            GrantReward(caseOpenResult.GrantedReward, saveImmediately: false);
            App.Profile.Save();
            return true;
        }

        private bool TryBuildCaseOpenResult(CaseRewardsConfig.CaseDefinition caseDefinition, out CaseOpenResult caseOpenResult)
        {
            caseOpenResult = default;

            if (caseDefinition.CaseReward == null
                || caseDefinition.CaseReward.Kind != RewardData.RewardKind.Case
                || caseDefinition.PossibleRewards == null)
                return false;

            List<CaseRewardPoolConfig.Entry> weightedEntries = CollectWeightedEntries(caseDefinition.PossibleRewards);
            if (weightedEntries.Count == 0)
                return false;

            System.Random random = CreateCaseRandom(caseDefinition.CaseRewardId);
            ResolvedReward grantedReward = ResolveWeightedReward(weightedEntries, random);
            if (grantedReward.RewardData == null || grantedReward.Amount <= 0)
                return false;

            int reelItemCount = caseDefinition.ResolveReelItemCount();
            int landingTailCount = Mathf.Min(reelItemCount - 1, caseDefinition.ResolveLandingTailCount());
            int maxWinningIndex = Mathf.Max(0, reelItemCount - landingTailCount - 1);
            int defaultMinimumWinningIndex = Mathf.Min(reelItemCount / 2, maxWinningIndex);
            int minWinningIndex = Mathf.Clamp(caseDefinition.ResolveMinimumLandingIndex(), 0, maxWinningIndex);
            minWinningIndex = Mathf.Max(minWinningIndex, defaultMinimumWinningIndex);
            int winningIndex = minWinningIndex >= maxWinningIndex
                ? maxWinningIndex
                : random.Next(minWinningIndex, maxWinningIndex + 1);

            List<ResolvedReward> reelRewards = BuildReelRewards(weightedEntries, grantedReward, winningIndex, reelItemCount, random);
            caseOpenResult = new CaseOpenResult(caseDefinition, grantedReward, reelRewards, winningIndex);
            return caseOpenResult.IsValid;
        }

        private static List<CaseRewardPoolConfig.Entry> CollectWeightedEntries(CaseRewardPoolConfig rewardPool)
        {
            List<CaseRewardPoolConfig.Entry> entries = new List<CaseRewardPoolConfig.Entry>();
            CaseRewardPoolConfig.Entry[] sourceEntries = rewardPool.Rewards;

            for (int i = 0; i < sourceEntries.Length; i++)
            {
                if (sourceEntries[i].rewardData == null || sourceEntries[i].weight <= 0f)
                    continue;

                entries.Add(sourceEntries[i]);
            }

            return entries;
        }

        private static List<ResolvedReward> BuildReelRewards(
            IReadOnlyList<CaseRewardPoolConfig.Entry> weightedEntries,
            ResolvedReward winningReward,
            int winningIndex,
            int reelItemCount,
            System.Random random)
        {
            List<ResolvedReward> reelRewards = new List<ResolvedReward>(reelItemCount);

            for (int i = 0; i < reelItemCount; i++)
            {
                if (i == winningIndex)
                {
                    reelRewards.Add(winningReward);
                    continue;
                }

                string excludedRewardId = weightedEntries.Count > 1 && Mathf.Abs(i - winningIndex) <= 1
                    ? winningReward.RewardId
                    : null;

                reelRewards.Add(ResolveWeightedReward(weightedEntries, random, excludedRewardId));
            }

            return reelRewards;
        }

        private static ResolvedReward ResolveWeightedReward(
            IReadOnlyList<CaseRewardPoolConfig.Entry> weightedEntries,
            System.Random random,
            string excludedRewardId = null)
        {
            float totalWeight = 0f;

            for (int i = 0; i < weightedEntries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(excludedRewardId)
                    && weightedEntries[i].rewardData != null
                    && weightedEntries[i].rewardData.RewardId == excludedRewardId)
                    continue;

                totalWeight += weightedEntries[i].weight;
            }

            if (totalWeight <= 0f)
            {
                for (int i = 0; i < weightedEntries.Count; i++)
                {
                    RewardData fallbackReward = weightedEntries[i].rewardData;
                    if (fallbackReward == null)
                        continue;

                    return new ResolvedReward(fallbackReward, weightedEntries[i].ResolveAmount(random));
                }

                return default;
            }

            double roll = random.NextDouble() * totalWeight;
            float cumulativeWeight = 0f;

            for (int i = 0; i < weightedEntries.Count; i++)
            {
                RewardData rewardData = weightedEntries[i].rewardData;
                if (rewardData == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(excludedRewardId) && rewardData.RewardId == excludedRewardId)
                    continue;

                cumulativeWeight += weightedEntries[i].weight;
                if (roll > cumulativeWeight)
                    continue;

                return new ResolvedReward(rewardData, weightedEntries[i].ResolveAmount(random));
            }

            for (int i = weightedEntries.Count - 1; i >= 0; i--)
            {
                RewardData rewardData = weightedEntries[i].rewardData;
                if (rewardData == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(excludedRewardId) && rewardData.RewardId == excludedRewardId)
                    continue;

                return new ResolvedReward(rewardData, weightedEntries[i].ResolveAmount(random));
            }

            return ResolveWeightedReward(weightedEntries, random, excludedRewardId: null);
        }

        private System.Random CreateCaseRandom(string rewardId)
        {
            _caseOpenCounter++;
            int seed = unchecked((Environment.TickCount * 397) ^ (_caseOpenCounter * 486187739) ^ rewardId.GetHashCode());
            return new System.Random(seed);
        }

        private static void EnsureProfile()
        {
            if (App.Profile == null)
                throw new InvalidOperationException("RewardManager requires ProfileManager before rewards can be granted.");
        }
    }
}
