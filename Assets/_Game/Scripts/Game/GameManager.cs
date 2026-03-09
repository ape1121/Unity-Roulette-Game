using System;
using UnityEngine;

public sealed class GameManager : IManager
{
    public GameConfig Config => App.Config.GameConfig;
    public GameSceneDependencies SceneDependencies { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsSceneBound { get; private set; }

    public void Initialize()
    {
        SceneDependencies = default;
        IsInitialized = true;
        IsSceneBound = false;
    }

    public void PrepareForSceneLoad()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("GameManager must be initialized before preparing for scene load.");

        SceneDependencies = default;
        IsSceneBound = false;
    }

    public void BindScene(GameSceneDependencies sceneDependencies)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("GameManager must be initialized before scene dependencies are bound.");

        if (sceneDependencies.MainCamera == null)
            throw new MissingReferenceException("Game scene dependencies require a main camera reference.");

        SceneDependencies = sceneDependencies;
        IsSceneBound = true;

        Debug.Log("GameManager: Scene dependencies bound successfully.");
    }
}
