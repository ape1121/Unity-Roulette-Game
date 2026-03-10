using System;
using System.Collections;
using Ape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ape.Scenes
{
    public sealed class AppSceneManager : IManager
    {
        private const float DelayProgressPortion = 0.35f;

        public event Action<string> SceneLoadStarted;
        public event Action<string> SceneLoadCompleted;
        public event Action<float> SceneLoadProgressChanged;

        private bool _isLoading;
        private Coroutine _loadSceneCoroutine;

        public void Initialize()
        {
            _isLoading = false;
            _loadSceneCoroutine = null;

            LoadingScreenView loadingScreen = App.Dependencies.LoadingScreen;
            if (loadingScreen != null)
            {
                loadingScreen.Initialize();
                loadingScreen.Bind(this);
            }
        }

        public bool LoadScene(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
        {
            if (_isLoading)
                return false;

            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("A valid scene name is required.", nameof(sceneName));

            _isLoading = true;
            SceneLoadStarted?.Invoke(sceneName);
            ReportProgress(0f);
            _loadSceneCoroutine = App.Instance.StartCoroutine(LoadSceneRoutine(sceneName, loadSceneMode));
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode loadSceneMode)
        {
            float delay = Mathf.Max(0f, App.Config.SceneTransitionDuration);
            float delayProgressPortion = delay > 0f ? DelayProgressPortion : 0f;
            if (delay > 0f)
            {
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float normalizedDelayProgress = Mathf.Clamp01(elapsed / delay);
                    ReportProgress(normalizedDelayProgress * delayProgressPortion);
                    yield return null;
                }
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            if (loadOperation == null)
            {
                _isLoading = false;
                _loadSceneCoroutine = null;
                throw new MissingReferenceException($"Failed to start loading scene '{sceneName}'.");
            }

            while (!loadOperation.isDone)
            {
                float normalizedLoadProgress = Mathf.Clamp01(loadOperation.progress / 0.9f);
                ReportProgress(Mathf.Lerp(delayProgressPortion, 1f, normalizedLoadProgress));
                yield return null;
            }

            ReportProgress(1f);
            _loadSceneCoroutine = null;
            CompleteLoad(sceneName);
        }

        private void CompleteLoad(string sceneName)
        {
            _isLoading = false;
            SceneLoadCompleted?.Invoke(sceneName);
        }

        private void ReportProgress(float progress)
        {
            SceneLoadProgressChanged?.Invoke(Mathf.Clamp01(progress));
        }

        public void Shutdown()
        {
            if (_loadSceneCoroutine != null && App.Instance != null)
                App.Instance.StopCoroutine(_loadSceneCoroutine);

            LoadingScreenView loadingScreen = App.Dependencies.LoadingScreen;
            if (loadingScreen != null)
                loadingScreen.Unbind();

            SceneLoadStarted = null;
            SceneLoadCompleted = null;
            SceneLoadProgressChanged = null;
            _isLoading = false;
            _loadSceneCoroutine = null;
        }
    }
}
