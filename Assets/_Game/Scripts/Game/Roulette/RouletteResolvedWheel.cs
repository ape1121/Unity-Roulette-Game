using System.Collections.Generic;
using Ape.Data;
using DG.Tweening;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RouletteResolvedWheel
    {
        public RouletteWheelData WheelData { get; }
        public IReadOnlyList<RouletteResolvedSlice> Slices { get; }

        public RouletteResolvedWheel(RouletteWheelData wheelData, IReadOnlyList<RouletteResolvedSlice> slices)
        {
            WheelData = wheelData;
            Slices = slices;
        }

        public RouletteZoneType ZoneType => WheelData != null ? WheelData.ZoneType : RouletteZoneType.Normal;
        public Sprite WheelBackground => WheelData != null ? WheelData.WheelBackground : null;
        public Sprite RouletteIndicator => WheelData != null ? WheelData.RouletteIndicator : null;
        public float SpinDuration => WheelData != null ? WheelData.SpinDuration : 3f;
        public int FullRotations => WheelData != null ? WheelData.FullRotations : 6;
        public float SettleOvershootDegrees => WheelData != null ? WheelData.SettleOvershootDegrees : 0f;
        public float SettleDuration => WheelData != null ? WheelData.SettleDuration : 0.24f;
        public Ease SpinEase => WheelData != null ? WheelData.SpinEase : Ease.OutQuart;
        public Ease SettleEase => WheelData != null ? WheelData.SettleEase : Ease.OutBack;
        public float StartScale => WheelData != null ? WheelData.StartScale : 1.05f;
        public float StartScaleDuration => WheelData != null ? WheelData.StartScaleDuration : 0.18f;
        public float EndScaleDuration => WheelData != null ? WheelData.EndScaleDuration : 0.12f;
        public Ease ScaleEase => WheelData != null ? WheelData.ScaleEase : Ease.OutCubic;
    }
}
