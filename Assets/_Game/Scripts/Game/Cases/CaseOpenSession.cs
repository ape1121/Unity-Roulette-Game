using Ape.Data;

namespace Ape.Game
{
    public readonly struct CaseOpenSession
    {
        public CaseOpenSession(int sessionId, CaseDefinitionData caseDefinition, ResolvedReward openCost)
        {
            SessionId = sessionId;
            CaseDefinition = caseDefinition;
            OpenCost = openCost;
        }

        public int SessionId { get; }
        public CaseDefinitionData CaseDefinition { get; }
        public ResolvedReward OpenCost { get; }

        public RewardData CaseReward => CaseDefinition != null ? CaseDefinition.CaseReward : null;
        public string CaseRewardId => CaseDefinition != null ? CaseDefinition.CaseRewardId : string.Empty;
        public bool HasOpenCost => OpenCost.HasReward && OpenCost.Amount > 0;
        public bool IsValid => SessionId > 0 && CaseDefinition != null && CaseReward != null;
    }
}
