using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouletteWheelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private RectTransform _wheelRotatorRect;
        [SerializeField] private RectTransform _sliceRootRect;
        [SerializeField] private RouletteRewardSliceUI _rewardSlicePrefab;
        [Min(100f)] [SerializeField] private float _diameter = 640f;
        [Min(0f)] [SerializeField] private float _sliceRadiusPadding = 72f;

        private readonly List<RouletteRewardSliceUI> _spawnedSlices = new List<RouletteRewardSliceUI>();

        private Sequence _spinSequence;
        private float _currentRotationDegrees;

        public void BuildWheel(RouletteResolvedWheel wheel)
        {
            ClearSlices();

            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _rewardSlicePrefab == null || _sliceRootRect == null)
                return;

            float sliceAngle = 360f / wheel.Slices.Count;
            float radius = Mathf.Max(0f, (_diameter * 0.5f) - _sliceRadiusPadding);

            for (int i = 0; i < wheel.Slices.Count; i++)
            {
                RouletteRewardSliceUI sliceView = Instantiate(_rewardSlicePrefab, _sliceRootRect);
                sliceView.Bind(wheel.Slices[i]);
                LayoutSlice(sliceView.RootRect, i, sliceAngle, radius);
                _spawnedSlices.Add(sliceView);
            }

            SetWheelRotation(0f);
        }

        public void StopAnimation()
        {
            if (_spinSequence != null && _spinSequence.IsActive())
                _spinSequence.Kill();

            _spinSequence = null;
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
                .SetEase(Ease.OutQuart);

            Tween settleTween = DOTween.To(
                    () => animatedRotation,
                    value =>
                    {
                        animatedRotation = value;
                        SetWheelRotation(value);
                        EmitSliceTicks(ref lastTickStep, sliceAngle, value, wheel.Slices.Count, onSliceTick);
                    },
                    endRotation,
                    0.24f)
                .SetEase(Ease.OutBack);

            _spinSequence = DOTween.Sequence();
            _spinSequence.Append(_wheelRotatorRect.DOScale(1.05f, 0.18f).SetEase(Ease.OutCubic));
            _spinSequence.Join(mainRotationTween);
            _spinSequence.Append(settleTween);
            _spinSequence.Append(_wheelRotatorRect.DOScale(1f, 0.12f).SetEase(Ease.OutCubic));
            _spinSequence.OnComplete(() =>
            {
                _currentRotationDegrees = endRotation;
                SetWheelRotation(_currentRotationDegrees);
                onComplete?.Invoke();
            });
            _spinSequence.OnKill(() => _spinSequence = null);
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

        private void LayoutSlice(RectTransform sliceRect, int index, float sliceAngle, float radius)
        {
            if (sliceRect == null)
                return;

            sliceRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliceRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliceRect.pivot = new Vector2(0.5f, 0.5f);

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
