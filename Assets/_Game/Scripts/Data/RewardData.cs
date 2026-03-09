using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Ape.Data
{
    [MovedFrom(false, sourceNamespace: "")]
    [CreateAssetMenu(fileName = "RewardData", menuName = "CriticalShot/Roulette/Reward Data")]
    public sealed class RewardData : ScriptableObject
    {
        public enum RewardKind
        {
            Cash,
            Gold,
            ItemCard
        }

        public enum RewardRarity
        {
            Common,
            Rare,
            Epic,
            Legendary
        }

        [SerializeField] private string rewardId;
        [SerializeField] private string rewardName;
        [SerializeField] private Sprite icon;
        [SerializeField] private RewardKind rewardKind = RewardKind.ItemCard;
        [SerializeField] private RewardRarity rarity = RewardRarity.Common;
        [Min(1)] [SerializeField] private int minAmount = 1;
        [Min(1)] [SerializeField] private int maxAmount = 1;
        [Min(0)] [SerializeField] private int amountIncreasePerZone;

        public string RewardId => string.IsNullOrWhiteSpace(rewardId) ? name : rewardId;
        public string RewardName => string.IsNullOrWhiteSpace(rewardName) ? name : rewardName;
        public Sprite Icon => icon;
        public RewardKind Kind => rewardKind;
        public RewardRarity Rarity => rarity;

        public int ResolveAmount(int zone, int amountMultiplier, int flatAmountBonus, System.Random random)
        {
            int resolvedMinAmount = Mathf.Max(1, minAmount);
            int resolvedMaxAmount = Mathf.Max(resolvedMinAmount, maxAmount);
            int baseAmount = resolvedMinAmount == resolvedMaxAmount
                ? resolvedMinAmount
                : random.Next(resolvedMinAmount, resolvedMaxAmount + 1);

            int resolvedAmountMultiplier = Mathf.Max(1, amountMultiplier);
            int zoneBonus = Mathf.Max(0, zone - 1) * Mathf.Max(0, amountIncreasePerZone);
            return Mathf.Max(1, (baseAmount * resolvedAmountMultiplier) + Mathf.Max(0, flatAmountBonus) + zoneBonus);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                rewardId = name;

            if (string.IsNullOrWhiteSpace(rewardName))
                rewardName = name;

            maxAmount = Mathf.Max(minAmount, maxAmount);
        }
    }
}
