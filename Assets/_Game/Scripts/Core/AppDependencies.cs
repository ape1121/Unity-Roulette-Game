using Ape.Scenes;
using Ape.Sounds;

namespace Ape.Core
{
    [System.Serializable]
    public struct AppDependencies
    {
        public LoadingScreenView LoadingScreen;
        public SoundManager SoundManager;
    }
}
