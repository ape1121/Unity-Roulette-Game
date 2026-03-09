using Ape.Data;

namespace Ape.Game
{
    public readonly struct RouletteSpinResult
    {
        public int ZoneBeforeSpin { get; }
        public GameConfig.ZoneType ZoneType { get; }
        public int SelectedSliceIndex { get; }
        public RouletteResolvedSlice SelectedSlice { get; }
        public bool CompletedRun { get; }
        public int NextZone { get; }

        public RouletteSpinResult(
            int zoneBeforeSpin,
            GameConfig.ZoneType zoneType,
            int selectedSliceIndex,
            RouletteResolvedSlice selectedSlice,
            bool completedRun,
            int nextZone)
        {
            ZoneBeforeSpin = zoneBeforeSpin;
            ZoneType = zoneType;
            SelectedSliceIndex = selectedSliceIndex;
            SelectedSlice = selectedSlice;
            CompletedRun = completedRun;
            NextZone = nextZone;
        }

        public bool WasBomb => SelectedSlice.IsBomb;
        public bool DidAdvanceZone => !WasBomb && !CompletedRun && NextZone > ZoneBeforeSpin;
    }
}
