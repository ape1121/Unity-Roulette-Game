using System;
using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using Ape.Profile;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameManager : IManager
    {
        private GameConfig _config;
        private RouletteSpinResult _pendingSpinResult;
        private bool _hasPendingSpinResult;

        public event Action<GameStateSnapshot> StateChanged;
        public event Action<GameWheelBuildRequest> WheelBuildRequested;
        public event Action<GameSpinPresentationRequest> SpinPresentationRequested;
        public event Action<GameSpinRevealPresentationRequest> SpinRevealPresentationRequested;
        public event Action<GameFeedbackRequest> FeedbackRequested;
        public event Action WheelAnimationStopRequested;
        public event Action WheelRotationResetRequested;

        public GameConfig Config => _config;
        public RewardManager Rewards { get; } = new RewardManager();
        public InventoryManager Inventory { get; } = new InventoryManager();
        public RouletteManager Roulette { get; } = new RouletteManager();
        public bool IsInitialized { get; private set; }
        public bool IsSceneBound { get; private set; }
        public bool IsGameStarted { get; private set; }
        public GameRunPhase Phase { get; private set; }
        public bool HasUsedContinue { get; private set; }
        public IReadOnlyList<ResolvedReward> PendingInventoryRewards => Inventory.PendingInventoryRewards;
        public GameStateSnapshot CurrentState => BuildStateSnapshot();
        public bool CanSpin => IsGameStarted && Phase == GameRunPhase.AwaitingSpin && Roulette.HasActiveWheel;
        public bool CanCashOut => IsGameStarted && Config != null && Phase == GameRunPhase.AwaitingSpin && Config.CanCashOutAtZone(Roulette.CurrentZone);
        public bool CanContinue => IsGameStarted
            && Config != null
            && Config.continueEnabled
            && Phase == GameRunPhase.Busted
            && !HasUsedContinue
            && Inventory.CanContinue(Config.continueCost);
        public bool CanRestart => IsGameStarted && (Phase == GameRunPhase.Busted || Phase == GameRunPhase.CashedOut || Phase == GameRunPhase.Completed || Phase == GameRunPhase.BlockedByBuyIn);

        public void Configure(GameConfig config, ProfileManager profileManager)
        {
            _config = config;
            Rewards.Configure(config, profileManager);
            Inventory.Configure(config, profileManager, Rewards);
            Roulette.Configure(config);
        }

        public void Initialize()
        {
            ResetManagerState();
            Rewards.Initialize();
            Inventory.Initialize();
            Roulette.Initialize();
            IsInitialized = true;
        }

        public void PrepareForSceneLoad()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before preparing for scene load.");

            ResetWheelRotation();
            IsSceneBound = false;
            IsGameStarted = false;
            ResetRunState();
        }

        public void BindScene()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before the scene can be bound.");

            IsSceneBound = true;

            if (Roulette.ActiveWheel != null)
                RequestWheelBuild(preserveRotation: true);
        }

        public void StartGame()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before the game can start.");

            if (!IsSceneBound)
                throw new InvalidOperationException("GameManager requires scene dependencies before the game can start.");

            if (IsGameStarted)
                return;

            ValidateConfig();
            IsGameStarted = true;
            StartRunInternal();
        }

        public bool TryRestartRun()
        {
            if (!CanRestart)
                return false;

            StartRunInternal();
            return Phase == GameRunPhase.AwaitingSpin;
        }

        public bool TrySpin(out RouletteSpinResult spinResult)
        {
            spinResult = default;

            if (!CanSpin)
                return false;

            RequestFeedback(GameFeedbackRequest.CreateSpinStartShake());
            spinResult = ResolveSpin();
            _pendingSpinResult = spinResult;
            _hasPendingSpinResult = true;
            Phase = GameRunPhase.Spinning;
            RequestSpinPresentation(Roulette.ActiveWheel, spinResult.SelectedSliceIndex, FinalizePendingSpin);

            PublishStateChanged();
            return true;
        }

        public bool TryCashOut()
        {
            if (!CanCashOut)
                return false;

            Inventory.BankPendingRewards();
            Phase = GameRunPhase.CashedOut;
            Inventory.ClearContinueSnapshot();
            PublishStateChanged();
            return true;
        }

        public bool TryContinue()
        {
            if (!CanContinue)
                return false;

            if (!Inventory.TrySpendContinueCost(Config.continueCost))
                return false;

            HasUsedContinue = true;
            Roulette.RestoreZone(Inventory.ContinueZone);
            Inventory.RestoreContinueSnapshot();
            Inventory.ClearContinueSnapshot();
            BuildWheelForCurrentZone(preserveRotation: true);
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
            return true;
        }

        private void StartRunInternal()
        {
            ResetRunState();
            Roulette.StartRun();

            if (!Inventory.TryPayBuyIn(Config.buyInCost))
            {
                Phase = GameRunPhase.BlockedByBuyIn;
                RequestWheelBuild(preserveRotation: true);
                PublishStateChanged();
                return;
            }

            BuildWheelForCurrentZone(preserveRotation: true);
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
        }

        private void BuildWheelForCurrentZone(bool preserveRotation = true, bool syncToScene = true)
        {
            Roulette.BuildWheelForCurrentZone();

            if (syncToScene)
                RequestWheelBuild(preserveRotation);
        }

        private RouletteSpinResult ResolveSpin()
        {
            return Roulette.ResolveSpin();
        }

        private void FinalizePendingSpin()
        {
            if (!_hasPendingSpinResult)
                return;

            _hasPendingSpinResult = false;
            ApplySpinResult(_pendingSpinResult);
            PublishStateChanged();
        }

        private void ApplySpinResult(RouletteSpinResult spinResult)
        {
            if (spinResult.WasBomb)
            {
                RequestFeedback(GameFeedbackRequest.CreateBombShake());
                Inventory.CaptureContinueSnapshot(Roulette.CurrentZone);
                Inventory.ClearPendingRewards();
                Phase = GameRunPhase.Busted;
                RequestFeedback(GameFeedbackRequest.CreateSound(Roulette.PresentationConfig != null ? Roulette.PresentationConfig.SpinBombSound : null));
                return;
            }

            Inventory.ClearContinueSnapshot();
            Inventory.AddPendingReward(spinResult.SelectedSlice.Reward);
            RequestFeedback(GameFeedbackRequest.CreateSound(Roulette.PresentationConfig != null ? Roulette.PresentationConfig.SpinRewardSound : null));

            if (spinResult.CompletedRun)
            {
                Inventory.BankPendingRewards();
                Phase = GameRunPhase.Completed;
                return;
            }

            Roulette.AdvanceToZone(spinResult.NextZone);
            BuildWheelForCurrentZone(preserveRotation: true, syncToScene: false);
            Phase = GameRunPhase.AwaitingSpin;
            PlayPendingWheelReveal(spinResult);
        }

        private void ValidateConfig()
        {
            if (Config == null)
                throw new MissingReferenceException("GameManager requires AppConfig.GameConfig to be assigned.");

            Roulette.ValidateConfig();
        }

        private void RequestWheelBuild(bool preserveRotation = true)
        {
            WheelBuildRequested?.Invoke(new GameWheelBuildRequest(Roulette.ActiveWheel, preserveRotation));
        }

        private void RequestWheelAnimationStop()
        {
            WheelAnimationStopRequested?.Invoke();
        }

        private void ResetWheelRotation()
        {
            WheelRotationResetRequested?.Invoke();
        }

        private void RequestSpinPresentation(RouletteResolvedWheel wheel, int targetSliceIndex, Action onCompleted)
        {
            if (SpinPresentationRequested == null)
            {
                onCompleted?.Invoke();
                return;
            }

            SpinPresentationRequested.Invoke(new GameSpinPresentationRequest(wheel, targetSliceIndex, onCompleted));
        }

        private void RequestFeedback(GameFeedbackRequest request)
        {
            FeedbackRequested?.Invoke(request);
        }

        private void PlayPendingWheelReveal(RouletteSpinResult spinResult)
        {
            if (SpinRevealPresentationRequested == null)
            {
                RequestWheelBuild(preserveRotation: true);
                PublishStateChanged();
                return;
            }

            SpinRevealPresentationRequested.Invoke(new GameSpinRevealPresentationRequest(
                Roulette.ActiveWheel,
                spinResult.SelectedSliceIndex,
                spinResult.SelectedSlice,
                PublishStateChanged));
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke(BuildStateSnapshot());
        }

        private GameStateSnapshot BuildStateSnapshot()
        {
            return new GameStateSnapshot(
                Phase,
                Roulette.CurrentZone,
                Roulette.CurrentZoneType,
                Inventory.PendingCash,
                Inventory.PendingGold,
                Inventory.PendingInventoryRewardCount,
                Inventory.PendingInventoryRewardKinds,
                Inventory.SavedCash,
                Inventory.SavedGold,
                HasUsedContinue,
                CanSpin,
                CanCashOut,
                CanContinue,
                CanRestart,
                Roulette.ActiveWheel != null ? Roulette.ActiveWheel.Slices.Count : 0);
        }

        private void ResetManagerState()
        {
            IsInitialized = false;
            IsSceneBound = false;
            IsGameStarted = false;
            Rewards.ResetState();
            Inventory.ResetState();
            Roulette.ResetManagerState();
            ResetTransientState();
        }

        private void ResetRunState()
        {
            RequestWheelAnimationStop();
            Inventory.ResetRunState();
            Roulette.ResetRunState();
            ResetTransientState();
        }

        private void ResetTransientState()
        {
            Phase = GameRunPhase.None;
            HasUsedContinue = false;
            _pendingSpinResult = default;
            _hasPendingSpinResult = false;
        }
    }
}
