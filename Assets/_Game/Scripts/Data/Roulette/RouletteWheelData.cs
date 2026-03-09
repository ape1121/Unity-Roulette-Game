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
        [SerializeField] private Sprite wheelBackground;

        public bool AllowDuplicateRewards => allowDuplicateRewards;
        public RouletteSliceData[] SliceDefinitions => sliceDefinitions ?? System.Array.Empty<RouletteSliceData>();
        public float SpinDuration => Mathf.Max(0.5f, spinDuration);
        public int FullRotations => Mathf.Max(1, fullRotations);
        public float SettleOvershootDegrees => Mathf.Max(0f, settleOvershootDegrees);
        public RouletteZoneType ZoneType => zoneType;
    }
}
