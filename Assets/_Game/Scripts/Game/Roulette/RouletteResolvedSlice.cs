using Ape.Data;

namespace Ape.Game
{
    public readonly struct RouletteResolvedSlice
    {
        public RouletteSliceData SliceRule { get; }
        public ResolvedReward Reward { get; }
        public string DisplayName { get; }

        public RouletteResolvedSlice(RouletteSliceData sliceRule, ResolvedReward reward, string displayName = null)
        {
            SliceRule = sliceRule;
            Reward = reward;
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : reward.RewardName;
        }

        public bool IsBomb => SliceRule != null && SliceRule.IsBomb;
    }
}
