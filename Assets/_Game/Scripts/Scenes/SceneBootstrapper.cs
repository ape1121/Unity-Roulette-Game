using UnityEngine;

namespace Ape.Scenes
{
    public class SceneBootstrapper : MonoBehaviour
    {
        protected virtual void BootstrapScene()
        {
        }

        private void Start()
        {
            BootstrapScene();
        }
    }
}
