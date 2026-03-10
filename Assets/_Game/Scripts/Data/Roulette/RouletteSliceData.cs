using System;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteSliceData", menuName = "CriticalShot/Roulette/Roulette Slice Data")]
    public sealed class RouletteSliceData : ScriptableObject
    {
        [Serializable]
        public struct RarityWeight
        {
            public RarityType rarity;
            [Min(0f)] public float weight;
        }

        [SerializeField] private bool isBomb;
        [SerializeField] private bool allowCashRewards = true;
        [SerializeField] private bool allowGoldRewards = true;
        [SerializeField] private bool allowItemRewards = true;
        [SerializeField] private bool allowCaseRewards;
        [SerializeField] private RarityType minimumRarity = RarityType.Common;
        [SerializeField] private RarityType maximumRarity = RarityType.Legendary;
        [Min(1)] [SerializeField] private int amountMultiplier = 1;
        [Min(0)] [SerializeField] private int flatAmountBonus;
        [SerializeField] private RarityWeight[] rarityWeights;

        public bool IsBomb => isBomb;
        public int AmountMultiplier => Mathf.Max(1, amountMultiplier);
        public int FlatAmountBonus => Mathf.Max(0, flatAmountBonus);

        public bool MatchesReward(RewardData rewardData)
        {
            if (rewardData == null || isBomb)
                return false;

            if (rewardData.Rarity < minimumRarity || rewardData.Rarity > maximumRarity)
                return false;

            return rewardData.Kind switch
            {
                RewardType.Cash => allowCashRewards,
                RewardType.Gold => allowGoldRewards,
                RewardType.ItemCard => allowItemRewards,
                RewardType.Case => allowCaseRewards,
                _ => false
            };
        }

        public float GetRarityWeight(RarityType rarity)
        {
            if (rarityWeights == null || rarityWeights.Length == 0)
                return 1f;

            for (int i = 0; i < rarityWeights.Length; i++)
            {
                if (rarityWeights[i].rarity == rarity)
                    return Mathf.Max(0f, rarityWeights[i].weight);
            }

            return 0f;
        }
    }
}
