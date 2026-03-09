using Ape.Data;

namespace Ape.Game
{
    public readonly struct RouletteResolvedSlice
    {
        public int Index { get; }
        public RouletteSliceData SliceRule { get; }
        public ResolvedReward Reward { get; }

        public RouletteResolvedSlice(int index, RouletteSliceData sliceRule, ResolvedReward reward)
        {
            Index = index;
            SliceRule = sliceRule;
            Reward = reward;
        }

        public bool IsBomb => SliceRule != null && SliceRule.IsBomb;
        public string DisplayName => IsBomb ? "Bomb" : Reward.RewardName;
    }
}
