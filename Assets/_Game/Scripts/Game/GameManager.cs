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
        private const string SpinStartSoundName = "roulette_spin_start";
        private const string SpinTickSoundName = "roulette_spin_tick";
        private const string SpinStopSoundName = "roulette_spin_stop";
        private const string SpinRewardSoundName = "roulette_spin_reward";
        private const string SpinBombSoundName = "roulette_spin_bomb";

        private readonly RouletteRewardLedger _rewardLedger = new RouletteRewardLedger();
        private readonly List<ResolvedReward> _pendingContinueItemRewards = new List<ResolvedReward>();

        private System.Random _runRandom;
        private RouletteSpinResult _pendingSpinResult;
        private int _pendingContinueZone;
        private int _pendingContinueCash;
        private int _pendingContinueGold;
        private int _runCounter;
        private bool _hasPendingSpinResult;

        public event Action<GameStateSnapshot> StateChanged;
        public event Action<RouletteSpinResult> SpinResolved;

        public GameConfig Config => App.Config != null ? App.Config.GameConfig : null;
        public GameSceneDependencies SceneDependencies { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsSceneBound { get; private set; }
        public bool IsGameStarted { get; private set; }
        public GameRunPhase Phase { get; private set; }
        public int CurrentZone { get; private set; }
        public GameConfig.ZoneType CurrentZoneType { get; private set; }
        public RouletteResolvedWheel ActiveWheel { get; private set; }
        public bool HasUsedContinue { get; private set; }
        public RouletteSpinResult LastSpinResult { get; private set; }
        public IReadOnlyList<ResolvedReward> PendingItemRewards => _rewardLedger.ItemRewards;
        public GameStateSnapshot CurrentState => BuildStateSnapshot();
        public int PendingCash => _rewardLedger.PendingCash;
        public int PendingGold => _rewardLedger.PendingGold;
        public int SavedCash => App.Profile != null ? App.Profile.CurrentData.Cash : 0;
        public int SavedGold => App.Profile != null ? App.Profile.CurrentData.Gold : 0;
        public bool CanSpin => IsGameStarted && Phase == GameRunPhase.AwaitingSpin && ActiveWheel != null && ActiveWheel.Slices.Count > 0;
        public bool CanCashOut => IsGameStarted && Config != null && Phase == GameRunPhase.AwaitingSpin && Config.CanCashOutAtZone(CurrentZone);
        public bool CanContinue => IsGameStarted
            && Config != null
            && Config.continueEnabled
            && Phase == GameRunPhase.Busted
            && !HasUsedContinue
            && _pendingContinueZone > 0
            && App.Profile != null
            && App.Profile.CanAffordCash(Config.continueCost);
        public bool CanRestart => IsGameStarted && (Phase == GameRunPhase.Busted || Phase == GameRunPhase.CashedOut || Phase == GameRunPhase.Completed || Phase == GameRunPhase.BlockedByBuyIn);

        public void Initialize()
        {
            ResetManagerState();
            IsInitialized = true;
        }

        public void PrepareForSceneLoad()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before preparing for scene load.");

            StopWheelAnimation();
            SceneDependencies = default;
            IsSceneBound = false;
            IsGameStarted = false;
            ResetRunState();
        }

        public void BindScene(GameSceneDependencies sceneDependencies)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before scene dependencies are bound.");

            if (sceneDependencies.MainCamera == null)
                throw new MissingReferenceException("Game scene dependencies require a main camera reference.");

            SceneDependencies = sceneDependencies;
            IsSceneBound = true;

            if (ActiveWheel != null && SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.BuildWheel(ActiveWheel);
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

            spinResult = ResolveSpin();
            LastSpinResult = spinResult;
            _pendingSpinResult = spinResult;
            _hasPendingSpinResult = true;
            Phase = GameRunPhase.Spinning;

            PlaySound(SpinStartSoundName);

            if (SceneDependencies.RouletteWheel != null)
            {
                SceneDependencies.RouletteWheel.PlaySpin(
                    ActiveWheel,
                    spinResult.SelectedSliceIndex,
                    HandleWheelSliceTick,
                    FinalizePendingSpin);
            }
            else
            {
                FinalizePendingSpin();
            }

            PublishStateChanged();
            return true;
        }

        public bool TryCashOut()
        {
            if (!CanCashOut)
                return false;

            BankPendingRewards();
            Phase = GameRunPhase.CashedOut;
            ClearPendingContinueState();
            PublishStateChanged();
            return true;
        }

        public bool TryContinue()
        {
            if (!CanContinue)
                return false;

            if (!App.Profile.TrySpendCash(Config.continueCost))
                return false;

            HasUsedContinue = true;
            CurrentZone = _pendingContinueZone;
            CurrentZoneType = Config.GetZoneType(CurrentZone);
            _rewardLedger.Restore(_pendingContinueCash, _pendingContinueGold, _pendingContinueItemRewards);
            ClearPendingContinueState();
            BuildWheelForCurrentZone();
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
            return true;
        }

        private void StartRunInternal()
        {
            _runCounter++;
            _runRandom = new System.Random(unchecked(Config.deterministicSeed + (_runCounter * 9973)));

            ResetRunState();
            CurrentZone = Config.GetStartingZone();
            CurrentZoneType = Config.GetZoneType(CurrentZone);

            if (!TryPayBuyIn())
            {
                Phase = GameRunPhase.BlockedByBuyIn;
                ActiveWheel = null;
                SyncWheelToScene();
                PublishStateChanged();
                return;
            }

            BuildWheelForCurrentZone();
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
        }

        private void BuildWheelForCurrentZone()
        {
            CurrentZoneType = Config.GetZoneType(CurrentZone);
            ActiveWheel = RouletteWheelBuilder.BuildWheel(Config, CurrentZone, _runRandom);
            SyncWheelToScene();
        }

        private RouletteSpinResult ResolveSpin()
        {
            int selectedSliceIndex = _runRandom.Next(ActiveWheel.Slices.Count);
            RouletteResolvedSlice selectedSlice = ActiveWheel.Slices[selectedSliceIndex];
            bool completedRun = !selectedSlice.IsBomb && Config.IsFinalZone(CurrentZone);
            int nextZone = completedRun ? CurrentZone : CurrentZone + 1;

            return new RouletteSpinResult(
                CurrentZone,
                CurrentZoneType,
                selectedSliceIndex,
                selectedSlice,
                completedRun,
                nextZone);
        }

        private void FinalizePendingSpin()
        {
            if (!_hasPendingSpinResult)
                return;

            _hasPendingSpinResult = false;
            PlaySound(SpinStopSoundName);
            ApplySpinResult(_pendingSpinResult);
            SpinResolved?.Invoke(_pendingSpinResult);
            PublishStateChanged();
        }

        private void ApplySpinResult(RouletteSpinResult spinResult)
        {
            if (spinResult.WasBomb)
            {
                CaptureContinueState();
                _rewardLedger.Clear();
                Phase = GameRunPhase.Busted;
                PlaySound(SpinBombSoundName);
                return;
            }

            ClearPendingContinueState();
            _rewardLedger.AddReward(spinResult.SelectedSlice.Reward);
            PlaySound(SpinRewardSoundName);

            if (spinResult.CompletedRun)
            {
                BankPendingRewards();
                Phase = GameRunPhase.Completed;
                return;
            }

            CurrentZone = spinResult.NextZone;
            CurrentZoneType = Config.GetZoneType(CurrentZone);
            BuildWheelForCurrentZone();
            Phase = GameRunPhase.AwaitingSpin;
        }

        private void CaptureContinueState()
        {
            _pendingContinueZone = CurrentZone;
            _pendingContinueCash = _rewardLedger.PendingCash;
            _pendingContinueGold = _rewardLedger.PendingGold;
            _pendingContinueItemRewards.Clear();

            for (int i = 0; i < _rewardLedger.ItemRewards.Count; i++)
                _pendingContinueItemRewards.Add(_rewardLedger.ItemRewards[i]);
        }

        private void ClearPendingContinueState()
        {
            _pendingContinueZone = 0;
            _pendingContinueCash = 0;
            _pendingContinueGold = 0;
            _pendingContinueItemRewards.Clear();
        }

        private void BankPendingRewards()
        {
            if (App.Profile == null)
                throw new InvalidOperationException("GameManager requires ProfileManager before rewards can be banked.");

            App.Profile.ApplyBankedRewards(PendingCash, PendingGold, _rewardLedger.CreateInventorySnapshot());
            _rewardLedger.Clear();
        }

        private bool TryPayBuyIn()
        {
            int resolvedBuyInCost = Mathf.Max(0, Config.buyInCost);
            if (resolvedBuyInCost == 0)
                return true;

            if (App.Profile == null)
                throw new InvalidOperationException("GameManager requires ProfileManager before a buy-in can be charged.");

            return App.Profile.TrySpendCash(resolvedBuyInCost);
        }

        private void ValidateConfig()
        {
            if (Config == null)
                throw new MissingReferenceException("GameManager requires AppConfig.GameConfig to be assigned.");

            if (Config.GetRewardCatalog().Length == 0)
                throw new InvalidOperationException("GameConfig.rewardCatalog must contain at least one RewardData asset.");

            ValidateWheelAsset(Config.normalWheel, GameConfig.ZoneType.Normal, mustIncludeBomb: true, mustExcludeBomb: false);
            ValidateWheelAsset(Config.safeWheel, GameConfig.ZoneType.Safe, mustIncludeBomb: false, mustExcludeBomb: true);
            ValidateWheelAsset(Config.superWheel, GameConfig.ZoneType.Super, mustIncludeBomb: false, mustExcludeBomb: true);
        }

        private static void ValidateWheelAsset(RouletteWheelData wheelData, GameConfig.ZoneType zoneType, bool mustIncludeBomb, bool mustExcludeBomb)
        {
            if (wheelData == null)
                throw new MissingReferenceException($"GameConfig is missing the wheel asset for zone type {zoneType}.");

            RouletteSliceData[] sliceDefinitions = wheelData.SliceDefinitions;
            if (sliceDefinitions.Length == 0)
                throw new InvalidOperationException($"Wheel asset '{wheelData.name}' has no slice rules.");

            bool hasBomb = false;

            for (int i = 0; i < sliceDefinitions.Length; i++)
            {
                if (sliceDefinitions[i] == null)
                    throw new InvalidOperationException($"Wheel asset '{wheelData.name}' contains a null slice rule.");

                if (sliceDefinitions[i].IsBomb)
                    hasBomb = true;
            }

            if (mustIncludeBomb && !hasBomb)
                throw new InvalidOperationException($"Wheel asset '{wheelData.name}' must contain at least one bomb slice for {zoneType} zones.");

            if (mustExcludeBomb && hasBomb)
                throw new InvalidOperationException($"Wheel asset '{wheelData.name}' must not contain bomb slices for {zoneType} zones.");
        }

        private void HandleWheelSliceTick(int sliceIndex)
        {
            float pitchMultiplier = 1f + ((sliceIndex % 3) * 0.04f);
            PlaySound(SpinTickSoundName, pitchMultiplier);
        }

        private void SyncWheelToScene()
        {
            if (SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.BuildWheel(ActiveWheel);
        }

        private void StopWheelAnimation()
        {
            if (SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.StopAnimation();
        }

        private void PlaySound(string soundName, float pitchMultiplier = 1f)
        {
            if (App.Sound == null)
                return;

            App.Sound.PlaySound(soundName, isUI: true, pitchMultiplier: pitchMultiplier);
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke(BuildStateSnapshot());
        }

        private GameStateSnapshot BuildStateSnapshot()
        {
            GameConfig.WheelVisualTheme wheelTheme = ActiveWheel != null
                ? ActiveWheel.VisualTheme
                : Config != null && CurrentZone > 0 && Config.GetWheelData(CurrentZoneType) != null
                    ? Config.GetWheelData(CurrentZoneType).VisualTheme
                    : GameConfig.WheelVisualTheme.Bronze;

            return new GameStateSnapshot(
                Phase,
                CurrentZone,
                CurrentZoneType,
                wheelTheme,
                PendingCash,
                PendingGold,
                _rewardLedger.PendingItemCardCount,
                _rewardLedger.ItemRewards.Count,
                SavedCash,
                SavedGold,
                HasUsedContinue,
                CanSpin,
                CanCashOut,
                CanContinue,
                CanRestart,
                ActiveWheel != null ? ActiveWheel.Slices.Count : 0);
        }

        private void ResetManagerState()
        {
            SceneDependencies = default;
            IsInitialized = false;
            IsSceneBound = false;
            IsGameStarted = false;
            _runCounter = 0;
            _runRandom = null;
            ResetRunState();
        }

        private void ResetRunState()
        {
            StopWheelAnimation();
            Phase = GameRunPhase.None;
            CurrentZone = 0;
            CurrentZoneType = GameConfig.ZoneType.Normal;
            ActiveWheel = null;
            HasUsedContinue = false;
            LastSpinResult = default;
            _pendingSpinResult = default;
            _hasPendingSpinResult = false;
            _rewardLedger.Clear();
            ClearPendingContinueState();
        }
    }
}
