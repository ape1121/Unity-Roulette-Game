using Ape.Data;

namespace Ape.Game
{
    public readonly struct RouletteResolvedSlice
    {
        public RouletteSliceData SliceRule { get; }
        public ResolvedReward Reward { get; }

        public RouletteResolvedSlice(RouletteSliceData sliceRule, ResolvedReward reward)
        {
            SliceRule = sliceRule;
            Reward = reward;
        }

        public bool IsBomb => SliceRule != null && SliceRule.IsBomb;
        public string DisplayName => IsBomb ? "Bomb" : Reward.RewardName;
    }
}
