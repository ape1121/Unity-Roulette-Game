using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "CriticalShot/Configs/GameConfig")]
public class GameConfig : ScriptableObject
{
    
    public bool cashOutOnSafeZoneOnly = true;
    public int continueCost = 0;
    public int buyInCost = 0;
    public int maxLevel = 0;
}