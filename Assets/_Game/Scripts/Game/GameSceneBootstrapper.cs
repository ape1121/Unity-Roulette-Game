using Ape.Core;
using Ape.Scenes;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Ape.Game
{
    [MovedFrom(false, sourceNamespace: "")]
    public sealed class GameSceneBootstrapper : SceneBootstrapper
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private RouletteWheelUI _rouletteWheel;

        protected override void BootstrapScene()
        {
            App.Game.PrepareForSceneLoad();
            App.Game.BindScene(new GameSceneDependencies(_mainCamera, _rouletteWheel));
            App.Game.StartGame();
        }
    }
}
