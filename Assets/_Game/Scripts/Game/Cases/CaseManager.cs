using System;
using System.Collections.Generic;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    public sealed class CaseManager
    {
        private GameConfig _config;
        private ProfileManager _profile;
        private RewardManager _rewardManager;
        private int _caseOpenCounter;
        private int _nextSessionId;
        private CaseOpenSession _preparedSession;
        private bool _hasPreparedSession;

        private GameConfig Config => _config;

        public bool IsPresentationActive { get; private set; }

        public void Configure(GameConfig config, ProfileManager profile, RewardManager rewardManager)
        {
            _config = config;
            _profile = profile;
            _rewardManager = rewardManager;
        }

        public void Initialize()
        {
            ResetState();
        }

        public void ResetState()
        {
            _caseOpenCounter = 0;
            _nextSessionId = 0;
            _preparedSession = default;
            _hasPreparedSession = false;
            IsPresentationActive = false;
        }

        public void Shutdown()
        {
            ResetState();
            _rewardManager = null;
            _profile = null;
            _config = null;
        }

        public bool CanOpenCase(string caseRewardId)
        {
            if (IsPresentationActive
                || string.IsNullOrWhiteSpace(caseRewardId)
                || Config == null
                || _profile == null)
                return false;

            if (!Config.TryGetCaseDefinition(caseRewardId, out CaseDefinitionData caseDefinition))
                return false;

            if (caseDefinition.CaseReward == null || caseDefinition.CaseReward.Kind != RewardType.Case)
                return false;

            return _profile.GetInventoryRewardCount(caseDefinition.CaseRewardId) > 0;
        }

        public bool TryPrepareCaseOpen(string caseRewardId, out CaseOpenSession session)
        {
            session = default;

            if (IsPresentationActive
                || string.IsNullOrWhiteSpace(caseRewardId)
                || Config == null
                || _profile == null)
                return false;

            if (!Config.TryGetCaseDefinition(caseRewardId, out CaseDefinitionData caseDefinition)
                || caseDefinition == null
                || caseDefinition.CaseReward == null
                || caseDefinition.CaseReward.Kind != RewardType.Case
                || caseDefinition.PossibleRewards == null
                || _profile.GetInventoryRewardCount(caseDefinition.CaseRewardId) <= 0)
                return false;

            List<CaseRewardPoolConfig.Entry> weightedEntries = CollectWeightedEntries(caseDefinition.PossibleRewards);
            if (weightedEntries.Count == 0)
                return false;

            session = new CaseOpenSession(++_nextSessionId, caseDefinition, ResolveOpenCost(caseDefinition));
            _preparedSession = session;
            _hasPreparedSession = true;
            IsPresentationActive = true;
            return true;
        }

        public bool CanRollPreparedCase(CaseOpenSession session)
        {
            if (!TryResolvePreparedSession(session, out CaseOpenSession preparedSession))
                return false;

            return CanConsumeCaseAndOpenCost(preparedSession.CaseRewardId, preparedSession.OpenCost);
        }

        public bool TryStartCaseRoll(CaseOpenSession session, out CaseOpenResult caseOpenResult)
        {
            caseOpenResult = default;

            if (!TryResolvePreparedSession(session, out CaseOpenSession preparedSession))
                return false;

            EnsureRewardManager();
            EnsureProfile();

            if (!CanConsumeCaseAndOpenCost(preparedSession.CaseRewardId, preparedSession.OpenCost))
                return false;

            if (!TryBuildCaseOpenResult(preparedSession.CaseDefinition, preparedSession.OpenCost, out caseOpenResult))
                return false;

            if (!TrySpendCaseAndOpenCost(preparedSession.CaseRewardId, preparedSession.OpenCost))
                return false;

            _rewardManager.GrantReward(caseOpenResult.GrantedReward, saveImmediately: false);
            _profile.Save();
            _preparedSession = default;
            _hasPreparedSession = false;
            return true;
        }

        public void CompletePresentation()
        {
            _preparedSession = default;
            _hasPreparedSession = false;
            IsPresentationActive = false;
        }

        private bool TryBuildCaseOpenResult(CaseDefinitionData caseDefinition, ResolvedReward openCost, out CaseOpenResult caseOpenResult)
        {
            caseOpenResult = default;

            if (caseDefinition == null
                || caseDefinition.CaseReward == null
                || caseDefinition.CaseReward.Kind != RewardType.Case
                || caseDefinition.PossibleRewards == null)
                return false;

            List<CaseRewardPoolConfig.Entry> weightedEntries = CollectWeightedEntries(caseDefinition.PossibleRewards);
            if (weightedEntries.Count == 0)
                return false;

            System.Random random = CreateCaseRandom(caseDefinition.CaseRewardId);
            ResolvedReward grantedReward = ResolveWeightedReward(weightedEntries, random);
            if (!grantedReward.HasReward || grantedReward.Amount <= 0)
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
            caseOpenResult = new CaseOpenResult(caseDefinition, openCost, grantedReward, reelRewards, winningIndex);
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
            List<ResolvedReward> previewRewards = BuildPreviewRewards(weightedEntries);
            List<ResolvedReward> reelRewards = new List<ResolvedReward>(reelItemCount);
            if (previewRewards.Count == 0)
                return reelRewards;

            int offset = previewRewards.Count > 1
                ? random.Next(0, previewRewards.Count)
                : 0;

            for (int i = 0; i < reelItemCount; i++)
                reelRewards.Add(previewRewards[(offset + i) % previewRewards.Count]);

            if (winningIndex >= 0 && winningIndex < reelRewards.Count)
                reelRewards[winningIndex] = winningReward;

            return reelRewards;
        }

        private static List<ResolvedReward> BuildPreviewRewards(IReadOnlyList<CaseRewardPoolConfig.Entry> weightedEntries)
        {
            List<ResolvedReward> rewards = new List<ResolvedReward>(weightedEntries.Count);

            for (int i = 0; i < weightedEntries.Count; i++)
            {
                ResolvedReward reward = weightedEntries[i].ResolvePreviewReward();
                if (!reward.HasReward || reward.Amount <= 0)
                    continue;

                rewards.Add(reward);
            }

            return rewards;
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

        private static ResolvedReward ResolveOpenCost(CaseDefinitionData caseDefinition)
        {
            if (caseDefinition == null || !caseDefinition.HasOpenCost || caseDefinition.OpenCost.Reward == null)
                return default;

            return new ResolvedReward(caseDefinition.OpenCost.Reward, caseDefinition.OpenCost.Amount);
        }

        private bool TryResolvePreparedSession(CaseOpenSession session, out CaseOpenSession preparedSession)
        {
            preparedSession = default;

            if (!_hasPreparedSession || !session.IsValid || !_preparedSession.IsValid)
                return false;

            if (_preparedSession.SessionId != session.SessionId)
                return false;

            preparedSession = _preparedSession;
            return true;
        }

        private bool CanConsumeCaseAndOpenCost(string caseRewardId, ResolvedReward openCost)
        {
            if (_profile == null || string.IsNullOrWhiteSpace(caseRewardId))
                return false;

            int caseCostAmount = openCost.HasReward
                && openCost.Amount > 0
                && openCost.IsInventoryReward
                && openCost.RewardId == caseRewardId
                    ? openCost.Amount
                    : 0;

            if (_profile.GetInventoryRewardCount(caseRewardId) < 1 + caseCostAmount)
                return false;

            if (!openCost.HasReward || openCost.Amount <= 0)
                return true;

            switch (openCost.RewardKind)
            {
                case RewardType.Cash:
                    return _profile.CanAffordCash(openCost.Amount);

                case RewardType.Gold:
                    return _profile.CanAffordGold(openCost.Amount);

                default:
                    return openCost.RewardId == caseRewardId
                        || _profile.GetInventoryRewardCount(openCost.RewardId) >= openCost.Amount;
            }
        }

        private bool TrySpendCaseAndOpenCost(string caseRewardId, ResolvedReward openCost)
        {
            if (!CanConsumeCaseAndOpenCost(caseRewardId, openCost))
                return false;

            if (openCost.HasReward && openCost.Amount > 0)
            {
                switch (openCost.RewardKind)
                {
                    case RewardType.Cash:
                        if (!_profile.TrySpendCash(openCost.Amount, saveImmediately: false))
                            return false;
                        break;

                    case RewardType.Gold:
                        if (!_profile.TrySpendGold(openCost.Amount, saveImmediately: false))
                            return false;
                        break;

                    default:
                        if (openCost.RewardId == caseRewardId)
                            return _profile.TrySpendInventoryReward(caseRewardId, openCost.Amount + 1, saveImmediately: false);

                        if (!_profile.TrySpendInventoryReward(openCost.RewardId, openCost.Amount, saveImmediately: false))
                            return false;
                        break;
                }
            }

            return _profile.TrySpendInventoryReward(caseRewardId, 1, saveImmediately: false);
        }

        private void EnsureRewardManager()
        {
            if (_rewardManager == null)
                throw new InvalidOperationException("CaseManager requires RewardManager before cases can be opened.");
        }

        private void EnsureProfile()
        {
            if (_profile == null)
                throw new InvalidOperationException("CaseManager requires ProfileManager before cases can be opened.");
        }
    }
}
