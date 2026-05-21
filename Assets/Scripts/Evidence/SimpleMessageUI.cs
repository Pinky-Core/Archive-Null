using System.Collections;
using TMPro;
using UnityEngine;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class SimpleMessageUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float visibleDuration = 2f;
        [SerializeField] private float fadeDuration = 0.2f;

        private Coroutine routine;

        private void Awake()
        {
            if (group == null && messageText != null)
            {
                group = messageText.GetComponentInParent<CanvasGroup>();
            }

            SetVisible(false, true);
        }

        public void Configure(TMP_Text text, CanvasGroup canvasGroup)
        {
            messageText = text;
            group = canvasGroup;
            SetVisible(false, true);
        }

        public void ShowMessage(string message)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            SetVisible(true, false);
            yield return new WaitForSeconds(visibleDuration);
            SetVisible(false, false);
            routine = null;
        }

        private void SetVisible(bool visible, bool immediate)
        {
            if (group == null)
            {
                if (messageText != null)
                {
                    messageText.gameObject.SetActive(visible);
                }
                return;
            }

            group.gameObject.SetActive(true);
            if (immediate || fadeDuration <= 0f)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                group.gameObject.SetActive(visible);
                return;
            }

            StartCoroutine(FadeRoutine(visible));
        }

        private IEnumerator FadeRoutine(bool visible)
        {
            float from = group.alpha;
            float to = visible ? 1f : 0f;
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, fadeDuration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(visible);
        }
    }
}
