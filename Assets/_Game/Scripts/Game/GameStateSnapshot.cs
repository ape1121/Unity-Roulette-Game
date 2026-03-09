using Ape.Data;

namespace Ape.Game
{
    public readonly struct GameStateSnapshot
    {
        public GameRunPhase Phase { get; }
        public int CurrentZone { get; }
        public GameConfig.ZoneType CurrentZoneType { get; }
        public GameConfig.WheelVisualTheme CurrentWheelTheme { get; }
        public int PendingCash { get; }
        public int PendingGold { get; }
        public int PendingItemCardCount { get; }
        public int PendingItemCardKinds { get; }
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
            GameConfig.ZoneType currentZoneType,
            GameConfig.WheelVisualTheme currentWheelTheme,
            int pendingCash,
            int pendingGold,
            int pendingItemCardCount,
            int pendingItemCardKinds,
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
            CurrentWheelTheme = currentWheelTheme;
            PendingCash = pendingCash;
            PendingGold = pendingGold;
            PendingItemCardCount = pendingItemCardCount;
            PendingItemCardKinds = pendingItemCardKinds;
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
