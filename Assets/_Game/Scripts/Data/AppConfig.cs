using Ape.Sounds;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "AppConfig", menuName = "CriticalShot/Core/AppConfig")]
    public class AppConfig : ScriptableObject
    {
        public GameConfig GameConfig;
        public AllSounds SoundsConfig;
        public string GameSceneName;
        public float SceneTransitionDuration = 1f;
    }
}
