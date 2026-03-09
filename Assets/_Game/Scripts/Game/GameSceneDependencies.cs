using UnityEngine;

namespace Ape.Game
{
    public readonly struct GameSceneDependencies
    {
        public Camera MainCamera { get; }
        public RouletteWheelUI RouletteWheel { get; }

        public GameSceneDependencies(Camera mainCamera, RouletteWheelUI rouletteWheel)
        {
            MainCamera = mainCamera;
            RouletteWheel = rouletteWheel;
        }
    }
}
