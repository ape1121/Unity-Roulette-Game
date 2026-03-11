using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CriticalShot/Core/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public RarityCollection RarityCollection;
        public RouletteConfig RouletteConfig;
        public CaseRewardsConfig CaseRewardsConfig;
        public GameUiTextConfig UiTextConfig;

        [Header("Run Rules")]
        public bool cashOutOnSafeZoneOnly = true;
        public bool continueEnabled;
        [Min(0)] public int continueCost;
        [Min(0)] public int buyInCost;
        [Min(1)] public int startingZone = 1;
        [Min(0)] public int maxLevel;
        [Min(1)] public int safeZoneInterval = 5;
        [Min(1)] public int superZoneInterval = 30;

        public bool HasLevelCap => maxLevel > 0;

        public int GetStartingZone()
        {
            int resolvedStartingZone = Mathf.Max(1, startingZone);

            if (HasLevelCap)
                resolvedStartingZone = Mathf.Min(resolvedStartingZone, maxLevel);

            return resolvedStartingZone;
        }

        public RouletteZoneType GetZoneType(int zone)
        {
            int resolvedZone = Mathf.Max(1, zone);

            if (resolvedZone % Mathf.Max(1, superZoneInterval) == 0)
                return RouletteZoneType.Super;

            if (resolvedZone % Mathf.Max(1, safeZoneInterval) == 0)
                return RouletteZoneType.Safe;

            return RouletteZoneType.Normal;
        }

        public bool CanCashOutAtZone(int zone)
        {
            if (!cashOutOnSafeZoneOnly)
                return true;

            RouletteZoneType zoneType = GetZoneType(zone);
            return zoneType == RouletteZoneType.Safe || zoneType == RouletteZoneType.Super;
        }

        public bool IsFinalZone(int zone)
        {
            return HasLevelCap && Mathf.Max(1, zone) >= maxLevel;
        }

        public bool TryGetReward(string rewardId, out RewardData rewardData)
        {
            rewardData = null;

            if (RouletteConfig != null && RouletteConfig.TryGetReward(rewardId, out rewardData))
                return true;

            if (CaseRewardsConfig != null && CaseRewardsConfig.TryGetReward(rewardId, out rewardData))
                return true;

            return false;
        }

        public bool TryGetCaseDefinition(string rewardId, out CaseRewardsConfig.CaseDefinition caseDefinition)
        {
            caseDefinition = default;
            return CaseRewardsConfig != null && CaseRewardsConfig.TryGetCase(rewardId, out caseDefinition);
        }
    }
}
