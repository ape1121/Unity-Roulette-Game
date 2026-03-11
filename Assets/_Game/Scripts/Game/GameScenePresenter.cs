using Ape.Core;
using UnityEngine;

namespace Ape.Game
{
    [DisallowMultipleComponent]
    public sealed class GameScenePresenter : MonoBehaviour
    {
        [SerializeField] private RouletteWheelUI _rouletteWheel;
        [SerializeField] private GameUIManager _uiManager;
        [SerializeField] private GameUIEffects _effects;

        private GameManager _gameManager;

        private void OnDisable()
        {
            Unbind();
        }

        private void OnValidate()
        {
            _uiManager ??= GetComponentInChildren<GameUIManager>(true);
            _rouletteWheel ??= GetComponentInChildren<RouletteWheelUI>(true);

            if (_effects == null)
                _effects = _uiManager != null ? _uiManager.Effects : GetComponentInChildren<GameUIEffects>(true);
        }

        public void Bind(GameManager gameManager)
        {
            if (gameManager == null)
                return;

            if (_gameManager == gameManager)
                return;

            Unbind();
            _gameManager = gameManager;
            _gameManager.WheelBuildRequested += HandleWheelBuildRequested;
            _gameManager.SpinPresentationRequested += HandleSpinPresentationRequested;
            _gameManager.SpinRevealPresentationRequested += HandleSpinRevealPresentationRequested;
            _gameManager.FeedbackRequested += HandleFeedbackRequested;
            _gameManager.WheelAnimationStopRequested += HandleWheelAnimationStopRequested;
            _gameManager.WheelRotationResetRequested += HandleWheelRotationResetRequested;
        }

        public void Unbind()
        {
            if (_gameManager == null)
                return;

            _gameManager.WheelBuildRequested -= HandleWheelBuildRequested;
            _gameManager.SpinPresentationRequested -= HandleSpinPresentationRequested;
            _gameManager.SpinRevealPresentationRequested -= HandleSpinRevealPresentationRequested;
            _gameManager.FeedbackRequested -= HandleFeedbackRequested;
            _gameManager.WheelAnimationStopRequested -= HandleWheelAnimationStopRequested;
            _gameManager.WheelRotationResetRequested -= HandleWheelRotationResetRequested;
            _gameManager = null;
        }

        private void HandleWheelBuildRequested(GameWheelBuildRequest request)
        {
            if (_rouletteWheel != null)
                _rouletteWheel.BuildWheel(request.Wheel, request.PreserveRotation);
        }

        private void HandleSpinPresentationRequested(GameSpinPresentationRequest request)
        {
            if (_rouletteWheel == null)
            {
                request.Complete();
                return;
            }

            _rouletteWheel.PlaySpin(request.Wheel, request.TargetSliceIndex, request.Complete);
        }

        private void HandleSpinRevealPresentationRequested(GameSpinRevealPresentationRequest request)
        {
            if (_rouletteWheel == null)
            {
                request.Complete();
                return;
            }

            _rouletteWheel.PlayPostSpinReveal(
                request.NextWheel,
                request.SelectedSliceIndex,
                request.SelectedSlice,
                request.Complete);
        }

        private void HandleFeedbackRequested(GameFeedbackRequest request)
        {
            switch (request.Type)
            {
                case GameFeedbackType.SpinStartShake:
                    _effects?.PlaySpinStartShake();
                    return;

                case GameFeedbackType.BombShake:
                    _effects?.PlayBombShake();
                    return;

                case GameFeedbackType.PlaySound:
                    if (App.Sound != null && !string.IsNullOrWhiteSpace(request.SoundName))
                        App.Sound.PlaySound(request.SoundName, isUI: true, pitchMultiplier: request.PitchMultiplier);
                    return;
            }
        }

        private void HandleWheelAnimationStopRequested()
        {
            if (_rouletteWheel != null)
                _rouletteWheel.StopAnimation();
        }

        private void HandleWheelRotationResetRequested()
        {
            if (_rouletteWheel != null)
                _rouletteWheel.ResetWheelRotation();
        }
    }
}
