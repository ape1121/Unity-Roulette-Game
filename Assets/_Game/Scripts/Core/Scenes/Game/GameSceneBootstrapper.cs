using System.Collections;
using UnityEngine;

public sealed class GameSceneBootstrapper : SceneBootstrapper
{
    [SerializeField] private Camera _mainCamera;

    protected override IEnumerator BootstrapSceneAsync()
    {
        if (_mainCamera == null)
            throw new MissingReferenceException("GameSceneBootstrapper requires a main camera reference.");

        App.Game.BindScene(new GameSceneDependencies(_mainCamera));
        yield break;
    }
}
