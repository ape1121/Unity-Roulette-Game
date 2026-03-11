using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteConfig", menuName = "CriticalShot/Roulette/RouletteConfig", order = 2)]
    public sealed class RouletteConfig : ScriptableObject
    {
        [SerializeField] private int _deterministicSeed = 1337;
        [SerializeField] private bool _randomizeSeedOnRunStart;
        [Min(0f)] [SerializeField] private float _postSpinRevealDelay = 1f;
        [SerializeField] private RouletteRewards _rewardCatalog;
        [SerializeField] private RoulettePresentationConfig _presentationConfig;
        [SerializeField] private RouletteWheelData[] _wheels;

        private Dictionary<RouletteZoneType, RouletteWheelData> _wheelLookup;

        public int DeterministicSeed => _deterministicSeed;
        public bool RandomizeSeedOnRunStart => _randomizeSeedOnRunStart;
        public float PostSpinRevealDelay => Mathf.Max(0f, _postSpinRevealDelay);
        public RoulettePresentationConfig PresentationConfig => _presentationConfig;

        public RewardData[] GetRewardCatalog()
        {
            return _rewardCatalog?.rewards ?? System.Array.Empty<RewardData>();
        }

        public int ResolveRunSeed(int runCounter)
        {
            if (_randomizeSeedOnRunStart)
                return Guid.NewGuid().GetHashCode();

            return unchecked(_deterministicSeed + (runCounter * 9973));
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;
            return _rewardCatalog != null && _rewardCatalog.TryGetReward(rewardId, out rewardData);
        }

        public RouletteWheelData GetWheelData(RouletteZoneType zoneType)
        {
            EnsureWheelLookup();
            _wheelLookup.TryGetValue(zoneType, out RouletteWheelData wheelData);
            return wheelData;
        }

        private void OnValidate()
        {
            _postSpinRevealDelay = Mathf.Max(0f, _postSpinRevealDelay);
            _wheelLookup = null;
        }

        private void EnsureWheelLookup()
        {
            if (_wheelLookup != null)
                return;

            _wheelLookup = new Dictionary<RouletteZoneType, RouletteWheelData>();

            if (_wheels == null)
                return;

            for (int i = 0; i < _wheels.Length; i++)
            {
                RouletteWheelData wheelData = _wheels[i];
                if (wheelData == null)
                    continue;

                _wheelLookup[wheelData.ZoneType] = wheelData;
            }
        }
    }
}
