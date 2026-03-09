using UnityEngine;

public sealed class GameSceneBootstrapper : SceneBootstrapper
{
    [SerializeField] private Camera _mainCamera;

    protected override void BootstrapScene()
    {
        App.Game.PrepareForSceneLoad();
        App.Game.BindScene(new GameSceneDependencies(_mainCamera));
        App.Game.StartGame();
    }
}
