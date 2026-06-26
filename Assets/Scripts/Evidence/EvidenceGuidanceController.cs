using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceGuidanceController : MonoBehaviour
    {
        private const float DefaultHintDelay = 180f;
        private const string MainMenuScene = "MainMenu";

        [SerializeField] private float inactivityHintDelay = DefaultHintDelay;
        [SerializeField] private float subtitleDuration = 6f;
        [SerializeField] private float hintDuration = 12f;

        private CanvasGroup subtitleGroup;
        private TMP_Text subtitleText;
        private CanvasGroup hintGroup;
        private TMP_Text hintText;
        private CanvasGroup objectiveGroup;
        private TMP_Text objectiveText;
        private Image waypoint;
        private TMP_Text waypointLabel;
        private Camera playerCamera;
        private EvidenceTarget hintedTarget;
        private float lastProgressTime;
        private Coroutine subtitleRoutine;
        private Coroutine hintRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || string.Equals(scene.name, MainMenuScene, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindObjectOfType<EvidenceGuidanceController>() == null)
            {
                new GameObject("EvidenceGuidance").AddComponent<EvidenceGuidanceController>();
            }
        }

        private void Awake()
        {
            playerCamera = Camera.main;
            lastProgressTime = Time.unscaledTime;
            BuildUi();
            RefreshObjective();
        }

        private void OnEnable()
        {
            EvidenceInventory.Instance.OnEvidenceRegistered += HandleEvidenceRegistered;
        }

        private void OnDisable()
        {
            if (EvidenceInventory.ExistingInstance != null)
            {
                EvidenceInventory.ExistingInstance.OnEvidenceRegistered -= HandleEvidenceRegistered;
            }
        }

        private void Update()
        {
            if (hintedTarget != null)
            {
                UpdateWaypoint();
            }

            if (!PlayerAssistanceSettings.ShouldShowHelp ||
                PhoneEvidenceReader.IsAnyOpen ||
                EvidenceNotebookUI.IsAnyNotebookOpen ||
                Keypad.IsAnyOpen)
            {
                return;
            }

            if (Time.unscaledTime - lastProgressTime >= inactivityHintDelay)
            {
                lastProgressTime = Time.unscaledTime;
                ShowNextHint();
            }
        }

        private void HandleEvidenceRegistered(EvidenceData data)
        {
            lastProgressTime = Time.unscaledTime;
            HideHint();
            string line = data != null ? data.narrativeLine : string.Empty;
            if (string.IsNullOrWhiteSpace(line) && data != null)
            {
                line = string.IsNullOrWhiteSpace(data.description)
                    ? "Esto puede ser importante: " + data.evidenceName + "."
                    : data.description;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                ShowSubtitle(line);
            }

            RefreshObjective();
        }

        private void ShowNextHint()
        {
            hintedTarget = FindNearestPendingEvidence();
            if (hintedTarget == null)
            {
                ShowHint("No parece quedar evidencia directa por registrar. Revisa la galeria y conecta lo que ya encontraste.");
                return;
            }

            EvidenceData data = hintedTarget.EvidenceData;
            string clue = data != null ? data.hintText : string.Empty;
            if (string.IsNullOrWhiteSpace(clue))
            {
                string evidenceName = data != null && !string.IsNullOrWhiteSpace(data.evidenceName)
                    ? data.evidenceName
                    : hintedTarget.gameObject.name;
                clue = "Todavia no revisaste todo. Busca cerca de " + evidenceName + ".";
            }

            ShowHint(clue);
        }

        private EvidenceTarget FindNearestPendingEvidence()
        {
            playerCamera ??= Camera.main;
            Vector3 origin = playerCamera != null ? playerCamera.transform.position : Vector3.zero;
            EvidenceTarget[] targets = FindObjectsOfType<EvidenceTarget>(true);
            EvidenceTarget nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (EvidenceTarget target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                EvidenceData data = target.EvidenceData;
                if (data == null || EvidenceInventory.Instance.HasEvidence(data.evidenceId))
                {
                    continue;
                }

                float distance = (target.transform.position - origin).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest;
        }

        private void ShowSubtitle(string message)
        {
            if (subtitleRoutine != null)
            {
                StopCoroutine(subtitleRoutine);
            }

            subtitleRoutine = StartCoroutine(ShowGroupRoutine(subtitleGroup, subtitleText, message, subtitleDuration));
        }

        private void ShowHint(string message)
        {
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
            }

            hintRoutine = StartCoroutine(HintRoutine(message));
        }

        private IEnumerator HintRoutine(string message)
        {
            hintText.text = "PISTA\n" + message;
            hintGroup.gameObject.SetActive(true);
            hintGroup.alpha = 1f;
            SetWaypointVisible(hintedTarget != null);
            yield return new WaitForSecondsRealtime(hintDuration);
            HideHint();
            hintRoutine = null;
        }

        private static IEnumerator ShowGroupRoutine(CanvasGroup group, TMP_Text text, string message, float duration)
        {
            text.text = message;
            group.gameObject.SetActive(true);
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(duration);
            float timer = 0f;
            while (timer < 0.25f)
            {
                timer += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(timer / 0.25f);
                yield return null;
            }

            group.gameObject.SetActive(false);
        }

        private void HideHint()
        {
            hintedTarget = null;
            if (hintGroup != null)
            {
                hintGroup.alpha = 0f;
                hintGroup.gameObject.SetActive(false);
            }

            SetWaypointVisible(false);
        }

        private void UpdateWaypoint()
        {
            playerCamera ??= Camera.main;
            if (playerCamera == null || hintedTarget == null)
            {
                SetWaypointVisible(false);
                return;
            }

            Vector3 screen = playerCamera.WorldToScreenPoint(hintedTarget.transform.position + Vector3.up * 0.35f);
            if (screen.z < 0f)
            {
                screen.x = Screen.width - screen.x;
                screen.y = Screen.height - screen.y;
            }

            float margin = 48f;
            screen.x = Mathf.Clamp(screen.x, margin, Screen.width - margin);
            screen.y = Mathf.Clamp(screen.y, margin, Screen.height - margin);
            waypoint.rectTransform.position = screen;
            waypointLabel.rectTransform.position = screen + new Vector3(0f, 28f);
            SetWaypointVisible(true);
        }

        private void BuildUi()
        {
            GameObject canvasObject = new("EvidenceGuidanceCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            subtitleGroup = CreatePanel("NarrativeSubtitle", canvasObject.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(980f, 92f), new Vector2(0f, 52f));
            subtitleText = CreateText(subtitleGroup.transform as RectTransform, 25f, TextAlignmentOptions.Center);

            hintGroup = CreatePanel("InactivityHint", canvasObject.transform as RectTransform, new Vector2(0f, 1f), new Vector2(520f, 112f), new Vector2(42f, -42f));
            hintText = CreateText(hintGroup.transform as RectTransform, 20f, TextAlignmentOptions.TopLeft);
            hintText.color = new Color(0.96f, 0.88f, 0.63f);

            objectiveGroup = CreatePanel("CurrentObjective", canvasObject.transform as RectTransform, new Vector2(1f, 1f), new Vector2(470f, 88f), new Vector2(-42f, -42f));
            objectiveText = CreateText(objectiveGroup.transform as RectTransform, 18f, TextAlignmentOptions.TopLeft);
            objectiveText.color = new Color(0.78f, 0.92f, 0.88f);

            waypoint = CreateImage("HintWaypoint", canvasObject.transform as RectTransform, new Color(1f, 0.78f, 0.24f));
            waypoint.rectTransform.sizeDelta = new Vector2(18f, 18f);
            waypointLabel = CreateText(canvasObject.transform as RectTransform, 17f, TextAlignmentOptions.Center);
            waypointLabel.text = "REVISAR";
            waypointLabel.color = new Color(1f, 0.84f, 0.38f);
            waypointLabel.rectTransform.sizeDelta = new Vector2(150f, 30f);

            subtitleGroup.gameObject.SetActive(false);
            hintGroup.gameObject.SetActive(false);
            SetWaypointVisible(false);
        }

        private void RefreshObjective()
        {
            if (objectiveGroup == null || objectiveText == null)
            {
                return;
            }

            if (!PlayerAssistanceSettings.ShouldShowHelp)
            {
                objectiveGroup.gameObject.SetActive(false);
                return;
            }

            int evidenceCount = EvidenceInventory.Instance.GetAllEvidence().Count;
            objectiveGroup.gameObject.SetActive(true);
            objectiveText.text = evidenceCount switch
            {
                0 => "OBJETIVO ACTUAL\nExplora la casa y registra la primera evidencia.",
                1 => "OBJETIVO ACTUAL\nBusca relaciones: revisa objetos, mensajes y rastros ocultos.",
                _ => $"OBJETIVO ACTUAL\nEvidencias registradas: {evidenceCount}. Revisa la galeria y sigue explorando."
            };
        }

        private static CanvasGroup CreatePanel(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            Image image = CreateImage(name, parent, new Color(0.025f, 0.028f, 0.03f, 0.9f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            CanvasGroup group = image.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.94f, 0.92f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 14f);
            rect.offsetMax = new Vector2(-24f, -14f);
            return text;
        }

        private void SetWaypointVisible(bool visible)
        {
            if (waypoint != null) waypoint.gameObject.SetActive(visible);
            if (waypointLabel != null) waypointLabel.gameObject.SetActive(visible);
        }
    }
}
