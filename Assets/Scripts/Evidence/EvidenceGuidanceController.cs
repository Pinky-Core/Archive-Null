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
        [SerializeField] private float subtitleDuration = 9f;
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
        private Coroutine followUpRoutine;

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
            string followUp = GetInvestigationFollowUp(EvidenceInventory.Instance.GetAllEvidence().Count);
            if (!string.IsNullOrWhiteSpace(followUp))
            {
                if (followUpRoutine != null) StopCoroutine(followUpRoutine);
                followUpRoutine = StartCoroutine(ShowFollowUpAfterDelay(followUp));
            }
        }

        private IEnumerator ShowFollowUpAfterDelay(string message)
        {
            yield return new WaitForSecondsRealtime(subtitleDuration + 0.6f);
            ShowSubtitle(message);
            followUpRoutine = null;
        }

        private static string GetInvestigationFollowUp(int evidenceCount)
        {
            return evidenceCount switch
            {
                2 => "La escena ya ofrece dos lecturas: una muerte voluntaria y una explicación construida para que parezca voluntaria. Necesito buscar qué objeto fue limpiado o manipulado.",
                4 => "El conflicto familiar explica sospechas, pero todavía no explica el método. La cocina puede conservar lo que alguien intentó borrar de la sala.",
                6 => "Ya puedo sostener que varias pistas fueron acomodadas. Falta conectar método, acceso y motivo antes de señalar a una persona.",
                8 => "Hay evidencia suficiente para volver a la oficina y ordenar una hipótesis provisional. Una acusación correcta debe explicar también por qué los otros sospechosos parecen culpables.",
                _ => string.Empty
            };
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

            if (CrimeSceneTutorial.IsActive)
            {
                if (objectiveGroup != null)
                {
                    objectiveGroup.gameObject.SetActive(false);
                }
                return;
            }

            if (objectiveGroup != null && !objectiveGroup.gameObject.activeSelf)
            {
                RefreshObjective();
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
            string line = BuildNarrativeLine(data);
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

        private static string BuildNarrativeLine(EvidenceData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(data.narrativeLine))
            {
                return data.narrativeLine;
            }

            string identity = ((data.evidenceId ?? string.Empty) + " " + (data.evidenceName ?? string.Empty)).ToLowerInvariant();
            if (identity.Contains("pastilla") || identity.Contains("frasco"))
                return "El frasco está demasiado expuesto, casi colocado para ser encontrado. Si Julián lo manipuló, deberían quedar huellas claras.";
            if (identity.Contains("phone_messages") || identity.Contains("mensaje"))
                return "Julián escribía mensajes largos y explicativos. Esta despedida es breve, terminante y ajena a su forma habitual de hablar.";
            if (identity.Contains("phone_call") || identity.Contains("llamada"))
                return "El registro de llamadas fija contactos y horarios. Tengo que compararlo con la ventana probable de muerte, no leerlo como una acusación aislada.";
            if (identity.Contains("telefono") || identity.Contains("celular"))
                return "Es el teléfono de Julián. El mensaje final puede explicar la escena o demostrar que alguien intentó explicarla por él.";
            if (identity.Contains("copa") || identity.Contains("vaso"))
                return "Una huella en una copa demuestra presencia, no asesinato. Sofía estuvo aquí antes; necesito determinar cuándo y por qué.";
            if (identity.Contains("foto") || identity.Contains("fotograf"))
                return "La fotografía dañada confirma un conflicto familiar, pero un conflicto no establece quién estuvo aquí durante la muerte.";
            if (identity.Contains("barro") || identity.Contains("huella"))
                return "La marca conserva un patrón parcial. Si encuentro un calzado compatible, podré vincular movimiento y acceso, no solo una identidad.";
            if (identity.Contains("taza"))
                return "La taza fue lavada después de usarse. Limpiar un objeto de la escena también es una decisión y deja una secuencia detrás.";
            if (identity.Contains("azucar") || identity.Contains("polvo"))
                return "Esto no parece azúcar pura. Si el medicamento fue triturado y mezclado, el frasco junto al cuerpo sería una puesta en escena.";
            if (identity.Contains("guante"))
                return "Los guantes explican la ausencia de huellas en otros objetos. Todavía no indican quién los utilizó.";
            if (identity.Contains("farmacia") || identity.Contains("recibo"))
                return "La compra fue en efectivo y no identifica al cliente. Sirve para reconstruir el método, no para cerrar al culpable.";
            if (identity.Contains("utensilio") || identity.Contains("tritur"))
                return "Hay restos de medicación triturada. Esto conecta preparación, bebida y una intoxicación que debía parecer voluntaria.";
            return string.Empty;
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
            Canvas subtitleCanvas = subtitleGroup.gameObject.AddComponent<Canvas>();
            subtitleCanvas.overrideSorting = true;
            subtitleCanvas.sortingOrder = 20000;
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
