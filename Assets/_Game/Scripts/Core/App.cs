using System.Collections.Generic;
using UnityEngine;

public sealed class App : MonoBehaviour
{
    public static App               Instance { get; private set; }
    public static AppDependencies   Dependencies { get; private set; }
    public static AppConfig         Config { get; private set; }
    public static GameManager       Game { get; private set; }
    public static AppSceneManager   Scenes { get; private set; }
    public static SaveManager       Saver { get; private set; }
    public static SoundManager      Sound { get; private set; }

    [SerializeField] private AppConfig appConfig;
    [SerializeField] private AppDependencies appDependencies;
    
    private List<IManager> _managers = new List<IManager>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Config = appConfig;
        Dependencies = appDependencies;
        DontDestroyOnLoad(gameObject);
        CreateManagers();
    }

    private void Start()
    {
        InitializeManagers();
        Scenes.LoadScene(Config.GameSceneName);
    }

    private void CreateManagers()
    {
        Saver = new SaveManager();
        Scenes = new AppSceneManager();
        Sound = Dependencies.SoundManager;
        Game = new GameManager();
        
        _managers.Clear();
        _managers.Add(Saver);
        _managers.Add(Scenes);
        _managers.Add(Sound);
        _managers.Add(Game);
    }

    private void InitializeManagers()
    {
        foreach (IManager manager in _managers)
            manager.Initialize();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        foreach (IManager manager in _managers)
            manager.Shutdown();

        Dependencies = default;
        Instance = null;
        Config = null;
        Game = null;
        Scenes = null;
        Saver = null;
    }
}
