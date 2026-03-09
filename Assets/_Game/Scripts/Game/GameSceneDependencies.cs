using UnityEngine;

public readonly struct GameSceneDependencies
{
    public Camera MainCamera { get; }

    public GameSceneDependencies(Camera mainCamera)
    {
        MainCamera = mainCamera;
    }
}
