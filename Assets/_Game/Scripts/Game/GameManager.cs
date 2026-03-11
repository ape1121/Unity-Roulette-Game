using System;
using System.Collections.Generic;
using Ape.Core;
using Ape.Data;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameManager : IManager
    {
        private const string SpinRewardSoundName = "roulette_spin_reward";
        private const string SpinBombSoundName = "boom";

        private readonly RouletteRewardLedger _rewardLedger = new RouletteRewardLedger();
        private readonly List<ResolvedReward> _pendingContinueInventoryRewards = new List<ResolvedReward>();

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
        public RewardManager Rewards { get; } = new RewardManager();
        public InventoryManager Inventory { get; } = new InventoryManager();
        public GameSceneDependencies SceneDependencies { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsSceneBound { get; private set; }
        public bool IsGameStarted { get; private set; }
        public GameRunPhase Phase { get; private set; }
        public int CurrentZone { get; private set; }
        public RouletteZoneType CurrentZoneType { get; private set; }
        public RouletteResolvedWheel ActiveWheel { get; private set; }
        public bool HasUsedContinue { get; private set; }
        public RouletteSpinResult LastSpinResult { get; private set; }
        public IReadOnlyList<ResolvedReward> PendingInventoryRewards => _rewardLedger.InventoryRewards;
        public GameStateSnapshot CurrentState => BuildStateSnapshot();
        public int PendingCash => _rewardLedger.PendingCash;
        public int PendingGold => _rewardLedger.PendingGold;
        public int SavedCash => App.Profile != null ? App.Profile.CurrentData.Cash : 0;
        public int SavedGold => App.Profile != null ? App.Profile.CurrentData.Gold : 0;
        private RouletteConfig RouletteConfig => Config != null ? Config.RouletteConfig : null;
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
            Rewards.Initialize();
            Inventory.Initialize(Rewards);
        }

        public void PrepareForSceneLoad()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("GameManager must be initialized before preparing for scene load.");

            StopWheelAnimation();
            ResetWheelRotation();
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
                SceneDependencies.RouletteWheel.BuildWheel(ActiveWheel, preserveRotation: true);
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

            PlaySpinStartUiShake();
            spinResult = ResolveSpin();
            LastSpinResult = spinResult;
            _pendingSpinResult = spinResult;
            _hasPendingSpinResult = true;
            Phase = GameRunPhase.Spinning;

            if (SceneDependencies.RouletteWheel != null)
            {
                SceneDependencies.RouletteWheel.PlaySpin(
                    ActiveWheel,
                    spinResult.SelectedSliceIndex,
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
            _rewardLedger.Restore(_pendingContinueCash, _pendingContinueGold, _pendingContinueInventoryRewards);
            ClearPendingContinueState();
            BuildWheelForCurrentZone(preserveRotation: true);
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
            return true;
        }

        private void StartRunInternal()
        {
            _runCounter++;
            _runRandom = new System.Random(GetRouletteConfig().ResolveRunSeed(_runCounter));

            ResetRunState();
            CurrentZone = Config.GetStartingZone();
            CurrentZoneType = Config.GetZoneType(CurrentZone);

            if (!TryPayBuyIn())
            {
                Phase = GameRunPhase.BlockedByBuyIn;
                ActiveWheel = null;
                SyncWheelToScene(preserveRotation: true);
                PublishStateChanged();
                return;
            }

            BuildWheelForCurrentZone(preserveRotation: true);
            Phase = GameRunPhase.AwaitingSpin;
            PublishStateChanged();
        }

        private void BuildWheelForCurrentZone(bool preserveRotation = true, bool syncToScene = true)
        {
            CurrentZoneType = Config.GetZoneType(CurrentZone);
            RouletteConfig rouletteConfig = GetRouletteConfig();
            RouletteWheelData wheelData = rouletteConfig.GetWheelData(CurrentZoneType);
            ActiveWheel = RouletteWheelBuilder.BuildWheel(rouletteConfig, wheelData, CurrentZone, _runRandom);
            CurrentZoneType = ActiveWheel.Definition.ZoneType;

            if (syncToScene)
                SyncWheelToScene(preserveRotation);
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
            ApplySpinResult(_pendingSpinResult);
            SpinResolved?.Invoke(_pendingSpinResult);
            PublishStateChanged();
        }

        private void ApplySpinResult(RouletteSpinResult spinResult)
        {
            if (spinResult.WasBomb)
            {
                PlayBombUiShake();
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
            BuildWheelForCurrentZone(preserveRotation: true, syncToScene: false);
            Phase = GameRunPhase.AwaitingSpin;
            PlayPendingWheelReveal(spinResult);
        }

        private void CaptureContinueState()
        {
            _pendingContinueZone = CurrentZone;
            _pendingContinueCash = _rewardLedger.PendingCash;
            _pendingContinueGold = _rewardLedger.PendingGold;
            _pendingContinueInventoryRewards.Clear();

            for (int i = 0; i < _rewardLedger.InventoryRewards.Count; i++)
                _pendingContinueInventoryRewards.Add(_rewardLedger.InventoryRewards[i]);
        }

        private void ClearPendingContinueState()
        {
            _pendingContinueZone = 0;
            _pendingContinueCash = 0;
            _pendingContinueGold = 0;
            _pendingContinueInventoryRewards.Clear();
        }

        private void BankPendingRewards()
        {
            Rewards.GrantRewards(PendingCash, PendingGold, _rewardLedger.InventoryRewards);
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

            RouletteConfig rouletteConfig = GetRouletteConfig();
            if (rouletteConfig.GetRewardCatalog().Length == 0)
                throw new InvalidOperationException("RouletteConfig must contain at least one RewardData asset in its reward catalog.");

            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Normal, mustIncludeBomb: true, mustExcludeBomb: false);
            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Safe, mustIncludeBomb: false, mustExcludeBomb: true);
            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Super, mustIncludeBomb: false, mustExcludeBomb: true);
        }

        private static void ValidateWheelAsset(RouletteConfig rouletteConfig, RouletteZoneType zoneType, bool mustIncludeBomb, bool mustExcludeBomb)
        {
            RouletteWheelData wheelData = rouletteConfig.GetWheelData(zoneType);
            if (wheelData == null)
                throw new MissingReferenceException($"RouletteConfig is missing the wheel asset for zone type {zoneType}.");

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

        private RouletteConfig GetRouletteConfig()
        {
            if (RouletteConfig == null)
                throw new MissingReferenceException("GameConfig.RouletteConfig must be assigned.");

            return RouletteConfig;
        }

        private void SyncWheelToScene(bool preserveRotation = true)
        {
            if (SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.BuildWheel(ActiveWheel, preserveRotation);
        }

        private void StopWheelAnimation()
        {
            if (SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.StopAnimation();
        }

        private void ResetWheelRotation()
        {
            if (SceneDependencies.RouletteWheel != null)
                SceneDependencies.RouletteWheel.ResetWheelRotation();
        }

        private void PlaySound(string soundName, float pitchMultiplier = 1f)
        {
            if (App.Sound == null)
                return;

            App.Sound.PlaySound(soundName, isUI: true, pitchMultiplier: pitchMultiplier);
        }

        private void PlaySpinStartUiShake()
        {
            SceneDependencies.UIManager?.Effects?.PlaySpinStartShake();
        }

        private void PlayBombUiShake()
        {
            SceneDependencies.UIManager?.Effects?.PlayBombShake();
        }

        private void PlayPendingWheelReveal(RouletteSpinResult spinResult)
        {
            if (SceneDependencies.RouletteWheel == null)
            {
                SyncWheelToScene(preserveRotation: true);
                return;
            }

            SceneDependencies.RouletteWheel.PlayPostSpinReveal(
                ActiveWheel,
                spinResult.SelectedSliceIndex,
                spinResult.SelectedSlice,
                PublishStateChanged);
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke(BuildStateSnapshot());
        }

        private GameStateSnapshot BuildStateSnapshot()
        {
            return new GameStateSnapshot(
                Phase,
                CurrentZone,
                CurrentZoneType,
                PendingCash,
                PendingGold,
                _rewardLedger.PendingInventoryRewardCount,
                _rewardLedger.InventoryRewards.Count,
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
            Rewards.ResetState();
            Inventory.ResetState();
            ResetRunState();
        }

        private void ResetRunState()
        {
            StopWheelAnimation();
            Phase = GameRunPhase.None;
            CurrentZone = 0;
            CurrentZoneType = RouletteZoneType.Normal;
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
