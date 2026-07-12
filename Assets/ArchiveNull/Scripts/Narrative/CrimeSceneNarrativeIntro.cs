using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Narrative
{
    [DisallowMultipleComponent]
    public sealed class CrimeSceneNarrativeIntro : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";
        private const string CompletedPref = "archive.narrative.memory01.intro.completed";
        private CanvasGroup group;
        private TMP_Text speakerText;
        private TMP_Text dialogueText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name == MainMenuScene || PlayerPrefs.GetInt(CompletedPref, 0) == 1)
            {
                return;
            }

            if (FindAnyObjectByType<ArchiveNull.Evidence.EvidenceCameraController>() == null || FindAnyObjectByType<CrimeSceneNarrativeIntro>() != null)
            {
                return;
            }

            new GameObject("CrimeSceneNarrativeIntro").AddComponent<CrimeSceneNarrativeIntro>();
        }

        private void Awake()
        {
            BuildUi();
            StartCoroutine(PlayIntroduction());
        }

        private IEnumerator PlayIntroduction()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return ShowLine(GameLocalization.Text("SISTEMA", "SYSTEM"), GameLocalization.Text("Memoria 01 estabilizada. Sala principal de la residencia Herrera. Reconstrucción correspondiente a la noche del 14 de junio.", "Memory 01 stabilized. Main room of the Herrera residence. Reconstruction corresponding to the night of June 14."), 5.5f);
            yield return ShowLine(GameLocalization.Text("OPERADOR 253", "OPERATOR 253"), GameLocalization.Text("Julián Herrera, 41 años. Arquitecto. Fue encontrado muerto aquí, con la puerta principal cerrada desde adentro.", "Julián Herrera, age 41. Architect. He was found dead here, with the front door locked from the inside."), 5.5f);
            yield return ShowLine(GameLocalization.Text("SISTEMA", "SYSTEM"), GameLocalization.Text("La hipótesis inicial indica suicidio: un frasco de pastillas junto al cuerpo y un mensaje final enviado a Sofía Roldán.", "The initial hypothesis indicates suicide: a pill bottle beside the body and a final message sent to Sofía Roldán."), 6f);
            yield return ShowLine(GameLocalization.Text("OPERADOR 253", "OPERATOR 253"), GameLocalization.Text("Pero la escena parece demasiado ordenada. Si alguien quiso imponer una explicación, tuvo que dejar rastros al construirla.", "But the scene looks too orderly. If someone tried to impose an explanation, they must have left traces while constructing it."), 6f);
            yield return ShowLine(GameLocalization.Text("SISTEMA", "SYSTEM"), GameLocalization.Text("Objetivo: registrar la escena, revisar el teléfono y separar evidencia real, circunstancial y posiblemente plantada. No acuse por una sola pista.", "Objective: record the scene, inspect the phone, and separate real, circumstantial, and possibly planted evidence. Do not accuse based on a single clue."), 7f);
            PlayerPrefs.SetInt(CompletedPref, 1);
            PlayerPrefs.Save();
            Destroy(gameObject);
        }

        private IEnumerator ShowLine(string speaker, string line, float duration)
        {
            speakerText.text = speaker;
            dialogueText.text = line;
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(duration);
            float timer = 0f;
            while (timer < 0.25f)
            {
                timer += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(timer / 0.25f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }

        private void BuildUi()
        {
            GameObject canvasObject = new("NarrativeIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Image panel = CreateImage(canvasObject.transform as RectTransform, new Color(0.015f, 0.018f, 0.017f, 0.9f));
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(1120f, 132f);
            speakerText = CreateText(rect, 18f, FontStyles.Bold);
            speakerText.rectTransform.offsetMin = new Vector2(32f, 84f);
            speakerText.color = new Color(0.72f, 0.65f, 0.43f, 1f);
            dialogueText = CreateText(rect, 25f, FontStyles.Normal);
            dialogueText.rectTransform.offsetMin = new Vector2(32f, 18f);
            dialogueText.rectTransform.offsetMax = new Vector2(-32f, -48f);
        }

        private static Image CreateImage(RectTransform parent, Color color)
        {
            GameObject go = new("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, float size, FontStyles style)
        {
            GameObject go = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color(0.9f, 0.9f, 0.84f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32f, 18f);
            rect.offsetMax = new Vector2(-32f, -18f);
            return text;
        }
    }
}
