using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.Core
{
    /// <summary>
    /// Owns application-wide lifecycle state and provides a single entry point for scene changes.
    /// Feature-specific state remains in dedicated systems such as GameSaveSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        private static GameManager instance;

        public static GameManager Instance
        {
            get
            {
                EnsureInstance();
                return instance;
            }
        }

        public static bool HasInstance => instance != null;
        public bool IsChangingScene { get; private set; }
        public string CurrentSceneName { get; private set; }

        public event Action<string> SceneChangeStarted;
        public event Action<Scene> SceneChangeCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new(nameof(GameManager));
            instance = host.AddComponent<GameManager>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        public bool LoadScene(string sceneName)
        {
            if (IsChangingScene || string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            IsChangingScene = true;
            Time.timeScale = 1f;
            SceneChangeStarted?.Invoke(sceneName);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentSceneName = scene.name;
            IsChangingScene = false;
            SceneChangeCompleted?.Invoke(scene);
        }
    }
}
