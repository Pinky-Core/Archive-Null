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
        [SerializeField] private CanvasGroup fadeToBlack;

        [Header("Scene")]
        [SerializeField] private string memorySceneName = "Memory_01";
        [SerializeField] private float loadDelay = 0f;
        [SerializeField] private float fadeToBlackDuration = 0.35f;

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

            if (fadeToBlack != null)
            {
                yield return FadeCanvasGroup(fadeToBlack, fadeToBlack.alpha, 1f, fadeToBlackDuration, true, true);
            }

            if (loadDelay > 0f)
            {
                Debug.Log($"[MemorySceneLoader] Waiting {loadDelay:0.00}s before scene load.");
                yield return new WaitForSeconds(loadDelay);
            }

            Debug.Log($"[MemorySceneLoader] Loading scene '{sceneName}'.");
            SceneManager.LoadScene(sceneName);
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, bool activateAtStart, bool keepActiveAtEnd)
        {
            if (group == null)
            {
                yield break;
            }

            group.alpha = from;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (activateAtStart)
            {
                group.gameObject.SetActive(true);
            }

            if (duration <= 0f)
            {
                group.alpha = to;
                group.gameObject.SetActive(keepActiveAtEnd || to > 0.001f);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
            group.gameObject.SetActive(keepActiveAtEnd || to > 0.001f);
        }
    }
}
