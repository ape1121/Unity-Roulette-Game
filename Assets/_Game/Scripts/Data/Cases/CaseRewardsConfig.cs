using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "CaseRewardsConfig", menuName = "CriticalShot/Cases/Case Rewards Config")]
    public sealed class CaseRewardsConfig : ScriptableObject
    {
        [Serializable]
        public struct CaseDefinition
        {
            public RewardData caseReward;
            public CaseRewardPoolConfig possibleRewards;
            [Min(12)] public int reelItemCount;
            [Min(0)] public int minimumLandingIndex;
            [Min(2)] public int landingTailCount;

            public RewardData CaseReward => caseReward;
            public CaseRewardPoolConfig PossibleRewards => possibleRewards;
            public string CaseRewardId => caseReward != null ? caseReward.RewardId : string.Empty;
            public int ResolveReelItemCount() => Mathf.Max(12, reelItemCount);
            public int ResolveMinimumLandingIndex() => Mathf.Max(0, minimumLandingIndex);
            public int ResolveLandingTailCount() => Mathf.Max(2, landingTailCount);
        }

        [SerializeField] private CaseDefinition[] cases;

        private Dictionary<string, int> _caseLookup;

        public CaseDefinition[] Cases => cases ?? Array.Empty<CaseDefinition>();

        public bool TryGetCase(string rewardId, out CaseDefinition caseDefinition)
        {
            caseDefinition = default;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            EnsureLookup();

            if (_caseLookup.TryGetValue(rewardId, out int caseIndex))
            {
                caseDefinition = Cases[caseIndex];
                return true;
            }

            return false;
        }

        public bool TryGetCase(RewardData rewardData, out CaseDefinition caseDefinition)
        {
            caseDefinition = default;
            return rewardData != null && TryGetCase(rewardData.RewardId, out caseDefinition);
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            CaseDefinition[] definitions = Cases;

            for (int i = 0; i < definitions.Length; i++)
            {
                CaseDefinition definition = definitions[i];
                if (definition.CaseReward != null && definition.CaseReward.RewardId == rewardId)
                {
                    rewardData = definition.CaseReward;
                    return true;
                }

                if (definition.PossibleRewards != null && definition.PossibleRewards.TryGetReward(rewardId, out rewardData))
                    return true;
            }

            return false;
        }

        private void OnValidate()
        {
            _caseLookup = null;
        }

        private void EnsureLookup()
        {
            if (_caseLookup != null)
                return;

            _caseLookup = new Dictionary<string, int>();
            CaseDefinition[] definitions = Cases;

            for (int i = 0; i < definitions.Length; i++)
            {
                string rewardId = definitions[i].CaseRewardId;
                if (string.IsNullOrWhiteSpace(rewardId))
                    continue;

                _caseLookup[rewardId] = i;
            }
        }
    }
}
