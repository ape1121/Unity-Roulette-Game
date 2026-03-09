using UnityEngine;

[CreateAssetMenu(fileName = "AppConfig", menuName = "CriticalShot/Configs/AppConfig")]
public class AppConfig : ScriptableObject
{
    public GameConfig GameConfig;
    public AllSounds SoundsConfig;
    public string GameSceneName;
    public float SceneTransitionDuration = 1f;
}