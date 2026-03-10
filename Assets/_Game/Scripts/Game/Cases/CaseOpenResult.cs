using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    public readonly struct CaseOpenResult
    {
        public CaseRewardsConfig.CaseDefinition CaseDefinition { get; }
        public ResolvedReward GrantedReward { get; }
        public IReadOnlyList<ResolvedReward> ReelRewards { get; }
        public int WinningReelIndex { get; }

        public CaseOpenResult(
            CaseRewardsConfig.CaseDefinition caseDefinition,
            ResolvedReward grantedReward,
            IReadOnlyList<ResolvedReward> reelRewards,
            int winningReelIndex)
        {
            CaseDefinition = caseDefinition;
            GrantedReward = grantedReward;
            ReelRewards = reelRewards;
            WinningReelIndex = winningReelIndex;
        }

        public RewardData CaseReward => CaseDefinition.CaseReward;
        public bool IsValid => GrantedReward.HasReward && ReelRewards != null && ReelRewards.Count > 0;
    }
}
