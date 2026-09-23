using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class SceneRebuildTransition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OfficeDissolveTransition officeDissolveTransition;
        [SerializeField] private CanvasGroup fadeFromBlack;

        [Header("Playback")]
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private float blackHoldDuration = 0.1f;
        [SerializeField] private float blackFadeOutDuration = 1f;
        [SerializeField] private bool restoreOriginalMaterialsOnComplete = true;
        private Material originalSky;
        private Camera sceneCamera;
        private CameraClearFlags originalClearFlags;
        private Color originalBackground;
        private static SceneRebuildTransition busyOwner;
        public static bool IsBusy => busyOwner != null;

        private void Awake()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") autoPlayOnStart = false;
            if (autoPlayOnStart)
            {
                busyOwner = this;
                originalSky = RenderSettings.skybox;
                RenderSettings.skybox = null;
                sceneCamera = Camera.main;
                if (sceneCamera != null) { originalClearFlags = sceneCamera.clearFlags; originalBackground = sceneCamera.backgroundColor; sceneCamera.clearFlags = CameraClearFlags.SolidColor; sceneCamera.backgroundColor = Color.black; }
            }
            SetCanvasGroup(fadeFromBlack, autoPlayOnStart ? 1f : 0f, autoPlayOnStart);
        }

        private void Start()
        {
            if (autoPlayOnStart)
            {
                StartCoroutine(PlayTransition());
            }
        }

        public IEnumerator PlayTransition()
        {
            SetCanvasGroup(fadeFromBlack, 1f, true);
            while (MemoryLoadingScreen.IsLoadingAssets) yield return null;

            if (!MemoryLoadingScreen.IsBusy && blackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHoldDuration);
            }

            // Hide the environment before removing either blackout, then start the
            // wave only when the player can actually see it.
            if (officeDissolveTransition != null) officeDissolveTransition.PrepareRebuildStart();
            while (MemoryLoadingScreen.IsBusy) yield return null;
            yield return FadeCanvasGroup(fadeFromBlack, 1f, 0f, Mathf.Min(.35f,blackFadeOutDuration), true, false);
            yield return null;
            bool rebuildCompleted = officeDissolveTransition == null;
            if (officeDissolveTransition != null) StartCoroutine(PlayRebuildRoutine(() => rebuildCompleted = true));

            while (!rebuildCompleted)
            {
                yield return null;
            }

            SetCanvasGroup(fadeFromBlack, 0f, false);
            RenderSettings.skybox = originalSky;
            if (sceneCamera != null) { sceneCamera.clearFlags = originalClearFlags; sceneCamera.backgroundColor = originalBackground; }
            if (busyOwner == this) busyOwner = null;
        }
        private void OnDestroy() { if (busyOwner == this) busyOwner = null; }

        private IEnumerator PlayRebuildRoutine(System.Action onCompleted)
        {
            yield return officeDissolveTransition.PlayRebuild(restoreOriginalMaterialsOnComplete);
            onCompleted?.Invoke();
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
                SetCanvasGroup(group, to, keepActiveAtEnd || to > 0.001f);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            SetCanvasGroup(group, to, keepActiveAtEnd || to > 0.001f);
        }

        private static void SetCanvasGroup(CanvasGroup group, float alpha, bool active)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(active);
        }
    }
}
