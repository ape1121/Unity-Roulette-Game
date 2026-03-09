using DG.Tweening;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteWheelData", menuName = "CriticalShot/Roulette/Roulette Wheel Data")]
    public sealed class RouletteWheelData : ScriptableObject
    {
        [SerializeField] private RouletteZoneType zoneType;
        [SerializeField] private bool allowDuplicateRewards;
        [SerializeField] private RouletteSliceData[] sliceDefinitions;
        [Min(0.5f)] [SerializeField] private float spinDuration = 3.4f;
        [Min(1)] [SerializeField] private int fullRotations = 6;
        [Range(0f, 20f)] [SerializeField] private float settleOvershootDegrees = 6f;
        [Min(0.05f)] [SerializeField] private float settleDuration = 0.24f;
        [SerializeField] private Ease spinEase = Ease.OutQuart;
        [SerializeField] private Ease settleEase = Ease.OutBack;
        [Min(1f)] [SerializeField] private float startScale = 1.05f;
        [Min(0.01f)] [SerializeField] private float startScaleDuration = 0.18f;
        [Min(0.01f)] [SerializeField] private float endScaleDuration = 0.12f;
        [SerializeField] private Ease scaleEase = Ease.OutCubic;
        [SerializeField] private Sprite wheelBackground;

        public bool AllowDuplicateRewards => allowDuplicateRewards;
        public RouletteSliceData[] SliceDefinitions => sliceDefinitions ?? System.Array.Empty<RouletteSliceData>();
        public float SpinDuration => Mathf.Max(0.5f, spinDuration);
        public int FullRotations => Mathf.Max(1, fullRotations);
        public float SettleOvershootDegrees => Mathf.Max(0f, settleOvershootDegrees);
        public float SettleDuration => Mathf.Max(0.05f, settleDuration);
        public Ease SpinEase => spinEase;
        public Ease SettleEase => settleEase;
        public float StartScale => Mathf.Max(1f, startScale);
        public float StartScaleDuration => Mathf.Max(0.01f, startScaleDuration);
        public float EndScaleDuration => Mathf.Max(0.01f, endScaleDuration);
        public Ease ScaleEase => scaleEase;
        public RouletteZoneType ZoneType => zoneType;
        public Sprite WheelBackground => wheelBackground;
    }
}
