using System.Collections.Generic;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteRewards", menuName = "CriticalShot/Roulette/RouletteRewards", order = 1)]
    public class RouletteRewards : ScriptableObject
    {
        public RewardData[] rewards;

        private Dictionary<string, RewardData> _rewardLookup;

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            EnsureLookup();
            return _rewardLookup.TryGetValue(rewardId, out rewardData);
        }

        private void OnValidate()
        {
            _rewardLookup = null;
        }

        private void EnsureLookup()
        {
            if (_rewardLookup != null)
                return;

            _rewardLookup = new Dictionary<string, RewardData>();

            if (rewards == null)
                return;

            for (int i = 0; i < rewards.Length; i++)
            {
                RewardData currentReward = rewards[i];
                if (currentReward == null || string.IsNullOrWhiteSpace(currentReward.RewardId))
                    continue;

                _rewardLookup[currentReward.RewardId] = currentReward;
            }
        }
    }
}
