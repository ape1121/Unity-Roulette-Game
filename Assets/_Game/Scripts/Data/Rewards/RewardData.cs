using UnityEngine;
using UnityEngine.Serialization;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RewardData", menuName = "CriticalShot/Rewards/Reward Data")]
    public sealed class RewardData : ScriptableObject
    {
        [SerializeField] private string _rewardId;
        [SerializeField] private string _rewardName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private RewardType _rewardKind = RewardType.ItemCard;
        [SerializeField] private RarityType _rarity = RarityType.Common;
        [Min(1)] [SerializeField] private int _minAmount = 1;
        [Min(1)] [SerializeField] private int _maxAmount = 1;
        [Min(0)] [SerializeField] private int _amountIncreasePerZone;

        public string RewardId => string.IsNullOrWhiteSpace(_rewardId) ? name : _rewardId;
        public string RewardName => string.IsNullOrWhiteSpace(_rewardName) ? name : _rewardName;
        public Sprite Icon => _icon;
        public RewardType Kind => _rewardKind;
        public RarityType Rarity => _rarity;

        public int ResolveAmount(int zone, int amountMultiplier, int flatAmountBonus, System.Random random)
        {
            int resolvedMinAmount = Mathf.Max(1, _minAmount);
            int resolvedMaxAmount = Mathf.Max(resolvedMinAmount, _maxAmount);
            int baseAmount = resolvedMinAmount == resolvedMaxAmount
                ? resolvedMinAmount
                : random.Next(resolvedMinAmount, resolvedMaxAmount + 1);

            int resolvedAmountMultiplier = Mathf.Max(1, amountMultiplier);
            int zoneBonus = Mathf.Max(0, zone - 1) * Mathf.Max(0, _amountIncreasePerZone);
            return Mathf.Max(1, (baseAmount * resolvedAmountMultiplier) + Mathf.Max(0, flatAmountBonus) + zoneBonus);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_rewardId))
                _rewardId = name;

            if (string.IsNullOrWhiteSpace(_rewardName))
                _rewardName = name;

            _maxAmount = Mathf.Max(_minAmount, _maxAmount);
        }
    }
}
