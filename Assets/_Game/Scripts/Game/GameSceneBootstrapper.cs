using Ape.Core;
using Ape.Scenes;
using UnityEngine;

namespace Ape.Game
{
    public sealed class GameSceneBootstrapper : SceneBootstrapper
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private RouletteWheelUI _rouletteWheel;
        [SerializeField] private GameUIManager _gameUIManager;

        protected override void BootstrapScene()
        {
            App.Game.PrepareForSceneLoad();
            App.Game.BindScene(new GameSceneDependencies(_mainCamera, _rouletteWheel, _gameUIManager));
            App.Game.StartGame();
        }
    }
}
