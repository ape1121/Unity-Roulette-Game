using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    public sealed class RouletteResolvedWheel
    {
        public RouletteWheelData WheelData { get; }
        public RouletteZoneType ZoneType { get; }
        public IReadOnlyList<RouletteResolvedSlice> Slices { get; }

        public RouletteResolvedWheel(RouletteWheelData wheelData, RouletteZoneType zoneType, IReadOnlyList<RouletteResolvedSlice> slices)
        {
            WheelData = wheelData;
            ZoneType = zoneType;
            Slices = slices;
        }

        public float SpinDuration => WheelData != null ? WheelData.SpinDuration : 3f;
        public int FullRotations => WheelData != null ? WheelData.FullRotations : 6;
        public float SettleOvershootDegrees => WheelData != null ? WheelData.SettleOvershootDegrees : 0f;
    }
}
