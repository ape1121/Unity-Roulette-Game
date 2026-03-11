using System.Collections.Generic;
using Ape.Data;
using Ape.Game;
using Ape.Profile;
using Ape.Scenes;
using Ape.Sounds;
using UnityEngine;

namespace Ape.Core
{
    public sealed class App : MonoBehaviour
    {
        public static App               Instance { get; private set; }
        public static AppDependencies   Dependencies { get; private set; }
        public static AppConfig         Config { get; private set; }
        public static GameManager       Game { get; private set; }
        public static AppSceneManager   Scenes { get; private set; }
        public static ProfileManager    Profile { get; private set; }
        public static SoundManager      Sound { get; private set; }

        [SerializeField] private AppConfig _appConfig;
        [SerializeField] private AppDependencies _appDependencies;

        private readonly List<IManager> _managers = new List<IManager>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Config = _appConfig;
            Dependencies = _appDependencies;
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
            Profile = new ProfileManager();
            Scenes = new AppSceneManager();
            Sound = Dependencies.SoundManager;
            Game = new GameManager();
            Game.Configure(Config != null ? Config.GameConfig : null, Profile);

            _managers.Clear();
            _managers.Add(Profile);
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
            Profile = null;
        }
    }
}
