using UnityEngine;

public sealed class App : MonoBehaviour
{
    public static App               Instance    { get; private set; }
    public static AppConfig         Config      { get; private set; }
    public static GameManager       Game        { get; private set; } = new GameManager();
    public static AppSceneManager   Scenes      { get; private set; } = new AppSceneManager();
    public static SaveManager       Saver        { get; private set; } = new SaveManager();

    [SerializeField] private AppConfig _appConfig;

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
    }
}