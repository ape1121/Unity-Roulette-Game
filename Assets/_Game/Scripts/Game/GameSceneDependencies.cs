using UnityEngine;

namespace Ape.Game
{
    public readonly struct GameSceneDependencies
    {
        public Camera MainCamera { get; }
        public RouletteWheelUI RouletteWheel { get; }
        public GameUIManager UIManager { get; }

        public GameSceneDependencies(Camera mainCamera, RouletteWheelUI rouletteWheel, GameUIManager uiManager)
        {
            MainCamera = mainCamera;
            RouletteWheel = rouletteWheel;
            UIManager = uiManager;
        }
    }
}
