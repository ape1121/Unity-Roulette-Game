using System;
using System.Collections.Generic;
using Ape.Data;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Ape.Game
{
    public sealed class RouletteWheelLayoutController
    {
        private readonly List<RouletteRewardSliceUI> _spawnedSlices = new List<RouletteRewardSliceUI>();

        private RectTransform _sliceRootRect;
        private Image _wheelBackgroundImage;
        private Image _rouletteIndicatorImage;
        private RouletteRewardSliceUI _rewardSlicePrefab;
        private bool _useBackgroundShortestDimension = true;
        private float _fallbackDiameter = 640f;
        private float _sliceRadiusPaddingRatio = 0.1125f;
        private float _sliceSizeRatio = 0.15625f;

        public int SliceCount => _spawnedSlices.Count;
        public bool HasSlices => _spawnedSlices.Count > 0;

        public void Configure(
            RectTransform sliceRootRect,
            Image wheelBackgroundImage,
            Image rouletteIndicatorImage,
            RouletteRewardSliceUI rewardSlicePrefab,
            bool useBackgroundShortestDimension,
            float fallbackDiameter,
            float sliceRadiusPaddingRatio,
            float sliceSizeRatio)
        {
            _sliceRootRect = sliceRootRect;
            _wheelBackgroundImage = wheelBackgroundImage;
            _rouletteIndicatorImage = rouletteIndicatorImage;
            _rewardSlicePrefab = rewardSlicePrefab;
            _useBackgroundShortestDimension = useBackgroundShortestDimension;
            _fallbackDiameter = fallbackDiameter;
            _sliceRadiusPaddingRatio = sliceRadiusPaddingRatio;
            _sliceSizeRatio = sliceSizeRatio;
        }

        public void BuildWheel(RouletteResolvedWheel wheel, Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            ApplyWheelBackground(wheel);

            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _rewardSlicePrefab == null || _sliceRootRect == null)
            {
                ClearSlices();
                return;
            }

            EnsureSliceViewCount(wheel.Slices.Count);
            RefreshSliceViews(wheel, rarityColorResolver);
        }

        public void Relayout(RouletteResolvedWheel wheel)
        {
            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0)
                return;

            float sliceAngle = 360f / wheel.Slices.Count;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;

            for (int i = 0; i < _spawnedSlices.Count; i++)
            {
                if (_spawnedSlices[i] == null)
                    continue;

                LayoutSlice(_spawnedSlices[i].RootRect, i, sliceAngle, radius, sliceSize);
            }
        }

        public RouletteRewardSliceUI GetSliceView(int index)
        {
            return index >= 0 && index < _spawnedSlices.Count
                ? _spawnedSlices[index]
                : null;
        }

        public void BindSliceView(
            RouletteRewardSliceUI sliceView,
            int index,
            RouletteResolvedSlice slice,
            int sliceCount,
            Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            if (sliceView == null || sliceCount <= 0)
                return;

            float sliceAngle = 360f / sliceCount;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;
            Color rarityColor = rarityColorResolver != null ? rarityColorResolver(slice) : Color.white;

            sliceView.Bind(slice, rarityColor);
            LayoutSlice(sliceView.RootRect, index, sliceAngle, radius, sliceSize);
        }

        public void FinalizePostSpinReveal(RouletteResolvedWheel nextWheel, Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            ApplyWheelBackground(nextWheel);

            if (nextWheel == null || nextWheel.Slices == null)
            {
                ClearSlices();
                return;
            }

            if (nextWheel.Slices.Count != _spawnedSlices.Count)
            {
                BuildWheel(nextWheel, rarityColorResolver);
                return;
            }

            Relayout(nextWheel);
        }

        private void RefreshSliceViews(RouletteResolvedWheel wheel, Func<RouletteResolvedSlice, Color> rarityColorResolver)
        {
            if (wheel == null || wheel.Slices == null)
                return;

            for (int i = 0; i < wheel.Slices.Count && i < _spawnedSlices.Count; i++)
                BindSliceView(_spawnedSlices[i], i, wheel.Slices[i], wheel.Slices.Count, rarityColorResolver);
        }

        private void ClearSlices()
        {
            for (int i = _spawnedSlices.Count - 1; i >= 0; i--)
            {
                if (_spawnedSlices[i] == null)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(_spawnedSlices[i].gameObject);
                else
                    Object.DestroyImmediate(_spawnedSlices[i].gameObject);
            }

            _spawnedSlices.Clear();
        }

        private void EnsureSliceViewCount(int targetCount)
        {
            for (int i = _spawnedSlices.Count - 1; i >= targetCount; i--)
                DestroySliceAt(i);

            while (_spawnedSlices.Count < targetCount)
            {
                RouletteRewardSliceUI sliceView = Object.Instantiate(_rewardSlicePrefab, _sliceRootRect);
                _spawnedSlices.Add(sliceView);
            }
        }

        private void DestroySliceAt(int index)
        {
            RouletteRewardSliceUI sliceView = _spawnedSlices[index];
            _spawnedSlices.RemoveAt(index);

            if (sliceView == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(sliceView.gameObject);
            else
                Object.DestroyImmediate(sliceView.gameObject);
        }

        private void ApplyWheelBackground(RouletteResolvedWheel wheel)
        {
            if (_wheelBackgroundImage != null)
            {
                RouletteWheelData wheelDefinition = wheel != null ? wheel.Definition : null;
                Sprite backgroundSprite = wheelDefinition != null ? wheelDefinition.WheelBackground : null;
                _wheelBackgroundImage.sprite = backgroundSprite;
                _wheelBackgroundImage.enabled = backgroundSprite != null;
            }

            if (_rouletteIndicatorImage == null)
                return;

            RouletteWheelData indicatorDefinition = wheel != null ? wheel.Definition : null;
            Sprite indicatorSprite = indicatorDefinition != null ? indicatorDefinition.RouletteIndicator : null;
            _rouletteIndicatorImage.sprite = indicatorSprite;
            _rouletteIndicatorImage.enabled = indicatorSprite != null;
        }

        private float ResolveWheelDiameter()
        {
            if (!_useBackgroundShortestDimension || _wheelBackgroundImage == null)
                return _fallbackDiameter;

            RectTransform backgroundRect = _wheelBackgroundImage.rectTransform;
            float shortestDimension = Mathf.Min(backgroundRect.rect.width, backgroundRect.rect.height);
            return shortestDimension > 0f ? shortestDimension : _fallbackDiameter;
        }

        private static void LayoutSlice(RectTransform sliceRect, int index, float sliceAngle, float radius, float size)
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
    }
}
