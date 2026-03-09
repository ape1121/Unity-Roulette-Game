using UnityEngine;

[CreateAssetMenu(fileName = "AppConfig", menuName = "CriticalShot/Configs/AppConfig")]
public class AppConfig : ScriptableObject
{
    public GameConfig GameConfig;
    public string GameSceneName;
}