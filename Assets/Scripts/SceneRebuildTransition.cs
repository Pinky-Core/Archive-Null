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

        private void Awake()
        {
            SetCanvasGroup(fadeFromBlack, 1f, true);
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

            if (blackHoldDuration > 0f)
            {
                yield return new WaitForSeconds(blackHoldDuration);
            }

            bool rebuildCompleted = officeDissolveTransition == null;
            if (officeDissolveTransition != null)
            {
                officeDissolveTransition.PrepareRebuildStart();
                StartCoroutine(PlayRebuildRoutine(() => rebuildCompleted = true));
            }

            yield return FadeCanvasGroup(fadeFromBlack, 1f, 0f, blackFadeOutDuration, true, false);

            while (!rebuildCompleted)
            {
                yield return null;
            }

            SetCanvasGroup(fadeFromBlack, 0f, false);
        }

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
                timer += Time.deltaTime;
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
