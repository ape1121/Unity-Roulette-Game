using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "CaseRewardsConfig", menuName = "CriticalShot/Cases/Case Rewards Config")]
    public sealed class CaseRewardsConfig : ScriptableObject
    {
        [SerializeField] private CaseDefinitionData[] cases;

        private Dictionary<string, CaseDefinitionData> _caseLookup;

        public CaseDefinitionData[] Cases => cases ?? Array.Empty<CaseDefinitionData>();

        public bool TryGetCase(string rewardId, out CaseDefinitionData caseDefinition)
        {
            caseDefinition = null;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            EnsureLookup();
            return _caseLookup.TryGetValue(rewardId, out caseDefinition) && caseDefinition != null;
        }

        public bool TryGetCase(RewardData rewardData, out CaseDefinitionData caseDefinition)
        {
            caseDefinition = null;
            return rewardData != null && TryGetCase(rewardData.RewardId, out caseDefinition);
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;

            if (string.IsNullOrWhiteSpace(rewardId))
                return false;

            CaseDefinitionData[] definitions = Cases;

            for (int i = 0; i < definitions.Length; i++)
            {
                CaseDefinitionData definition = definitions[i];
                if (definition == null)
                    continue;

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

            _caseLookup = new Dictionary<string, CaseDefinitionData>();
            CaseDefinitionData[] definitions = Cases;

            for (int i = 0; i < definitions.Length; i++)
            {
                CaseDefinitionData definition = definitions[i];
                if (definition == null)
                    continue;

                string rewardId = definition.CaseRewardId;
                if (string.IsNullOrWhiteSpace(rewardId))
                    continue;

                _caseLookup[rewardId] = definition;
            }
        }
    }
}
