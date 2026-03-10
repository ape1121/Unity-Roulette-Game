using Ape.Data;

namespace Ape.Game
{
    public readonly struct GameStateSnapshot
    {
        public GameRunPhase Phase { get; }
        public int CurrentZone { get; }
        public RouletteZoneType CurrentZoneType { get; }
        public int PendingCash { get; }
        public int PendingGold { get; }
        public int PendingInventoryRewardCount { get; }
        public int PendingInventoryRewardKinds { get; }
        public int SavedCash { get; }
        public int SavedGold { get; }
        public bool HasUsedContinue { get; }
        public bool CanSpin { get; }
        public bool CanCashOut { get; }
        public bool CanContinue { get; }
        public bool CanRestart { get; }
        public int ActiveSliceCount { get; }

        public GameStateSnapshot(
            GameRunPhase phase,
            int currentZone,
            RouletteZoneType currentZoneType,
            int pendingCash,
            int pendingGold,
            int pendingInventoryRewardCount,
            int pendingInventoryRewardKinds,
            int savedCash,
            int savedGold,
            bool hasUsedContinue,
            bool canSpin,
            bool canCashOut,
            bool canContinue,
            bool canRestart,
            int activeSliceCount)
        {
            Phase = phase;
            CurrentZone = currentZone;
            CurrentZoneType = currentZoneType;
            PendingCash = pendingCash;
            PendingGold = pendingGold;
            PendingInventoryRewardCount = pendingInventoryRewardCount;
            PendingInventoryRewardKinds = pendingInventoryRewardKinds;
            SavedCash = savedCash;
            SavedGold = savedGold;
            HasUsedContinue = hasUsedContinue;
            CanSpin = canSpin;
            CanCashOut = canCashOut;
            CanContinue = canContinue;
            CanRestart = canRestart;
            ActiveSliceCount = activeSliceCount;
        }
    }
}
