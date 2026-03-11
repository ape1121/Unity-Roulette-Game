using Ape.Core;
using Ape.Data;
using Ape.Sounds;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RouletteWheelUI : MonoBehaviour
    {
        private RewardManager _rewardManager;
        private RoulettePresentationConfig _presentationConfig;
        private SoundManager _soundManager;
        private float _postSpinRevealDelay;

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

        [Header("Slow Spin Audio")]
        [Min(0.25f)] [SerializeField] private float _slowSpinExcitementTriggerSlices = 2.5f;

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

        private readonly RouletteWheelLayoutController _layoutController = new RouletteWheelLayoutController();
        private readonly RouletteWheelSpinAnimator _spinAnimator = new RouletteWheelSpinAnimator();
        private readonly RouletteWheelRevealController _revealController = new RouletteWheelRevealController();

        private RouletteResolvedWheel _lastWheel;

        public bool IsPostSpinRevealPending => _revealController.IsRevealPending;

        public void SetPresentationContext(
            RoulettePresentationConfig presentationConfig,
            float postSpinRevealDelay,
            RewardManager rewardManager,
            SoundManager soundManager)
        {
            _presentationConfig = presentationConfig;
            _postSpinRevealDelay = Mathf.Max(0f, postSpinRevealDelay);
            _rewardManager = rewardManager;
            _soundManager = soundManager;
            ConfigureControllers();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ConfigureControllers();
            _spinAnimator.Initialize();
        }

        public void BuildWheel(RouletteResolvedWheel wheel, bool preserveRotation = true)
        {
            _lastWheel = wheel;
            _layoutController.BuildWheel(wheel, ResolveRarityColor);

            if (!preserveRotation)
                _spinAnimator.SetRotation(0f);
        }

        public void StopAnimation()
        {
            _spinAnimator.StopSpin();
            _revealController.StopAnimation();
        }

        public void ResetWheelRotation()
        {
            StopAnimation();
            _spinAnimator.SetRotation(0f);
        }

        public void PlaySpin(RouletteResolvedWheel wheel, int targetSliceIndex, System.Action onComplete)
        {
            if (wheel == null || wheel.Slices == null || wheel.Slices.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _spinAnimator.SetIdlePresentationActive(false, false);
            _revealController.StopAnimation();
            _spinAnimator.PlaySpin(wheel, targetSliceIndex, onComplete);
        }

        public void PlayPostSpinReveal(
            RouletteResolvedWheel nextWheel,
            int winningSliceIndex,
            RouletteResolvedSlice winningSlice,
            System.Action onComplete = null)
        {
            if (nextWheel == null)
                return;

            _revealController.PlayPostSpinReveal(
                nextWheel,
                winningSliceIndex,
                winningSlice,
                ResolvePostSpinRevealDelay(),
                _spinAnimator.CurrentRotationDegrees,
                ResolveRarityColor,
                wheel => _lastWheel = wheel,
                onComplete);
        }

        public void SetIdlePresentationActive(bool isButtonIdleActive, bool isWheelIdleRotationActive)
        {
            _spinAnimator.SetIdlePresentationActive(isButtonIdleActive, isWheelIdleRotationActive);
        }

        private void Update()
        {
            _spinAnimator.Tick(Time.deltaTime);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_lastWheel == null || !_layoutController.HasSlices)
                return;

            _layoutController.Relayout(_lastWheel);
        }

        private void OnDestroy()
        {
            _spinAnimator.Dispose();
            _revealController.StopAnimation();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ConfigureControllers();
            _spinAnimator.CaptureIdleBaseScale();
        }

        private float ResolvePostSpinRevealDelay()
        {
            return _postSpinRevealDelay;
        }

        private void ResolveReferences()
        {
            _rootRect ??= GetComponent<RectTransform>();
            _wheelRotatorRect ??= _rootRect;
            _sliceRootRect ??= _wheelRotatorRect;
            _spinButton ??= GetComponentInChildren<Button>(true);
            _spinButtonPulseTarget ??= _spinButton != null ? _spinButton.transform as RectTransform : null;
            _ghostEffectsRoot ??= _rootRect;
        }

        private void ConfigureControllers()
        {
            _layoutController.Configure(
                _sliceRootRect,
                _wheelBackgroundImage,
                _rouletteIndicatorImage,
                _rewardSlicePrefab,
                _useBackgroundShortestDimension,
                _fallbackDiameter,
                _sliceRadiusPaddingRatio,
                _sliceSizeRatio);

            _spinAnimator.Configure(
                _wheelRotatorRect,
                _rouletteIndicatorImage,
                _spinButton,
                _spinButtonPulseTarget,
                _indicatorSwayMaxAngle,
                _indicatorSwayFrequency,
                _indicatorSwayDamping,
                _overshootMin,
                _overshootMax,
                _maxExtraBounces,
                _extraBounceDuration,
                _squeekerChance,
                _squeekerEdgeOffset,
                _squeekerCrawlDuration,
                _tickSliceOffset,
                _tickAngleOffsetDegrees,
                _tickPitchStep,
                _tickPitchCycle,
                _spinButtonIdleScaleMultiplier,
                _spinButtonIdlePulseDuration,
                _spinButtonIdlePulseEase,
                _idleRotationSpeedDegreesPerSecond,
                _slowSpinExcitementTriggerSlices,
                _presentationConfig != null ? _presentationConfig.SpinStartSound : null,
                _presentationConfig != null ? _presentationConfig.SpinTickSound : null,
                _presentationConfig != null ? _presentationConfig.SpinSlowExcitementSound : null,
                _presentationConfig != null ? _presentationConfig.SpinStopSound : null,
                _soundManager);

            _revealController.Configure(
                _layoutController,
                _ghostEffectsRoot,
                _rewardGhostPrefab,
                _replaceSmokeChainInterval,
                _replaceSwapDelay,
                _replaceChainStartAngle,
                _rewardGhostRiseDistance,
                _rewardGhostDuration,
                _rewardGhostFadeDuration,
                _rewardGhostEndScale,
                _rewardGhostMoveEase,
                _rewardGhostScaleEase,
                _rewardGhostFadeEase,
                _presentationConfig != null ? _presentationConfig.ReplaceSmokeSound : null,
                _soundManager);
        }

        private Color ResolveRarityColor(RouletteResolvedSlice slice)
        {
            return slice.Reward.HasReward && _rewardManager != null
                ? _rewardManager.GetRarityColor(slice.Reward.Rarity, Color.white)
                : Color.white;
        }
    }
}
