using System;
using UnityEngine;

namespace Ape.Data
{
    [Serializable]
    public struct CaseOpenCostData
    {
        public RewardData reward;
        [Min(0)] public int amount;

        public RewardData Reward => reward;
        public int Amount => Mathf.Max(0, amount);
        public bool HasCost => reward != null && Amount > 0;

        public void Normalize()
        {
            amount = Mathf.Max(0, amount);

            if (reward == null || amount == 0)
            {
                reward = null;
                amount = 0;
                return;
            }
        }
    }
}
