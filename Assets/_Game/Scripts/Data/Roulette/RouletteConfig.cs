using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteConfig", menuName = "CriticalShot/Roulette/RouletteConfig", order = 2)]
    public sealed class RouletteConfig : ScriptableObject
    {
        [SerializeField] private int deterministicSeed = 1337;
        [SerializeField] private bool randomizeSeedOnRunStart;
        [Min(0f)] [SerializeField] private float postSpinRevealDelay = 1f;
        [SerializeField] private RouletteRewards rewardCatalog;
        [SerializeField] private RouletteWheelData[] wheels;

        private Dictionary<RouletteZoneType, RouletteWheelData> _wheelLookup;

        public int DeterministicSeed => deterministicSeed;
        public bool RandomizeSeedOnRunStart => randomizeSeedOnRunStart;
        public float PostSpinRevealDelay => Mathf.Max(0f, postSpinRevealDelay);

        public RewardData[] GetRewardCatalog()
        {
            return rewardCatalog?.rewards ?? System.Array.Empty<RewardData>();
        }

        public int ResolveRunSeed(int runCounter)
        {
            if (randomizeSeedOnRunStart)
                return Guid.NewGuid().GetHashCode();

            return unchecked(deterministicSeed + (runCounter * 9973));
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;
            return rewardCatalog != null && rewardCatalog.TryGetReward(rewardId, out rewardData);
        }

        public RouletteWheelData GetWheelData(RouletteZoneType zoneType)
        {
            EnsureWheelLookup();
            _wheelLookup.TryGetValue(zoneType, out RouletteWheelData wheelData);
            return wheelData;
        }

        private void OnValidate()
        {
            postSpinRevealDelay = Mathf.Max(0f, postSpinRevealDelay);
            _wheelLookup = null;
        }

        private void EnsureWheelLookup()
        {
            if (_wheelLookup != null)
                return;

            _wheelLookup = new Dictionary<RouletteZoneType, RouletteWheelData>();

            if (wheels == null)
                return;

            for (int i = 0; i < wheels.Length; i++)
            {
                RouletteWheelData wheelData = wheels[i];
                if (wheelData == null)
                    continue;

                _wheelLookup[wheelData.ZoneType] = wheelData;
            }
        }
    }
}
