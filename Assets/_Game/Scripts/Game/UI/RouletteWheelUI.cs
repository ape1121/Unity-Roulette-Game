using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouletteWheelUI : MonoBehaviour
    {
        private const string SpinStartSoundName = "roulette_spin_start";
        private const string SpinTickSoundName = "roulette_spin_tick";
        private const string SpinStopSoundName = "roulette_spin_stop";
        private const string SmokeSoundName = "poof";

        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private RectTransform _wheelRotatorRect;
        [SerializeField] private RectTransform _sliceRootRect;
        [SerializeField] private Image _wheelBackgroundImage;
        [SerializeField] private Image _rouletteIndicatorImage;
        [SerializeField] private RouletteRewardSliceUI _rewardSlicePrefab;
        [SerializeField] private bool _useBackgroundShortestDimension = true;
        [Min(100f)] [SerializeField] private float _fallbackDiameter = 640f;
        [Range(0f, 0.5f)] [SerializeField] private float _sliceRadiusPaddingRatio = 0.1125f;
        [Range(0.05f, 0.5f)] [SerializeField] private float _sliceSizeRatio = 0.15625f;

        [Header("Indicator Sway")]
        [SerializeField] private float _indicatorSwayMaxAngle = 15f;
        [SerializeField] private float _indicatorSwayFrequency = 12f;
        [SerializeField] private float _indicatorSwayDamping = 4f;

        [Header("Overshoot")]
        [Min(0f)] [SerializeField] private float _overshootMin = 4f;
        [Min(0f)] [SerializeField] private float _overshootMax = 25f;
        [Range(0, 2)] [SerializeField] private int _maxExtraBounces = 1;
        [Min(0.05f)] [SerializeField] private float _extraBounceDuration = 0.18f;

        [Header("Squeaker")]
        [Range(0f, 1f)] [SerializeField] private float _squeekerChance = 0.25f;
        [Range(0.01f, 0.95f)] [SerializeField] private float _squeekerEdgeOffset = 0.85f;
        [Min(0.2f)] [SerializeField] private float _squeekerCrawlDuration = 0.7f;

        [Header("Tick Audio")]
        [Range(0f, 1f)] [SerializeField] private float _tickSliceOffset = 0.5f;
        [Range(-15f, 15f)] [SerializeField] private float _tickAngleOffsetDegrees;
        [Min(0f)] [SerializeField] private float _tickPitchStep = 0.04f;
        [Min(1)] [SerializeField] private int _tickPitchCycle = 3;

        [Header("Spin Button Idle")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private RectTransform _spinButtonPulseTarget;
        [Min(1f)] [SerializeField] private float _spinButtonIdleScaleMultiplier = 1.08f;
        [Min(0.1f)] [SerializeField] private float _spinButtonIdlePulseDuration = 0.8f;
        [SerializeField] private Ease _spinButtonIdlePulseEase = Ease.InOutSine;

        [Header("Wheel Idle Rotation")]
        [SerializeField] private float _idleRotationSpeedDegreesPerSecond = 8f;

        [Header("Post Spin Reveal")]
        [Min(0f)] [SerializeField] private float _replaceSmokeChainInterval = 0.08f;
        [Min(0f)] [SerializeField] private float _replaceSwapDelay = 0.1f;
        [Range(0f, 360f)] [SerializeField] private float _replaceChainStartAngle = 90f;

        [Header("Reward Ghost")]
        [SerializeField] private RectTransform _ghostEffectsRoot;
        [SerializeField] private RouletteRewardSliceUI _rewardGhostPrefab;
        [Min(0f)] [SerializeField] private float _rewardGhostRiseDistance = 110f;
        [Min(0.05f)] [SerializeField] private float _rewardGhostDuration = 0.45f;
        [Min(0.05f)] [SerializeField] private float _rewardGhostFadeDuration = 0.65f;
        [Min(1f)] [SerializeField] private float _rewardGhostEndScale = 1.3f;
        [SerializeField] private Ease _rewardGhostMoveEase = Ease.OutQuad;
        [SerializeField] private Ease _rewardGhostScaleEase = Ease.OutCubic;
        [SerializeField] private Ease _rewardGhostFadeEase = Ease.InQuad;

        private readonly List<RouletteRewardSliceUI> _spawnedSlices = new List<RouletteRewardSliceUI>();

        private Sequence _spinSequence;
        private Sequence _postSpinRevealSequence;
        private Sequence _rewardGhostSequence;
        private Tween _spinButtonIdleTween;
        private float _currentRotationDegrees;
        private RouletteResolvedWheel _lastWheel;
        private float _indicatorVelocity;
        private float _indicatorAngle;
        private float _prevAnimatedRotation;
        private Vector3 _spinButtonIdleBaseScale = Vector3.one;
        private bool _hasSpinButtonIdleBaseScale;
        private bool _wheelIdleRotationActive;
        private RouletteRewardSliceUI _rewardGhostInstance;
        private CanvasGroup _rewardGhostCanvasGroup;

        public bool IsPostSpinRevealPending => _postSpinRevealSequence != null && _postSpinRevealSequence.IsActive();

        private void OnEnable()
        {
            _rootRect ??= GetComponent<RectTransform>();
            _wheelRotatorRect ??= _rootRect;
            _sliceRootRect ??= _wheelRotatorRect;
            _spinButton ??= GetComponentInChildren<Button>(true);
            _spinButtonPulseTarget ??= _spinButton != null ? _spinButton.transform as RectTransform : null;
            _ghostEffectsRoot ??= _rootRect;
            CacheSpinButtonIdleBaseScale();
            _wheelIdleRotationActive = false;
            StopSpinButtonIdleAnimation(resetScale: true);
        }

        public void BuildWheel(RouletteResolvedWheel wheel, bool preserveRotation = true)
        {
            _lastWheel = wheel;
            ApplyWheelBackground(wheel);

            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _rewardSlicePrefab == null || _sliceRootRect == null)
            {
                ClearSlices();

                if (!preserveRotation)
                    SetWheelRotation(0f);

                return;
            }

            EnsureSliceViewCount(wheel.Slices.Count);
            RefreshSliceViews(wheel);

            if (!preserveRotation)
                SetWheelRotation(0f);
        }

        private void RefreshSliceViews(RouletteResolvedWheel wheel)
        {
            if (wheel == null || wheel.Slices == null)
                return;

            for (int i = 0; i < wheel.Slices.Count && i < _spawnedSlices.Count; i++)
                BindSliceView(_spawnedSlices[i], i, wheel.Slices[i], wheel.Slices.Count);
        }

        public void StopAnimation()
        {
            if (_spinSequence != null && _spinSequence.IsActive())
                _spinSequence.Kill();

            if (_postSpinRevealSequence != null && _postSpinRevealSequence.IsActive())
                _postSpinRevealSequence.Kill();

            _spinSequence = null;
            _postSpinRevealSequence = null;
            SetIndicatorRotation(0f);
            StopRewardGhost();
        }

        public void ResetWheelRotation()
        {
            StopAnimation();
            SetWheelRotation(0f);
        }

        public void PlaySpin(RouletteResolvedWheel wheel, int targetSliceIndex, System.Action onComplete)
        {
            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _wheelRotatorRect == null)
            {
                onComplete?.Invoke();
                return;
            }

            var wheelDefinition = wheel.Definition;
            _wheelIdleRotationActive = false;
            StopSpinButtonIdleAnimation(resetScale: true);
            StopAnimation();

            float sliceAngle = 360f / wheel.Slices.Count;
            float currentNormalizedRotation = Mathf.Repeat(_currentRotationDegrees, 360f);
            int clampedTargetSliceIndex = Mathf.Clamp(targetSliceIndex, 0, wheel.Slices.Count - 1);
            float targetNormalizedRotation = Mathf.Repeat(clampedTargetSliceIndex * sliceAngle, 360f);
            float deltaToTarget = Mathf.Repeat(targetNormalizedRotation - currentNormalizedRotation, 360f);
            float endRotation = _currentRotationDegrees + (wheelDefinition.FullRotations * 360f) + deltaToTarget;
            bool isSqueeker = Random.value < _squeekerChance;

            float animatedRotation = _currentRotationDegrees;
            int lastTickStep = CalculateTickStep(animatedRotation, sliceAngle);
            _prevAnimatedRotation = animatedRotation;
            _indicatorAngle = 0f;
            _indicatorVelocity = 0f;

            PlayUISound(SpinStartSoundName);

            System.Action<float> onTweenUpdate = value =>
            {
                animatedRotation = value;
                SetWheelRotation(value);
                UpdateIndicatorSway(value);
                EmitSliceTicks(ref lastTickStep, sliceAngle, value, wheel.Slices.Count);
            };

            DOGetter<float> tweenGetter = () => animatedRotation;
            DOSetter<float> tweenSetter = v => onTweenUpdate(v);

            _spinSequence = DOTween.Sequence();
            _spinSequence.Append(_wheelRotatorRect.DOScale(wheelDefinition.StartScale, wheelDefinition.StartScaleDuration).SetEase(wheelDefinition.ScaleEase));

            if (isSqueeker)
            {
                float edgeOffset = sliceAngle * _squeekerEdgeOffset;
                float pauseRotation = endRotation - edgeOffset;

                Tween spinTween = DOTween.To(tweenGetter, tweenSetter, pauseRotation, wheelDefinition.SpinDuration)
                    .SetEase(wheelDefinition.SpinEase);
                _spinSequence.Join(spinTween);

                Tween crawlTween = DOTween.To(tweenGetter, tweenSetter, endRotation, _squeekerCrawlDuration)
                    .SetEase(Ease.InOutSine);
                _spinSequence.Append(crawlTween);
            }
            else
            {
                float overshootDegrees = ComputeRandomizedOvershoot();

                Tween mainRotationTween = DOTween.To(tweenGetter, tweenSetter,
                        endRotation + overshootDegrees, wheelDefinition.SpinDuration)
                    .SetEase(wheelDefinition.SpinEase);
                _spinSequence.Join(mainRotationTween);

                AppendSettleBounces(_spinSequence, tweenGetter, tweenSetter,
                    endRotation, overshootDegrees, wheelDefinition.SettleDuration, wheelDefinition.SettleEase);
            }

            _spinSequence.Append(_wheelRotatorRect.DOScale(1f, wheelDefinition.EndScaleDuration).SetEase(wheelDefinition.ScaleEase));
            _spinSequence.OnComplete(() =>
            {
                _currentRotationDegrees = endRotation;
                SetWheelRotation(_currentRotationDegrees);
                SetIndicatorRotation(0f);
                PlayUISound(SpinStopSoundName);
                onComplete?.Invoke();
            });
            _spinSequence.OnKill(() => _spinSequence = null);
        }

        public void PlayPostSpinReveal(
            RouletteResolvedWheel nextWheel,
            int winningSliceIndex,
            RouletteResolvedSlice winningSlice,
            System.Action onComplete = null)
        {
            if (nextWheel == null)
                return;

            if (_postSpinRevealSequence != null && _postSpinRevealSequence.IsActive())
                _postSpinRevealSequence.Kill();

            StopRewardGhost();

            float revealDelay = ResolvePostSpinRevealDelay();
            if (revealDelay <= 0f || _spawnedSlices.Count == 0)
            {
                BuildWheel(nextWheel, preserveRotation: true);
                onComplete?.Invoke();
                return;
            }

            int clampedWinningSliceIndex = Mathf.Clamp(winningSliceIndex, 0, _spawnedSlices.Count - 1);
            PlayWinningSliceGhost(clampedWinningSliceIndex, winningSlice);

            _postSpinRevealSequence = DOTween.Sequence()
                .OnComplete(() =>
                {
                    FinalizePostSpinReveal(nextWheel);
                    _postSpinRevealSequence = null;
                    onComplete?.Invoke();
                })
                .OnKill(() => _postSpinRevealSequence = null);

            if (revealDelay > 0f)
                _postSpinRevealSequence.AppendInterval(revealDelay);

            AppendSliceReplaceChain(_postSpinRevealSequence, nextWheel);
        }

        public void SetIdlePresentationActive(bool isButtonIdleActive, bool isWheelIdleRotationActive)
        {
            _wheelIdleRotationActive = isWheelIdleRotationActive;

            if (isButtonIdleActive)
            {
                StartSpinButtonIdleAnimation();
                return;
            }

            StopSpinButtonIdleAnimation(resetScale: true);
        }

        private void Update()
        {
            if (!_wheelIdleRotationActive || _wheelRotatorRect == null || (_spinSequence != null && _spinSequence.IsActive()))
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            SetWheelRotation(_currentRotationDegrees + (_idleRotationSpeedDegreesPerSecond * deltaTime));
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_lastWheel == null || _spawnedSlices.Count == 0)
                return;

            RelayoutSlices(_lastWheel);
        }

        private void OnDestroy()
        {
            StopSpinButtonIdleAnimation(resetScale: true);
            StopAnimation();
        }

        private void OnValidate()
        {
            _rootRect ??= GetComponent<RectTransform>();
            _wheelRotatorRect ??= _rootRect;
            _sliceRootRect ??= _wheelRotatorRect;
            _spinButton ??= GetComponentInChildren<Button>(true);
            _spinButtonPulseTarget ??= _spinButton != null ? _spinButton.transform as RectTransform : null;
            _ghostEffectsRoot ??= _rootRect;
            CacheSpinButtonIdleBaseScale();
        }

        private void ClearSlices()
        {
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

        private void EnsureSliceViewCount(int targetCount)
        {
            for (int i = _spawnedSlices.Count - 1; i >= targetCount; i--)
                DestroySliceAt(i);

            while (_spawnedSlices.Count < targetCount)
            {
                RouletteRewardSliceUI sliceView = Instantiate(_rewardSlicePrefab, _sliceRootRect);
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
                Destroy(sliceView.gameObject);
            else
                DestroyImmediate(sliceView.gameObject);
        }

        private void ApplyWheelBackground(RouletteResolvedWheel wheel)
        {
            if (_wheelBackgroundImage == null)
                return;

            RouletteWheelData wheelDefinition = wheel != null ? wheel.Definition : null;
            Sprite backgroundSprite = wheelDefinition != null ? wheelDefinition.WheelBackground : null;
            _wheelBackgroundImage.sprite = backgroundSprite;
            _wheelBackgroundImage.enabled = backgroundSprite != null;
            if (_rouletteIndicatorImage != null)
            {
                Sprite indicatorSprite = wheelDefinition != null ? wheelDefinition.RouletteIndicator : null;
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

        private void EmitSliceTicks(ref int lastTickStep, float sliceAngle, float rotationDegrees, int sliceCount)
        {
            int currentStep = CalculateTickStep(rotationDegrees, sliceAngle);
            if (currentStep == lastTickStep)
                return;

            int stepDirection = currentStep > lastTickStep ? 1 : -1;
            for (int step = lastTickStep + stepDirection; step != currentStep + stepDirection; step += stepDirection)
                PlayTickSound(stepDirection > 0 ? step : step + 1, sliceCount);

            lastTickStep = currentStep;
        }

        private int CalculateTickStep(float rotationDegrees, float sliceAngle)
        {
            float tickOffsetDegrees = (sliceAngle * _tickSliceOffset) + _tickAngleOffsetDegrees;
            return Mathf.FloorToInt((rotationDegrees + tickOffsetDegrees) / sliceAngle);
        }

        private void PlayTickSound(int step, int sliceCount)
        {
            if (sliceCount <= 0)
                return;

            int sliceIndex = ((step % sliceCount) + sliceCount) % sliceCount;
            int pitchCycle = Mathf.Max(1, _tickPitchCycle);
            float pitchMultiplier = 1f + ((sliceIndex % pitchCycle) * _tickPitchStep);
            PlayUISound(SpinTickSoundName, pitchMultiplier);
        }

        private float ComputeRandomizedOvershoot()
        {
            return Random.Range(_overshootMin, Mathf.Max(_overshootMin, _overshootMax));
        }

        private void AppendSettleBounces(Sequence seq, DOGetter<float> getter, DOSetter<float> setter,
            float endRotation, float overshootDegrees, float settleDuration, Ease settleEase)
        {
            float currentOvershoot = overshootDegrees;
            float bounceTarget = endRotation;

            seq.Append(DOTween.To(getter, setter, bounceTarget, settleDuration).SetEase(settleEase));

            int extraBounces = _maxExtraBounces > 0 ? Random.Range(0, _maxExtraBounces + 1) : 0;
            for (int i = 0; i < extraBounces; i++)
            {
                float bounceMagnitude = currentOvershoot * Random.Range(0.15f, 0.35f);
                float sign = (i % 2 == 0) ? -1f : 1f;
                float bounceOvershoot = bounceTarget + sign * bounceMagnitude;

                float bounceDur = _extraBounceDuration * (1f - (i * 0.3f));
                bounceDur = Mathf.Max(0.06f, bounceDur);

                seq.Append(DOTween.To(getter, setter, bounceOvershoot, bounceDur).SetEase(Ease.OutQuad));
                seq.Append(DOTween.To(getter, setter, bounceTarget, bounceDur * 0.7f).SetEase(Ease.InOutSine));

                currentOvershoot = bounceMagnitude;
            }
        }

        private void UpdateIndicatorSway(float currentAnimatedRotation)
        {
            if (_rouletteIndicatorImage == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float angularVelocity = (currentAnimatedRotation - _prevAnimatedRotation) / dt;
            _prevAnimatedRotation = currentAnimatedRotation;

            float normalizedDrive = Mathf.Clamp01(Mathf.Abs(angularVelocity) / 1500f);
            float targetAngle = Mathf.Sin(Time.time * _indicatorSwayFrequency) * _indicatorSwayMaxAngle * normalizedDrive;

            float springForce = (targetAngle - _indicatorAngle) * _indicatorSwayFrequency;
            _indicatorVelocity += springForce * dt;
            _indicatorVelocity *= Mathf.Exp(-_indicatorSwayDamping * dt);
            _indicatorAngle += _indicatorVelocity * dt;

            SetIndicatorRotation(_indicatorAngle);
        }

        private void SetIndicatorRotation(float angle)
        {
            if (_rouletteIndicatorImage != null)
                _rouletteIndicatorImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SetWheelRotation(float rotationDegrees)
        {
            _currentRotationDegrees = rotationDegrees;

            if (_wheelRotatorRect != null)
                _wheelRotatorRect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        private void StartSpinButtonIdleAnimation()
        {
            RectTransform pulseTarget = ResolveSpinButtonPulseTarget();
            if (pulseTarget == null || _spinButton == null || !_spinButton.IsInteractable() || !_spinButton.gameObject.activeInHierarchy)
            {
                StopSpinButtonIdleAnimation(resetScale: true);
                return;
            }

            CacheSpinButtonIdleBaseScale();

            if (_spinButtonIdleTween != null && _spinButtonIdleTween.IsActive())
                return;

            pulseTarget.localScale = _spinButtonIdleBaseScale;
            _spinButtonIdleTween = pulseTarget.DOScale(_spinButtonIdleBaseScale * _spinButtonIdleScaleMultiplier, _spinButtonIdlePulseDuration)
                .SetEase(_spinButtonIdlePulseEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(pulseTarget.gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _spinButtonIdleTween = null);
        }

        private void StopSpinButtonIdleAnimation(bool resetScale)
        {
            if (_spinButtonIdleTween != null && _spinButtonIdleTween.IsActive())
                _spinButtonIdleTween.Kill();

            _spinButtonIdleTween = null;

            if (!resetScale)
                return;

            RectTransform pulseTarget = ResolveSpinButtonPulseTarget();
            if (pulseTarget != null && _hasSpinButtonIdleBaseScale)
                pulseTarget.localScale = _spinButtonIdleBaseScale;
        }

        private RectTransform ResolveSpinButtonPulseTarget()
        {
            if (_spinButtonPulseTarget != null)
                return _spinButtonPulseTarget;

            return _spinButton != null ? _spinButton.transform as RectTransform : null;
        }

        private void CacheSpinButtonIdleBaseScale()
        {
            RectTransform pulseTarget = ResolveSpinButtonPulseTarget();
            if (pulseTarget == null)
                return;

            _spinButtonIdleBaseScale = pulseTarget.localScale;
            _hasSpinButtonIdleBaseScale = true;
        }

        private void PlayWinningSliceGhost(int sliceIndex, RouletteResolvedSlice slice)
        {
            if (sliceIndex < 0 || sliceIndex >= _spawnedSlices.Count)
                return;

            RouletteRewardSliceUI sourceSliceView = _spawnedSlices[sliceIndex];
            if (sourceSliceView == null)
                return;

            PlayRewardGhost(sourceSliceView, slice, ResolveRarityColor(slice));
        }

        private float ResolvePostSpinRevealDelay()
        {
            if (App.Game == null || App.Game.Config == null || App.Game.Config.RouletteConfig == null)
                return 0f;

            return App.Game.Config.RouletteConfig.PostSpinRevealDelay;
        }

        private void AppendSliceReplaceChain(Sequence sequence, RouletteResolvedWheel nextWheel)
        {
            if (sequence == null || nextWheel == null || nextWheel.Slices == null || nextWheel.Slices.Count == 0)
                return;

            float chainInterval = Mathf.Max(0f, _replaceSmokeChainInterval);
            float swapDelay = Mathf.Max(0f, _replaceSwapDelay);
            int swapCount = Mathf.Min(_spawnedSlices.Count, nextWheel.Slices.Count);
            if (swapCount <= 0)
                return;

            float baseTime = sequence.Duration(false);
            float totalDuration = ((swapCount - 1) * chainInterval) + swapDelay;
            if (totalDuration > 0f)
                sequence.AppendInterval(totalDuration);

            int startIndex = ResolveReplaceChainStartIndex(swapCount);
            for (int orderIndex = 0; orderIndex < swapCount; orderIndex++)
            {
                int capturedIndex = (startIndex + orderIndex) % swapCount;
                RouletteRewardSliceUI sliceView = _spawnedSlices[capturedIndex];
                if (sliceView == null)
                    continue;

                RouletteRewardSliceUI capturedSliceView = sliceView;
                RouletteResolvedSlice capturedSlice = nextWheel.Slices[capturedIndex];
                float triggerTime = baseTime + (orderIndex * chainInterval);
                float bindTime = triggerTime + swapDelay;

                sequence.InsertCallback(triggerTime, () => TriggerSliceReplaceSmoke(capturedSliceView));
                sequence.InsertCallback(bindTime, () => BindSliceView(capturedSliceView, capturedIndex, capturedSlice, nextWheel.Slices.Count));
            }
        }

        private int ResolveReplaceChainStartIndex(int sliceCount)
        {
            if (sliceCount <= 1)
                return 0;

            float sliceAngle = 360f / sliceCount;
            int closestIndex = 0;
            float closestDelta = float.MaxValue;

            for (int i = 0; i < sliceCount; i++)
            {
                float visibleAngle = Mathf.Repeat((90f - (i * sliceAngle)) + _currentRotationDegrees, 360f);
                float delta = Mathf.Abs(Mathf.DeltaAngle(visibleAngle, _replaceChainStartAngle));
                if (delta < closestDelta)
                {
                    closestDelta = delta;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void TriggerSliceReplaceSmoke(RouletteRewardSliceUI sliceView)
        {
            if (sliceView == null)
                return;

            if (sliceView.ReplaceSmokeDuration > 0f)
            {
                sliceView.PlayReplaceSmoke();
                PlayUISound(SmokeSoundName);
            }
        }

        private void PlayRewardGhost(RouletteRewardSliceUI sourceSliceView, RouletteResolvedSlice slice, Color rarityColor)
        {
            if (_ghostEffectsRoot == null || _rewardGhostPrefab == null || sourceSliceView == null)
                return;

            RouletteRewardSliceUI ghost = GetOrCreateRewardGhost();
            if (ghost == null)
                return;

            RectTransform sourceRect = sourceSliceView.RootRect;
            RectTransform ghostRect = ghost.RootRect;
            if (sourceRect == null || ghostRect == null)
                return;

            StopRewardGhost();
            ghost.Bind(slice, rarityColor);
            ghost.gameObject.SetActive(true);
            ghostRect.SetAsLastSibling();
            ghostRect.localRotation = Quaternion.identity;

            Vector2 sourceAnchoredPosition = ResolveAnchoredPositionInGhostRoot(sourceRect);
            ghostRect.anchoredPosition = sourceAnchoredPosition;
            ghostRect.localScale = Vector3.one;

            if (_rewardGhostCanvasGroup != null)
                _rewardGhostCanvasGroup.alpha = 1f;

            _rewardGhostSequence = DOTween.Sequence()
                .SetLink(ghost.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (ghost != null)
                        ghost.gameObject.SetActive(false);

                    _rewardGhostSequence = null;
                })
                .OnKill(() => _rewardGhostSequence = null);

            _rewardGhostSequence.Join(
                ghostRect.DOAnchorPosY(sourceAnchoredPosition.y + _rewardGhostRiseDistance, _rewardGhostDuration)
                    .SetEase(_rewardGhostMoveEase));

            _rewardGhostSequence.Join(
                ghostRect.DOScale(_rewardGhostEndScale, _rewardGhostDuration)
                    .SetEase(_rewardGhostScaleEase));

            if (_rewardGhostCanvasGroup != null)
            {
                _rewardGhostSequence.Join(
                    _rewardGhostCanvasGroup.DOFade(0f, _rewardGhostFadeDuration)
                        .SetEase(_rewardGhostFadeEase));
            }
        }

        private void StopRewardGhost()
        {
            if (_rewardGhostSequence != null && _rewardGhostSequence.IsActive())
                _rewardGhostSequence.Kill();

            _rewardGhostSequence = null;

            if (_rewardGhostInstance != null)
                _rewardGhostInstance.gameObject.SetActive(false);
        }

        private RouletteRewardSliceUI GetOrCreateRewardGhost()
        {
            if (_rewardGhostInstance != null)
                return _rewardGhostInstance;

            _rewardGhostInstance = Instantiate(_rewardGhostPrefab, _ghostEffectsRoot);
            _rewardGhostInstance.gameObject.SetActive(false);
            _rewardGhostCanvasGroup = _rewardGhostInstance.GetComponent<CanvasGroup>();
            if (_rewardGhostCanvasGroup == null)
                _rewardGhostCanvasGroup = _rewardGhostInstance.gameObject.AddComponent<CanvasGroup>();

            return _rewardGhostInstance;
        }

        private Vector2 ResolveAnchoredPositionInGhostRoot(RectTransform sourceRect)
        {
            Vector3 worldPoint = sourceRect.TransformPoint(sourceRect.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostEffectsRoot, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        private static Color ResolveRarityColor(RouletteResolvedSlice slice)
        {
            return slice.Reward.HasReward && App.Game != null
                ? App.Game.Rewards.GetRarityColor(slice.Reward.Rarity, Color.white)
                : Color.white;
        }

        private void BindSliceView(RouletteRewardSliceUI sliceView, int index, RouletteResolvedSlice slice, int sliceCount)
        {
            if (sliceView == null || sliceCount <= 0)
                return;

            float sliceAngle = 360f / sliceCount;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;
            sliceView.Bind(slice, ResolveRarityColor(slice));
            LayoutSlice(sliceView.RootRect, index, sliceAngle, radius, sliceSize);
        }

        private void FinalizePostSpinReveal(RouletteResolvedWheel nextWheel)
        {
            _lastWheel = nextWheel;
            ApplyWheelBackground(nextWheel);

            if (nextWheel == null || nextWheel.Slices == null)
            {
                ClearSlices();
                return;
            }

            if (nextWheel.Slices.Count != _spawnedSlices.Count)
            {
                BuildWheel(nextWheel, preserveRotation: true);
                return;
            }

            float sliceAngle = 360f / nextWheel.Slices.Count;
            float diameter = ResolveWheelDiameter();
            float radius = Mathf.Max(0f, (diameter * 0.5f) - (diameter * _sliceRadiusPaddingRatio));
            float sliceSize = diameter * _sliceSizeRatio;

            for (int i = 0; i < _spawnedSlices.Count; i++)
                LayoutSlice(_spawnedSlices[i]?.RootRect, i, sliceAngle, radius, sliceSize);
        }

        private static void PlayUISound(string soundName, float pitchMultiplier = 1f)
        {
            if (App.Sound == null)
                return;

            App.Sound.PlaySound(soundName, isUI: true, pitchMultiplier: pitchMultiplier);
        }
    }
}
