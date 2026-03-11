using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RouletteWheelData", menuName = "CriticalShot/Roulette/Roulette Wheel Data")]
    public sealed class RouletteWheelData : ScriptableObject
    {
        [SerializeField] private RouletteZoneType _zoneType;
        [SerializeField] private bool _allowDuplicateRewards;
        [SerializeField] private RouletteSliceData[] _sliceDefinitions;
        [Min(0.5f)] [SerializeField] private float _spinDuration = 3.4f;
        [Min(1)] [SerializeField] private int _fullRotations = 6;
        [Min(0.05f)] [SerializeField] private float _settleDuration = 0.24f;
        [SerializeField] private Ease _spinEase = Ease.OutQuart;
        [SerializeField] private Ease _settleEase = Ease.OutBack;
        [Min(1f)] [SerializeField] private float _startScale = 1.05f;
        [Min(0.01f)] [SerializeField] private float _startScaleDuration = 0.18f;
        [Min(0.01f)] [SerializeField] private float _endScaleDuration = 0.12f;
        [SerializeField] private Ease _scaleEase = Ease.OutCubic;
        [SerializeField] private Sprite _wheelBackground;
        [SerializeField] private Sprite _rouletteIndicator;

        public bool AllowDuplicateRewards => _allowDuplicateRewards;
        public RouletteSliceData[] SliceDefinitions => _sliceDefinitions ?? System.Array.Empty<RouletteSliceData>();
        public float SpinDuration => Mathf.Max(0.5f, _spinDuration);
        public int FullRotations => Mathf.Max(1, _fullRotations);
        public float SettleDuration => Mathf.Max(0.05f, _settleDuration);
        public Ease SpinEase => _spinEase;
        public Ease SettleEase => _settleEase;
        public float StartScale => Mathf.Max(1f, _startScale);
        public float StartScaleDuration => Mathf.Max(0.01f, _startScaleDuration);
        public float EndScaleDuration => Mathf.Max(0.01f, _endScaleDuration);
        public Ease ScaleEase => _scaleEase;
        public RouletteZoneType ZoneType => _zoneType;
        public Sprite WheelBackground => _wheelBackground;
        public Sprite RouletteIndicator => _rouletteIndicator;
    }
}
