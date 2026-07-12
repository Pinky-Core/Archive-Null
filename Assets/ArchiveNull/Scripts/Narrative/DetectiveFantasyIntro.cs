using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Narrative
{
    public sealed class DetectiveFantasyIntro : MonoBehaviour
    {
        private const string SeenPref = "archive.intro.detective.v2.seen";
        public static bool IsPlaying { get; private set; }
        private CanvasGroup group;
        private TMP_Text title;
        private TMP_Text body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" || PlayerPrefs.GetInt(SeenPref, 0) == 1 || FindAnyObjectByType<DetectiveFantasyIntro>() != null) return;
            new GameObject("DetectiveFantasyIntro").AddComponent<DetectiveFantasyIntro>();
        }

        private void Awake()
        {
            IsPlaying = true;
            BuildUi();
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            group.alpha = 1f;
            yield return Show("ARCHIVE: NULL", GameLocalization.Text("DIVISIÓN DE RECONSTRUCCIÓN FORENSE", "FORENSIC RECONSTRUCTION DIVISION"), 2.8f);
            yield return Show(GameLocalization.Text("OPERADOR 253", "OPERATOR 253"), GameLocalization.Text("Su trabajo es ingresar en reconstrucciones de memoria, documentar evidencia y regresar a la oficina para construir una hipótesis.", "Your work is to enter memory reconstructions, document evidence, and return to the office to build a hypothesis."), 5.5f);
            float timer = 0f;
            while (timer < 1.2f)
            {
                timer += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(timer / 1.2f);
                yield return null;
            }
            PlayerPrefs.SetInt(SeenPref, 1);
            PlayerPrefs.Save();
            IsPlaying = false;
            Destroy(gameObject);
        }

        private IEnumerator Show(string heading, string message, float duration)
        {
            title.text = heading;
            body.text = message;
            yield return new WaitForSecondsRealtime(duration);
        }

        private void OnDestroy() => IsPlaying = false;

        private void BuildUi()
        {
            GameObject canvasObject = new("DetectiveIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            Image background = CreateImage(canvasObject.transform as RectTransform, Color.black);
            Stretch(background.rectTransform);
            title = CreateText(canvasObject.transform as RectTransform, 52f, FontStyles.Normal, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0.15f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.85f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 86f);
            title.rectTransform.sizeDelta = new Vector2(0f, 80f);
            title.color = new Color(0.82f, 0.76f, 0.56f, 1f);
            body = CreateText(canvasObject.transform as RectTransform, 27f, FontStyles.Normal, TextAlignmentOptions.Center);
            body.rectTransform.anchorMin = new Vector2(0.2f, 0.5f);
            body.rectTransform.anchorMax = new Vector2(0.8f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(0f, -35f);
            body.rectTransform.sizeDelta = new Vector2(0f, 150f);
        }

        private static Image CreateImage(RectTransform parent, Color color)
        {
            GameObject go = new("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.9f, 0.84f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
