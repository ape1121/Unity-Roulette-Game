using Ape.Core;
using Ape.Scenes;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameSceneBootstrapper : SceneBootstrapper
    {
        [SerializeField] private GameScenePresenter _scenePresenter;

        private void OnValidate()
        {
            _scenePresenter ??= GetComponentInChildren<GameScenePresenter>(true);
        }

        protected override void BootstrapScene()
        {
            App.Game.PrepareForSceneLoad();
            _scenePresenter?.Bind(App.Game);
            App.Game.BindScene();
            App.Game.StartGame();
        }
    }
}
