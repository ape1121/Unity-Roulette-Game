using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouletteWheelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private RectTransform _wheelRotatorRect;
        [SerializeField] private RectTransform _sliceRootRect;
        [SerializeField] private Image _wheelBackgroundImage;
        [SerializeField] private Image _rouletteIndicatorImage;
        [SerializeField] private RouletteRewardSliceUI _rewardSlicePrefab;
        [SerializeField] private bool _useBackgroundShortestDimension = true;
        [FormerlySerializedAs("_diameter")]
        [Min(100f)] [SerializeField] private float _fallbackDiameter = 640f;
        [Range(0f, 0.5f)] [SerializeField] private float _sliceRadiusPaddingRatio = 0.1125f; // 72/640
        [Range(0.05f, 0.5f)] [SerializeField] private float _sliceSizeRatio = 0.15625f; // 100/640

        private readonly List<RouletteRewardSliceUI> _spawnedSlices = new List<RouletteRewardSliceUI>();

        private Sequence _spinSequence;
        private float _currentRotationDegrees;
        private RouletteResolvedWheel _lastWheel;

        public void BuildWheel(RouletteResolvedWheel wheel, bool preserveRotation = true)
        {
            _lastWheel = wheel;
            ClearSlices();
            ApplyWheelBackground(wheel);

            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _rewardSlicePrefab == null || _sliceRootRect == null)
            {
                if (!preserveRotation)
                    SetWheelRotation(0f);

                return;
            }

            float sliceAngle = 360f / wheel.Slices.Count;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;

            for (int i = 0; i < wheel.Slices.Count; i++)
            {
                RouletteRewardSliceUI sliceView = Instantiate(_rewardSlicePrefab, _sliceRootRect);
                sliceView.Bind(wheel.Slices[i]);
                LayoutSlice(sliceView.RootRect, i, sliceAngle, radius, sliceSize);
                _spawnedSlices.Add(sliceView);
            }

            if (!preserveRotation)
                SetWheelRotation(0f);
        }

        public void StopAnimation()
        {
            if (_spinSequence != null && _spinSequence.IsActive())
                _spinSequence.Kill();

            _spinSequence = null;
        }

        public void ResetWheelRotation()
        {
            StopAnimation();
            SetWheelRotation(0f);
        }

        public void PlaySpin(RouletteResolvedWheel wheel, int targetSliceIndex, Action<int> onSliceTick, Action onComplete)
        {
            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _wheelRotatorRect == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAnimation();

            float sliceAngle = 360f / wheel.Slices.Count;
            float currentNormalizedRotation = Mathf.Repeat(_currentRotationDegrees, 360f);
            float targetNormalizedRotation = Mathf.Repeat(targetSliceIndex * sliceAngle, 360f);
            float deltaToTarget = Mathf.Repeat(targetNormalizedRotation - currentNormalizedRotation, 360f);
            float endRotation = _currentRotationDegrees + (wheel.FullRotations * 360f) + deltaToTarget;
            float overshootRotation = endRotation + wheel.SettleOvershootDegrees;

            float animatedRotation = _currentRotationDegrees;
            int lastTickStep = Mathf.FloorToInt(animatedRotation / sliceAngle);

            Tween mainRotationTween = DOTween.To(
                    () => animatedRotation,
                    value =>
                    {
                        animatedRotation = value;
                        SetWheelRotation(value);
                        EmitSliceTicks(ref lastTickStep, sliceAngle, value, wheel.Slices.Count, onSliceTick);
                    },
                    overshootRotation,
                    wheel.SpinDuration)
                .SetEase(wheel.SpinEase);

            Tween settleTween = DOTween.To(
                    () => animatedRotation,
                    value =>
                    {
                        animatedRotation = value;
                        SetWheelRotation(value);
                        EmitSliceTicks(ref lastTickStep, sliceAngle, value, wheel.Slices.Count, onSliceTick);
                    },
                    endRotation,
                    wheel.SettleDuration)
                .SetEase(wheel.SettleEase);

            _spinSequence = DOTween.Sequence();
            _spinSequence.Append(_wheelRotatorRect.DOScale(wheel.StartScale, wheel.StartScaleDuration).SetEase(wheel.ScaleEase));
            _spinSequence.Join(mainRotationTween);
            _spinSequence.Append(settleTween);
            _spinSequence.Append(_wheelRotatorRect.DOScale(1f, wheel.EndScaleDuration).SetEase(wheel.ScaleEase));
            _spinSequence.OnComplete(() =>
            {
                _currentRotationDegrees = endRotation;
                SetWheelRotation(_currentRotationDegrees);
                onComplete?.Invoke();
            });
            _spinSequence.OnKill(() => _spinSequence = null);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_lastWheel == null || _spawnedSlices.Count == 0)
                return;

            RelayoutSlices(_lastWheel);
        }

        private void OnDestroy()
        {
            StopAnimation();
        }

        private void OnValidate()
        {
            _rootRect ??= GetComponent<RectTransform>();
            _wheelRotatorRect ??= _rootRect;
            _sliceRootRect ??= _wheelRotatorRect;
        }

        private void ClearSlices()
        {
            StopAnimation();

            for (int i = _spawnedSlices.Count - 1; i >= 0; i--)
            {
                if (_spawnedSlices[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(_spawnedSlices[i].gameObject);
                else
                    DestroyImmediate(_spawnedSlices[i].gameObject);
            }

            _spawnedSlices.Clear();
        }

        private void ApplyWheelBackground(RouletteResolvedWheel wheel)
        {
            if (_wheelBackgroundImage == null)
                return;

            Sprite backgroundSprite = wheel != null ? wheel.WheelBackground : null;
            _wheelBackgroundImage.sprite = backgroundSprite;
            _wheelBackgroundImage.enabled = backgroundSprite != null;
            if (_rouletteIndicatorImage != null)
            {
                Sprite indicatorSprite = wheel != null ? wheel.RouletteIndicator : null;
                _rouletteIndicatorImage.sprite = indicatorSprite;
                _rouletteIndicatorImage.enabled = indicatorSprite != null;
            }
        }

        private float ResolveWheelDiameter()
        {
            if (!_useBackgroundShortestDimension || _wheelBackgroundImage == null)
                return _fallbackDiameter;

            RectTransform backgroundRect = _wheelBackgroundImage.rectTransform;
            float shortestDimension = Mathf.Min(backgroundRect.rect.width, backgroundRect.rect.height);
            return shortestDimension > 0f ? shortestDimension : _fallbackDiameter;
        }

        private void RelayoutSlices(RouletteResolvedWheel wheel)
        {
            float sliceAngle = 360f / wheel.Slices.Count;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;

            for (int i = 0; i < _spawnedSlices.Count; i++)
            {
                if (_spawnedSlices[i] == null) continue;
                LayoutSlice(_spawnedSlices[i].RootRect, i, sliceAngle, radius, sliceSize);
            }
        }

        private void LayoutSlice(RectTransform sliceRect, int index, float sliceAngle, float radius, float size)
        {
            if (sliceRect == null)
                return;

            sliceRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliceRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliceRect.pivot = new Vector2(0.5f, 0.5f);
            sliceRect.sizeDelta = new Vector2(size, size);

            float centerAngle = 90f - (index * sliceAngle);
            float radians = centerAngle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

            sliceRect.anchoredPosition = position;
            sliceRect.localRotation = Quaternion.Euler(0f, 0f, centerAngle - 90f);
            sliceRect.localScale = Vector3.one;
        }

        private void EmitSliceTicks(ref int lastTickStep, float sliceAngle, float rotationDegrees, int sliceCount, Action<int> onSliceTick)
        {
            if (onSliceTick == null)
                return;

            int currentStep = Mathf.FloorToInt(rotationDegrees / sliceAngle);
            if (currentStep <= lastTickStep)
                return;

            for (int step = lastTickStep + 1; step <= currentStep; step++)
                onSliceTick.Invoke(Mathf.Abs(step % sliceCount));

            lastTickStep = currentStep;
        }

        private void SetWheelRotation(float rotationDegrees)
        {
            _currentRotationDegrees = rotationDegrees;

            if (_wheelRotatorRect != null)
                _wheelRotatorRect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }
    }
}
