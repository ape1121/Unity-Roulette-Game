using Ape.Core;
using Ape.Data;
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
            _scenePresenter?.Bind(App.Game, App.Profile, App.Sound, ResolveUiTextConfig());
            App.Game.BindScene();
            App.Game.StartGame();
        }

        private static GameUiTextConfig ResolveUiTextConfig()
        {
            return App.Config != null && App.Config.GameConfig != null
                ? App.Config.GameConfig.UiTextConfig
                : null;
        }
    }
}
