using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AppSceneManager : IManager
{
    private bool _isLoading;
    private bool _isSceneBootstrapComplete;
    private SceneBootstrapper _activeSceneBootstrapper;
    private LoadingScreenView _activeLoadingScreen;

    public IEnumerator LoadGameSceneAsync(LoadingScreenView loadingScreen)
    {
        if (_isLoading)
            yield break;

        _isLoading = true;
        _isSceneBootstrapComplete = false;
        _activeSceneBootstrapper = null;
        _activeLoadingScreen = loadingScreen;

        try
        {
            yield return loadingScreen.PlayEnterTransition("Loading game");
            App.Game.PrepareForSceneLoad();

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(App.Config.GameSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
                throw new MissingReferenceException($"Failed to start loading scene '{App.Config.GameSceneName}'.");

            loadOperation.allowSceneActivation = false;

            while (loadOperation.progress < 0.9f)
            {
                loadingScreen.SetProgress(loadOperation.progress / 0.9f);
                yield return null;
            }

            loadingScreen.SetStatus("Bootstrapping scene");
            loadingScreen.SetProgress(1f);

            loadOperation.allowSceneActivation = true;

            while (!loadOperation.isDone)
                yield return null;

            float waitTimeoutSeconds = 5f;
            float elapsed = 0f;

            while (!_isSceneBootstrapComplete)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= waitTimeoutSeconds)
                {
                    if (_activeSceneBootstrapper == null)
                        throw new TimeoutException("Timed out waiting for scene bootstrapper registration.");

                    throw new TimeoutException($"Timed out waiting for {_activeSceneBootstrapper.GetType().Name} to complete scene bootstrap.");
                }

                yield return null;
            }
        }
        finally
        {
            _activeLoadingScreen = null;
            _activeSceneBootstrapper = null;
            _isSceneBootstrapComplete = false;
            _isLoading = false;
        }
    }

    public void RegisterSceneBootstrapper(SceneBootstrapper sceneBootstrapper)
    {
        if (sceneBootstrapper == null)
            throw new MissingReferenceException("A valid scene bootstrapper is required.");

        if (!_isLoading)
            throw new InvalidOperationException("A scene bootstrapper can only register during an active scene load.");

        if (_activeSceneBootstrapper != null && _activeSceneBootstrapper != sceneBootstrapper)
            throw new InvalidOperationException($"Scene load is already owned by {_activeSceneBootstrapper.GetType().Name}.");

        _activeSceneBootstrapper = sceneBootstrapper;
    }

    public IEnumerator CompleteSceneBootstrapAsync(SceneBootstrapper sceneBootstrapper)
    {
        if (sceneBootstrapper == null)
            throw new MissingReferenceException("A valid scene bootstrapper is required.");

        if (_isSceneBootstrapComplete)
            yield break;

        if (_activeSceneBootstrapper != sceneBootstrapper)
            throw new InvalidOperationException("Only the active scene bootstrapper can complete the current scene load.");

        if (_activeLoadingScreen != null)
            yield return _activeLoadingScreen.PlayExitTransition();

        _isSceneBootstrapComplete = true;
    }
}
