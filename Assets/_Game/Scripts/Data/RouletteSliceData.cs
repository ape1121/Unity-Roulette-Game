using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Ape.Data
{
    [MovedFrom(false, sourceNamespace: "")]
    [CreateAssetMenu(fileName = "RouletteSliceData", menuName = "CriticalShot/Configs/Roulette Slice Data")]
    public sealed class RouletteSliceData : ScriptableObject
    {
        [Serializable]
        public struct RarityWeight
        {
            public RewardData.RewardRarity rarity;
            [Min(0f)] public float weight;
        }

        [SerializeField] private bool isBomb;
        [SerializeField] private bool allowCashRewards = true;
        [SerializeField] private bool allowGoldRewards = true;
        [SerializeField] private bool allowItemRewards = true;
        [SerializeField] private RewardData.RewardRarity minimumRarity = RewardData.RewardRarity.Common;
        [SerializeField] private RewardData.RewardRarity maximumRarity = RewardData.RewardRarity.Legendary;
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
                RewardData.RewardKind.Cash => allowCashRewards,
                RewardData.RewardKind.Gold => allowGoldRewards,
                RewardData.RewardKind.ItemCard => allowItemRewards,
                _ => false
            };
        }

        public float GetRarityWeight(RewardData.RewardRarity rarity)
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
