using System;
using Ape.Core;
using Ape.Data;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    public sealed class RouletteWheelSpinAnimator
    {
        private const string SpinStartSoundName = "roulette_spin_start";
        private const string SpinTickSoundName = "roulette_spin_tick";
        private const string SpinStopSoundName = "roulette_spin_stop";

        private RectTransform _wheelRotatorRect;
        private Image _rouletteIndicatorImage;
        private Button _spinButton;
        private RectTransform _spinButtonPulseTarget;
        private float _indicatorSwayMaxAngle = 15f;
        private float _indicatorSwayFrequency = 12f;
        private float _indicatorSwayDamping = 4f;
        private float _overshootMin = 4f;
        private float _overshootMax = 25f;
        private int _maxExtraBounces = 1;
        private float _extraBounceDuration = 0.18f;
        private float _squeekerChance = 0.25f;
        private float _squeekerEdgeOffset = 0.85f;
        private float _squeekerCrawlDuration = 0.7f;
        private float _tickSliceOffset = 0.5f;
        private float _tickAngleOffsetDegrees;
        private float _tickPitchStep = 0.04f;
        private int _tickPitchCycle = 3;
        private float _spinButtonIdleScaleMultiplier = 1.08f;
        private float _spinButtonIdlePulseDuration = 0.8f;
        private Ease _spinButtonIdlePulseEase = Ease.InOutSine;
        private float _idleRotationSpeedDegreesPerSecond = 8f;

        private Sequence _spinSequence;
        private Tween _spinButtonIdleTween;
        private float _currentRotationDegrees;
        private float _indicatorVelocity;
        private float _indicatorAngle;
        private float _prevAnimatedRotation;
        private Vector3 _spinButtonIdleBaseScale = Vector3.one;
        private bool _hasSpinButtonIdleBaseScale;
        private bool _wheelIdleRotationActive;

        public float CurrentRotationDegrees => _currentRotationDegrees;
        public bool IsSpinAnimationActive => _spinSequence != null && _spinSequence.IsActive();

        public void Configure(
            RectTransform wheelRotatorRect,
            Image rouletteIndicatorImage,
            Button spinButton,
            RectTransform spinButtonPulseTarget,
            float indicatorSwayMaxAngle,
            float indicatorSwayFrequency,
            float indicatorSwayDamping,
            float overshootMin,
            float overshootMax,
            int maxExtraBounces,
            float extraBounceDuration,
            float squeekerChance,
            float squeekerEdgeOffset,
            float squeekerCrawlDuration,
            float tickSliceOffset,
            float tickAngleOffsetDegrees,
            float tickPitchStep,
            int tickPitchCycle,
            float spinButtonIdleScaleMultiplier,
            float spinButtonIdlePulseDuration,
            Ease spinButtonIdlePulseEase,
            float idleRotationSpeedDegreesPerSecond)
        {
            _wheelRotatorRect = wheelRotatorRect;
            _rouletteIndicatorImage = rouletteIndicatorImage;
            _spinButton = spinButton;
            _spinButtonPulseTarget = spinButtonPulseTarget;
            _indicatorSwayMaxAngle = indicatorSwayMaxAngle;
            _indicatorSwayFrequency = indicatorSwayFrequency;
            _indicatorSwayDamping = indicatorSwayDamping;
            _overshootMin = overshootMin;
            _overshootMax = overshootMax;
            _maxExtraBounces = maxExtraBounces;
            _extraBounceDuration = extraBounceDuration;
            _squeekerChance = squeekerChance;
            _squeekerEdgeOffset = squeekerEdgeOffset;
            _squeekerCrawlDuration = squeekerCrawlDuration;
            _tickSliceOffset = tickSliceOffset;
            _tickAngleOffsetDegrees = tickAngleOffsetDegrees;
            _tickPitchStep = tickPitchStep;
            _tickPitchCycle = tickPitchCycle;
            _spinButtonIdleScaleMultiplier = spinButtonIdleScaleMultiplier;
            _spinButtonIdlePulseDuration = spinButtonIdlePulseDuration;
            _spinButtonIdlePulseEase = spinButtonIdlePulseEase;
            _idleRotationSpeedDegreesPerSecond = idleRotationSpeedDegreesPerSecond;
        }

        public void Initialize()
        {
            CaptureIdleBaseScale();
            _wheelIdleRotationActive = false;
            StopSpinButtonIdleAnimation(resetScale: true);
        }

        public void CaptureIdleBaseScale()
        {
            RectTransform pulseTarget = ResolveSpinButtonPulseTarget();
            if (pulseTarget == null)
                return;

            _spinButtonIdleBaseScale = pulseTarget.localScale;
            _hasSpinButtonIdleBaseScale = true;
        }

        public void Dispose()
        {
            StopSpinButtonIdleAnimation(resetScale: true);
            StopSpin();
        }

        public void StopSpin()
        {
            if (_spinSequence != null && _spinSequence.IsActive())
                _spinSequence.Kill();

            _spinSequence = null;
            SetIndicatorRotation(0f);
        }

        public void SetRotation(float rotationDegrees)
        {
            _currentRotationDegrees = rotationDegrees;

            if (_wheelRotatorRect != null)
                _wheelRotatorRect.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        public void PlaySpin(RouletteResolvedWheel wheel, int targetSliceIndex, Action onComplete)
        {
            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0 || _wheelRotatorRect == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopSpin();

            RouletteWheelData wheelDefinition = wheel.Definition;
            float sliceAngle = 360f / wheel.Slices.Count;
            float currentNormalizedRotation = Mathf.Repeat(_currentRotationDegrees, 360f);
            int clampedTargetSliceIndex = Mathf.Clamp(targetSliceIndex, 0, wheel.Slices.Count - 1);
            float targetNormalizedRotation = Mathf.Repeat(clampedTargetSliceIndex * sliceAngle, 360f);
            float deltaToTarget = Mathf.Repeat(targetNormalizedRotation - currentNormalizedRotation, 360f);
            float endRotation = _currentRotationDegrees + (wheelDefinition.FullRotations * 360f) + deltaToTarget;
            bool isSqueeker = UnityEngine.Random.value < _squeekerChance;

            float animatedRotation = _currentRotationDegrees;
            int lastTickStep = CalculateTickStep(animatedRotation, sliceAngle);
            _prevAnimatedRotation = animatedRotation;
            _indicatorAngle = 0f;
            _indicatorVelocity = 0f;

            PlayUISound(SpinStartSoundName);

            Action<float> onTweenUpdate = value =>
            {
                animatedRotation = value;
                SetRotation(value);
                UpdateIndicatorSway(value);
                EmitSliceTicks(ref lastTickStep, sliceAngle, value, wheel.Slices.Count);
            };

            DOGetter<float> tweenGetter = () => animatedRotation;
            DOSetter<float> tweenSetter = value => onTweenUpdate(value);

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
                Tween mainRotationTween = DOTween.To(tweenGetter, tweenSetter, endRotation + overshootDegrees, wheelDefinition.SpinDuration)
                    .SetEase(wheelDefinition.SpinEase);
                _spinSequence.Join(mainRotationTween);

                AppendSettleBounces(
                    _spinSequence,
                    tweenGetter,
                    tweenSetter,
                    endRotation,
                    overshootDegrees,
                    wheelDefinition.SettleDuration,
                    wheelDefinition.SettleEase);
            }

            _spinSequence.Append(_wheelRotatorRect.DOScale(1f, wheelDefinition.EndScaleDuration).SetEase(wheelDefinition.ScaleEase));
            _spinSequence.OnComplete(() =>
            {
                _currentRotationDegrees = endRotation;
                SetRotation(_currentRotationDegrees);
                SetIndicatorRotation(0f);
                PlayUISound(SpinStopSoundName);
                onComplete?.Invoke();
            });
            _spinSequence.OnKill(() => _spinSequence = null);
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

        public void Tick(float deltaTime)
        {
            if (!_wheelIdleRotationActive || _wheelRotatorRect == null || IsSpinAnimationActive || deltaTime <= 0f)
                return;

            SetRotation(_currentRotationDegrees + (_idleRotationSpeedDegreesPerSecond * deltaTime));
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
            return UnityEngine.Random.Range(_overshootMin, Mathf.Max(_overshootMin, _overshootMax));
        }

        private void AppendSettleBounces(
            Sequence sequence,
            DOGetter<float> getter,
            DOSetter<float> setter,
            float endRotation,
            float overshootDegrees,
            float settleDuration,
            Ease settleEase)
        {
            float currentOvershoot = overshootDegrees;
            float bounceTarget = endRotation;

            sequence.Append(DOTween.To(getter, setter, bounceTarget, settleDuration).SetEase(settleEase));

            int extraBounces = _maxExtraBounces > 0 ? UnityEngine.Random.Range(0, _maxExtraBounces + 1) : 0;
            for (int i = 0; i < extraBounces; i++)
            {
                float bounceMagnitude = currentOvershoot * UnityEngine.Random.Range(0.15f, 0.35f);
                float sign = i % 2 == 0 ? -1f : 1f;
                float bounceOvershoot = bounceTarget + (sign * bounceMagnitude);

                float bounceDuration = _extraBounceDuration * (1f - (i * 0.3f));
                bounceDuration = Mathf.Max(0.06f, bounceDuration);

                sequence.Append(DOTween.To(getter, setter, bounceOvershoot, bounceDuration).SetEase(Ease.OutQuad));
                sequence.Append(DOTween.To(getter, setter, bounceTarget, bounceDuration * 0.7f).SetEase(Ease.InOutSine));

                currentOvershoot = bounceMagnitude;
            }
        }

        private void UpdateIndicatorSway(float currentAnimatedRotation)
        {
            if (_rouletteIndicatorImage == null)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            float angularVelocity = (currentAnimatedRotation - _prevAnimatedRotation) / deltaTime;
            _prevAnimatedRotation = currentAnimatedRotation;

            float normalizedDrive = Mathf.Clamp01(Mathf.Abs(angularVelocity) / 1500f);
            float targetAngle = Mathf.Sin(Time.time * _indicatorSwayFrequency) * _indicatorSwayMaxAngle * normalizedDrive;

            float springForce = (targetAngle - _indicatorAngle) * _indicatorSwayFrequency;
            _indicatorVelocity += springForce * deltaTime;
            _indicatorVelocity *= Mathf.Exp(-_indicatorSwayDamping * deltaTime);
            _indicatorAngle += _indicatorVelocity * deltaTime;

            SetIndicatorRotation(_indicatorAngle);
        }

        private void SetIndicatorRotation(float angle)
        {
            if (_rouletteIndicatorImage != null)
                _rouletteIndicatorImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void StartSpinButtonIdleAnimation()
        {
            RectTransform pulseTarget = ResolveSpinButtonPulseTarget();
            if (pulseTarget == null || _spinButton == null || !_spinButton.IsInteractable() || !_spinButton.gameObject.activeInHierarchy)
            {
                StopSpinButtonIdleAnimation(resetScale: true);
                return;
            }

            CaptureIdleBaseScale();

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

        private static void PlayUISound(string soundName, float pitchMultiplier = 1f)
        {
            if (App.Sound == null)
                return;

            App.Sound.PlaySound(soundName, isUI: true, pitchMultiplier: pitchMultiplier);
        }
    }
}
