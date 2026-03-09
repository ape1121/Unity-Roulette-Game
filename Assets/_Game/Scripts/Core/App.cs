using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class App : MonoBehaviour
{
    public static App               Instance { get; private set; }
    public static AppConfig         Config { get; private set; }
    public static GameManager       Game { get; private set; }
    public static AppSceneManager   Scenes { get; private set; }
    public static SaveManager       Saver { get; private set; }

    [SerializeField] private AppConfig _appConfig;
    [SerializeField] private LoadingScreenView _loadingScreen;

    private Coroutine _startupRoutine;
    private List<IManager> _managers = new List<IManager>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Config = _appConfig;
        DontDestroyOnLoad(gameObject);
        InitializeManagers();
    }

    private void Start()
    {
        _startupRoutine = StartCoroutine(BeginStartupFlow());
    }

    private void InitializeManagers()
    {
        Saver = new SaveManager();
        Game = new GameManager();
        Scenes = new AppSceneManager();

        _managers.Clear();
        _managers.Add(Saver);
        _managers.Add(Game);
        _managers.Add(Scenes);

        foreach (IManager manager in _managers)
            manager.Initialize();
    }

    private IEnumerator BeginStartupFlow()
    {
        yield return Scenes.LoadGameSceneAsync(_loadingScreen);
        _startupRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (_startupRoutine != null)
            StopCoroutine(_startupRoutine);

        foreach (IManager manager in _managers)
            manager.Shutdown();

        Instance = null;
        Config = null;
        Game = null;
        Scenes = null;
        Saver = null;
    }
}
