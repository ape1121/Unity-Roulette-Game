using UnityEngine;

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
