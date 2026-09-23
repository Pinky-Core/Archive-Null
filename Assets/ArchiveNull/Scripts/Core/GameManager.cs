using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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
            if (IsChangingScene || string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return false;
            }

            IsChangingScene = true;
            GameSaveSystem.SaveNow();
            Time.timeScale = 1f;
            SceneChangeStarted?.Invoke(sceneName);
            try { if (!ArchiveNull.UI.MemoryLoadingScreen.Load(sceneName)) { IsChangingScene = false; return false; } }
            catch (Exception exception)
            {
                IsChangingScene = false;
                Debug.LogError("[GameManager] No se pudo cargar la escena: " + exception.Message);
                return false;
            }
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Persistent UI helpers and authored scene UIs must share one event system.
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
            EventSystem selected = null;
            foreach (EventSystem system in systems)
                if (system.isActiveAndEnabled && (selected == null || system.gameObject.scene == scene)) selected = system;
            if (selected != null)
            {
                foreach (EventSystem system in systems)
                    if (system != selected)
                    {
                        system.enabled = false;
                        foreach (BaseInputModule module in system.GetComponents<BaseInputModule>()) module.enabled = false;
                    }
                InputSystemUIInputModule input = selected.GetComponent<InputSystemUIInputModule>();
                if (input == null) { input = selected.gameObject.AddComponent<InputSystemUIInputModule>(); input.AssignDefaultActions(); }
                foreach (BaseInputModule module in selected.GetComponents<BaseInputModule>()) module.enabled = module == input;
            }
            CurrentSceneName = scene.name;
            IsChangingScene = false;
            SceneChangeCompleted?.Invoke(scene);
        }
    }
}
