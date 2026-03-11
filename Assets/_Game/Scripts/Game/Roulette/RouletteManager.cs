using System;
using Ape.Data;
using UnityEngine;

namespace Ape.Game
{
    public sealed class RouletteManager
    {
        private GameConfig _gameConfig;
        private System.Random _runRandom;
        private int _runCounter;

        public RouletteConfig Config => _gameConfig != null ? _gameConfig.RouletteConfig : null;
        public RoulettePresentationConfig PresentationConfig => Config != null ? Config.PresentationConfig : null;
        public int CurrentZone { get; private set; }
        public RouletteZoneType CurrentZoneType { get; private set; }
        public RouletteResolvedWheel ActiveWheel { get; private set; }
        public RouletteSpinResult LastSpinResult { get; private set; }
        public bool HasActiveWheel => ActiveWheel != null && ActiveWheel.Slices != null && ActiveWheel.Slices.Count > 0;
        public float PostSpinRevealDelay => Config != null ? Config.PostSpinRevealDelay : 0f;

        public void Configure(GameConfig gameConfig)
        {
            _gameConfig = gameConfig;
        }

        public void Initialize()
        {
            ResetManagerState();
        }

        public void ResetManagerState()
        {
            _runCounter = 0;
            _runRandom = null;
            ResetRunState();
        }

        public void ResetRunState()
        {
            CurrentZone = 0;
            CurrentZoneType = RouletteZoneType.Normal;
            ActiveWheel = null;
            LastSpinResult = default;
        }

        public void StartRun()
        {
            EnsureGameConfig();

            _runCounter++;
            _runRandom = new System.Random(GetRouletteConfig().ResolveRunSeed(_runCounter));
            CurrentZone = _gameConfig.GetStartingZone();
            CurrentZoneType = _gameConfig.GetZoneType(CurrentZone);
            ActiveWheel = null;
            LastSpinResult = default;
        }

        public RouletteResolvedWheel BuildWheelForCurrentZone()
        {
            EnsureGameConfig();
            EnsureRunRandom();

            CurrentZoneType = _gameConfig.GetZoneType(CurrentZone);

            RouletteConfig rouletteConfig = GetRouletteConfig();
            RouletteWheelData wheelData = rouletteConfig.GetWheelData(CurrentZoneType);
            ActiveWheel = RouletteWheelBuilder.BuildWheel(rouletteConfig, wheelData, CurrentZone, _runRandom);
            CurrentZoneType = ActiveWheel.Definition.ZoneType;
            return ActiveWheel;
        }

        public RouletteSpinResult ResolveSpin()
        {
            EnsureRunRandom();

            if (!HasActiveWheel)
                throw new InvalidOperationException("RouletteManager requires an active wheel before resolving a spin.");

            int selectedSliceIndex = _runRandom.Next(ActiveWheel.Slices.Count);
            RouletteResolvedSlice selectedSlice = ActiveWheel.Slices[selectedSliceIndex];
            bool completedRun = !selectedSlice.IsBomb && _gameConfig != null && _gameConfig.IsFinalZone(CurrentZone);
            int nextZone = completedRun ? CurrentZone : CurrentZone + 1;

            LastSpinResult = new RouletteSpinResult(
                CurrentZone,
                CurrentZoneType,
                selectedSliceIndex,
                selectedSlice,
                completedRun,
                nextZone);

            return LastSpinResult;
        }

        public void RestoreZone(int zone)
        {
            EnsureGameConfig();
            CurrentZone = Mathf.Max(1, zone);
            CurrentZoneType = _gameConfig.GetZoneType(CurrentZone);
            ActiveWheel = null;
        }

        public void AdvanceToZone(int zone)
        {
            RestoreZone(zone);
        }

        public void ValidateConfig()
        {
            RouletteConfig rouletteConfig = GetRouletteConfig();

            if (rouletteConfig.GetRewardCatalog().Length == 0)
                throw new InvalidOperationException("RouletteConfig must contain at least one RewardData asset in its reward catalog.");

            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Normal, mustIncludeBomb: true, mustExcludeBomb: false);
            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Safe, mustIncludeBomb: false, mustExcludeBomb: true);
            ValidateWheelAsset(rouletteConfig, RouletteZoneType.Super, mustIncludeBomb: false, mustExcludeBomb: true);
        }

        private RouletteConfig GetRouletteConfig()
        {
            EnsureGameConfig();

            if (_gameConfig.RouletteConfig == null)
                throw new MissingReferenceException("GameConfig.RouletteConfig must be assigned.");

            return _gameConfig.RouletteConfig;
        }

        private void EnsureGameConfig()
        {
            if (_gameConfig == null)
                throw new MissingReferenceException("RouletteManager requires GameConfig to be configured before use.");
        }

        private void EnsureRunRandom()
        {
            if (_runRandom == null)
                throw new InvalidOperationException("RouletteManager requires an active run before random operations can be resolved.");
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
    }
}
