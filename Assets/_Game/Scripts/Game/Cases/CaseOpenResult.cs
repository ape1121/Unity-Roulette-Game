using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    public readonly struct CaseOpenResult
    {
        public CaseDefinitionData CaseDefinition { get; }
        public ResolvedReward OpenCost { get; }
        public ResolvedReward GrantedReward { get; }
        public IReadOnlyList<ResolvedReward> ReelRewards { get; }
        public int WinningReelIndex { get; }

        public CaseOpenResult(
            CaseDefinitionData caseDefinition,
            ResolvedReward openCost,
            ResolvedReward grantedReward,
            IReadOnlyList<ResolvedReward> reelRewards,
            int winningReelIndex)
        {
            CaseDefinition = caseDefinition;
            OpenCost = openCost;
            GrantedReward = grantedReward;
            ReelRewards = reelRewards;
            WinningReelIndex = winningReelIndex;
        }

        public RewardData CaseReward => CaseDefinition != null ? CaseDefinition.CaseReward : null;
        public bool HasOpenCost => OpenCost.HasReward && OpenCost.Amount > 0;
        public bool IsValid =>
            CaseDefinition != null
            && GrantedReward.HasReward
            && ReelRewards != null
            && ReelRewards.Count > 0
            && WinningReelIndex >= 0
            && WinningReelIndex < ReelRewards.Count;
    }
}
