using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "CaseDefinition", menuName = "CriticalShot/Cases/Case Definition")]
    public sealed class CaseDefinitionData : ScriptableObject
    {
        [SerializeField] private RewardData _caseReward;
        [SerializeField] private CaseRewardPoolConfig _possibleRewards;
        [SerializeField] private CaseOpenCostData _openCost;
        [Min(24)] [SerializeField] private int _reelItemCount = 24;
        [Min(0)] [SerializeField] private int _minimumLandingIndex;
        [Min(4)] [SerializeField] private int _landingTailCount = 4;

        public RewardData CaseReward => _caseReward;
        public CaseRewardPoolConfig PossibleRewards => _possibleRewards;
        public CaseOpenCostData OpenCost => _openCost;
        public bool HasOpenCost => _openCost.HasCost;
        public string CaseRewardId => _caseReward != null ? _caseReward.RewardId : string.Empty;
        public int ResolveReelItemCount() => Mathf.Max(24, _reelItemCount);
        public int ResolveMinimumLandingIndex() => Mathf.Max(0, _minimumLandingIndex);
        public int ResolveLandingTailCount() => Mathf.Max(4, _landingTailCount);

        private void OnValidate()
        {
            _reelItemCount = Mathf.Max(24, _reelItemCount);
            _minimumLandingIndex = Mathf.Max(0, _minimumLandingIndex);
            _landingTailCount = Mathf.Max(4, _landingTailCount);
            _openCost.Normalize();
        }
    }
}
