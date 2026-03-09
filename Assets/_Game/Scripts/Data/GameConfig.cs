using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Ape.Data
{
    [MovedFrom(false, sourceNamespace: "")]
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CriticalShot/Core/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public enum ZoneType
        {
            Normal,
            Safe,
            Super
        }

        public enum WheelVisualTheme
        {
            Bronze,
            Silver,
            Gold
        }

        [Header("Run Rules")]
        public bool cashOutOnSafeZoneOnly = true;
        public bool continueEnabled;
        [Min(0)] public int continueCost;
        [Min(0)] public int buyInCost;
        [Min(1)] public int startingZone = 1;
        [Min(0)] public int maxLevel;
        [Min(1)] public int safeZoneInterval = 5;
        [Min(1)] public int superZoneInterval = 30;
        public int deterministicSeed = 1337;

        [Header("Reward Catalog")]
        public RouletteRewards rewardCatalog;

        [Header("Wheel Assets")]
        public RouletteWheelData normalWheel;
        public RouletteWheelData safeWheel;
        public RouletteWheelData superWheel;

        public bool HasLevelCap => maxLevel > 0;

        public int GetStartingZone()
        {
            int resolvedStartingZone = Mathf.Max(1, startingZone);

            if (HasLevelCap)
                resolvedStartingZone = Mathf.Min(resolvedStartingZone, maxLevel);

            return resolvedStartingZone;
        }

        public ZoneType GetZoneType(int zone)
        {
            int resolvedZone = Mathf.Max(1, zone);

            if (resolvedZone % Mathf.Max(1, superZoneInterval) == 0)
                return ZoneType.Super;

            if (resolvedZone % Mathf.Max(1, safeZoneInterval) == 0)
                return ZoneType.Safe;

            return ZoneType.Normal;
        }

        public bool CanCashOutAtZone(int zone)
        {
            if (!cashOutOnSafeZoneOnly)
                return true;

            ZoneType zoneType = GetZoneType(zone);
            return zoneType == ZoneType.Safe || zoneType == ZoneType.Super;
        }

        public bool IsFinalZone(int zone)
        {
            return HasLevelCap && Mathf.Max(1, zone) >= maxLevel;
        }

        public RouletteWheelData GetWheelData(int zone)
        {
            return GetWheelData(GetZoneType(zone));
        }

        public RouletteWheelData GetWheelData(ZoneType zoneType)
        {
            return zoneType switch
            {
                ZoneType.Safe => safeWheel,
                ZoneType.Super => superWheel,
                _ => normalWheel
            };
        }

        public RewardData[] GetRewardCatalog()
        {
            return rewardCatalog?.rewards ?? System.Array.Empty<RewardData>();
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;
            return rewardCatalog != null && rewardCatalog.TryGetReward(rewardId, out rewardData);
        }
    }
}
