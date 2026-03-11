using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteSliceData", menuName = "CriticalShot/Roulette/Roulette Slice Data")]
    public sealed class RouletteSliceData : ScriptableObject
    {
        [SerializeField] private bool _isBomb;
        [SerializeField] private bool _allowCashRewards = true;
        [SerializeField] private bool _allowGoldRewards = true;
        [SerializeField] private bool _allowItemRewards = true;
        [SerializeField] private bool _allowCaseRewards;
        [SerializeField] private RarityType _minimumRarity = RarityType.Common;
        [SerializeField] private RarityType _maximumRarity = RarityType.Legendary;
        [Min(1)] [SerializeField] private int _amountMultiplier = 1;
        [Min(0)] [SerializeField] private int _flatAmountBonus;
        [SerializeField] private RarityWeight[] _rarityWeights;

        public bool IsBomb => _isBomb;
        public int AmountMultiplier => Mathf.Max(1, _amountMultiplier);
        public int FlatAmountBonus => Mathf.Max(0, _flatAmountBonus);

        public bool MatchesReward(RewardData rewardData)
        {
            if (rewardData == null || _isBomb)
                return false;

            if (rewardData.Rarity < _minimumRarity || rewardData.Rarity > _maximumRarity)
                return false;

            return rewardData.Kind switch
            {
                RewardType.Cash => _allowCashRewards,
                RewardType.Gold => _allowGoldRewards,
                RewardType.ItemCard => _allowItemRewards,
                RewardType.Case => _allowCaseRewards,
                _ => false
            };
        }

        public float GetRarityWeight(RarityType rarity)
        {
            if (_rarityWeights == null || _rarityWeights.Length == 0)
                return 1f;

            for (int i = 0; i < _rarityWeights.Length; i++)
            {
                if (_rarityWeights[i].rarity == rarity)
                    return Mathf.Max(0f, _rarityWeights[i].weight);
            }

            return 0f;
        }
    }
}
