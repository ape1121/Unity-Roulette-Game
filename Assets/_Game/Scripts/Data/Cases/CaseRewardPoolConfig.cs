using System;
using System.Collections.Generic;
using Ape.Game;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "CaseRewardPool", menuName = "CriticalShot/Cases/Case Reward Pool")]
    public sealed class CaseRewardPoolConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public RewardData rewardData;
            [Min(0.001f)] public float weight;
            [Min(0)] public int minAmountOverride;
            [Min(0)] public int maxAmountOverride;

            public bool HasAmountOverride => minAmountOverride > 0 || maxAmountOverride > 0;

            public int ResolveAmount(System.Random random)
            {
                if (rewardData == null)
                    return 0;

                if (!HasAmountOverride)
                    return rewardData.ResolveAmount(1, 1, 0, random);

                int resolvedMinAmount = Mathf.Max(1, minAmountOverride > 0 ? minAmountOverride : maxAmountOverride);
                int resolvedMaxAmount = Mathf.Max(resolvedMinAmount, maxAmountOverride > 0 ? maxAmountOverride : resolvedMinAmount);
                return resolvedMinAmount == resolvedMaxAmount
                    ? resolvedMinAmount
                    : random.Next(resolvedMinAmount, resolvedMaxAmount + 1);
            }

            public int ResolvePreviewAmount()
            {
                if (rewardData == null)
                    return 0;

                if (!HasAmountOverride)
                    return rewardData.ResolvePreviewAmount();

                int resolvedMinAmount = Mathf.Max(1, minAmountOverride > 0 ? minAmountOverride : maxAmountOverride);
                int resolvedMaxAmount = Mathf.Max(resolvedMinAmount, maxAmountOverride > 0 ? maxAmountOverride : resolvedMinAmount);
                return Mathf.Max(1, resolvedMaxAmount);
            }

            public ResolvedReward ResolvePreviewReward()
            {
                return rewardData == null
                    ? default
                    : new ResolvedReward(rewardData, ResolvePreviewAmount());
            }
        }

        [SerializeField] private Entry[] rewards;

        public Entry[] Rewards => rewards ?? Array.Empty<Entry>();

        public void GetPreviewRewards(List<ResolvedReward> destination)
        {
            destination?.Clear();

            if (destination == null)
                return;

            Entry[] entries = Rewards;

            for (int i = 0; i < entries.Length; i++)
            {
                ResolvedReward reward = entries[i].ResolvePreviewReward();
                if (!reward.HasReward || reward.Amount <= 0)
                    continue;

                destination.Add(reward);
            }
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            Entry[] entries = Rewards;

            for (int i = 0; i < entries.Length; i++)
            {
                RewardData candidate = entries[i].rewardData;
                if (candidate == null || candidate.RewardId != rewardId)
                    continue;

                rewardData = candidate;
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (rewards == null)
                return;

            for (int i = 0; i < rewards.Length; i++)
            {
                Entry entry = rewards[i];
                entry.weight = Mathf.Max(0.001f, entry.weight);

                if (entry.maxAmountOverride > 0)
                    entry.maxAmountOverride = Mathf.Max(entry.minAmountOverride, entry.maxAmountOverride);

                rewards[i] = entry;
            }
        }
    }
}
