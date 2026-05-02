using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class MemorySceneLoader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OfficeDissolveTransition officeDissolveTransition;

        [Header("Scene")]
        [SerializeField] private string memorySceneName = "Memory_01";
        [SerializeField] private float loadDelay = 0f;

        public void StartMemory()
        {
            StartCoroutine(PlayMemory(memorySceneName));
        }

        public void StartMemory(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[MemorySceneLoader] Cannot start memory. Scene name is empty.");
                return;
            }

            StartCoroutine(PlayMemory(sceneName));
        }

        public IEnumerator PlayMemory(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[MemorySceneLoader] Cannot start memory. Scene name is empty.");
                yield break;
            }

            Debug.Log($"[MemorySceneLoader] Starting memory transition to scene '{sceneName}'.");

            if (officeDissolveTransition != null)
            {
                yield return officeDissolveTransition.PlayDissolve();
            }
            else
            {
                Debug.LogWarning("[MemorySceneLoader] No OfficeDissolveTransition assigned. Loading scene directly.");
            }

            if (loadDelay > 0f)
            {
                Debug.Log($"[MemorySceneLoader] Waiting {loadDelay:0.00}s before scene load.");
                yield return new WaitForSeconds(loadDelay);
            }

            Debug.Log($"[MemorySceneLoader] Loading scene '{sceneName}'.");
            SceneManager.LoadScene(sceneName);
        }
    }
}
