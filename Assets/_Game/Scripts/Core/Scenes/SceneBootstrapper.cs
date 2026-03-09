using System.Collections;
using UnityEngine;

public class SceneBootstrapper : MonoBehaviour
{
    private bool _isBootstrapped;

    protected virtual IEnumerator BootstrapSceneAsync()
    {
        yield break;
    }

    private IEnumerator Start()
    {
        if (_isBootstrapped)
            yield break;
        App.Scenes.RegisterSceneBootstrapper(this);
        yield return BootstrapSceneAsync();
        _isBootstrapped = true;
        yield return App.Scenes.CompleteSceneBootstrapAsync(this);
    }
}
