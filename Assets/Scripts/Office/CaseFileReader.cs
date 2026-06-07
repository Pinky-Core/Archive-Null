using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class CaseFileReader : MonoBehaviour
    {
        [System.Serializable]
        public sealed class CaseFilePage
        {
            public string title;
            [TextArea(5, 14)] public string body;
        }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private Transform fileVisual;
        [SerializeField] private Transform readPose;
        [SerializeField] private CRTMenuCameraFocus computerFocus;
        [SerializeField] private VRHeadsetArchiveStarter vrHeadset;

        [Header("Interaction")]
        [SerializeField] private float interactionDistance = 4f;
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private Key closeKey = Key.Escape;
        [SerializeField] private Key nextPageKey = Key.RightArrow;
        [SerializeField] private Key previousPageKey = Key.LeftArrow;
        [SerializeField] private string hoverPrompt = "LEER EXPEDIENTE";

        [Header("Animation")]
        [SerializeField] private float moveDuration = 0.45f;
        [SerializeField] private Vector3 cameraReadOffset = new Vector3(0f, -0.18f, 0.62f);
        [SerializeField] private Vector3 cameraReadEulerOffset = new Vector3(68f, 0f, 0f);
        [SerializeField] private Vector3 liftControlOffset = new Vector3(0f, 0.22f, 0f);
        [SerializeField] private float readScaleMultiplier = 1.08f;
        [SerializeField] private AnimationCurve moveEase;

        [Header("Pages")]
        [SerializeField] private CaseFilePage[] pages =
        {
            new()
            {
                title = "EXPEDIENTE: LA LLAVE POR DENTRO",
                body = "Victima: Julian Herrera, 41 anos.\nLugar: casa familiar Herrera.\nEstado inicial: muerte en interior cerrado.\n\nLa escena fue clasificada como posible suicidio, pero hay inconsistencias suficientes para montar una memoria investigable."
            },
            new()
            {
                title = "RESUMEN INICIAL",
                body = "La puerta principal fue encontrada cerrada desde adentro. Cerca del cuerpo habia un frasco de pastillas y un mensaje breve enviado a Sofia Roldan.\n\nObjetivo del operador: registrar evidencias, separar pistas reales de pistas circunstanciales y evitar una acusacion prematura."
            },
            new()
            {
                title = "VICTIMA: JULIAN HERRERA",
                body = "Arquitecto. Administraba la casa familiar y llevaba registros escritos de temas legales y obras pendientes.\n\nPerfil preliminar: metodico, reservado, con conflictos familiares recientes por la propiedad."
            },
            new()
            {
                title = "NICOLAS HERRERA",
                body = "Hermano menor de Julian. Tenia interes en vender la casa familiar.\n\nDato preliminar: discutio con la victima durante el ultimo dia conocido. Motivo economico posible, sin confirmacion de presencia durante la muerte."
            },
            new()
            {
                title = "SOFIA ROLDAN",
                body = "Expareja de Julian. Recibio el mensaje final y conserva vinculos emocionales con la victima.\n\nDato preliminar: hubo una discusion previa. No asumir culpabilidad por cercania emocional."
            },
            new()
            {
                title = "VICTOR SALAS",
                body = "Vecino y antiguo contratista vinculado a trabajos en la casa.\n\nDato preliminar: testigo secundario. Conocia accesos y rutinas del lugar por trabajos anteriores."
            },
            new()
            {
                title = "ORDEN DE TRABAJO",
                body = "1. Entrar a la memoria disponible.\n2. Observar la sala y la cocina.\n3. Fotografiar evidencias.\n4. Usar UV si una superficie parece limpia o manipulada.\n5. Volver a la oficina y revisar el tablero antes de acusar."
            }
        };

        [Header("Runtime UI")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private TMP_Text promptText;

        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private bool originalActiveSelf;
        private int pageIndex;
        private bool open;
        private bool busy;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static bool IsAnyCaseFileOpen { get; private set; }

        private void Reset()
        {
            targetCamera = Camera.main;
            fileVisual = transform;
            interactionCollider = GetComponentInChildren<Collider>();
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (fileVisual == null)
            {
                fileVisual = transform;
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponentInChildren<Collider>();
            }

            if (moveEase == null || moveEase.length == 0)
            {
                moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (fileVisual != null)
            {
                originalLocalPosition = fileVisual.localPosition;
                originalLocalRotation = fileVisual.localRotation;
                originalLocalScale = fileVisual.localScale;
                originalActiveSelf = fileVisual.gameObject.activeSelf;
            }

            if (rootGroup == null)
            {
                CreateRuntimeUi();
            }

            SetUiVisible(false);
            SetPromptVisible(false);
        }

        private void OnDisable()
        {
            if (open)
            {
                RestoreCursor();
            }

            open = false;
            busy = false;
            IsAnyCaseFileOpen = false;
        }

        private void Update()
        {
            if (busy)
            {
                return;
            }

            if (open)
            {
                HandleOpenInput();
                return;
            }

            bool hovering = CanInteract() && IsHoveringFile();
            SetPromptVisible(hovering);
            if (hovering && WasInteractPressed())
            {
                StartCoroutine(OpenRoutine());
            }
        }

        private void HandleOpenInput()
        {
            if (WasPressed(closeKey) || WasPressed(interactKey))
            {
                StartCoroutine(CloseRoutine());
                return;
            }

            bool nextPressed = WasPressed(nextPageKey) || WasPressed(Key.D) || WasPressed(Key.E) ||
                               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            bool previousPressed = WasPressed(previousPageKey) || WasPressed(Key.A) ||
                                   (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

            if (nextPressed)
            {
                SelectPage(pageIndex + 1);
            }
            else if (previousPressed)
            {
                SelectPage(pageIndex - 1);
            }
        }

        private bool CanInteract()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || interactionCollider == null)
            {
                return false;
            }

            if (computerFocus != null && (computerFocus.IsFocused || computerFocus.IsTransitioning))
            {
                return false;
            }

            return vrHeadset == null || !vrHeadset.IsEquipped;
        }

        private bool IsHoveringFile()
        {
            if (Mouse.current == null)
            {
                return false;
            }

            Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return interactionCollider.Raycast(ray, out _, interactionDistance);
        }

        private IEnumerator OpenRoutine()
        {
            busy = true;
            open = true;
            IsAnyCaseFileOpen = true;
            SetPromptVisible(false);
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            yield return AnimateFile(true);
            pageIndex = 0;
            RefreshPage();
            SetUiVisible(true);
            busy = false;
        }

        private IEnumerator CloseRoutine()
        {
            busy = true;
            SetUiVisible(false);
            yield return AnimateFile(false);

            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }

            RestoreCursor();
            open = false;
            busy = false;
            IsAnyCaseFileOpen = false;
        }

        private IEnumerator AnimateFile(bool toReadPose)
        {
            if (fileVisual == null)
            {
                yield break;
            }

            fileVisual.gameObject.SetActive(true);
            Vector3 startPosition = fileVisual.position;
            Quaternion startRotation = fileVisual.rotation;
            Vector3 startScale = fileVisual.localScale;

            Vector3 endPosition;
            Quaternion endRotation;
            Vector3 endScale;

            if (toReadPose)
            {
                Transform target = readPose != null ? readPose : targetCamera.transform;
                endPosition = readPose != null ? readPose.position : target.TransformPoint(cameraReadOffset);
                endRotation = readPose != null ? readPose.rotation : target.rotation * Quaternion.Euler(cameraReadEulerOffset);
                endScale = originalLocalScale * readScaleMultiplier;
            }
            else
            {
                endPosition = fileVisual.parent != null ? fileVisual.parent.TransformPoint(originalLocalPosition) : originalLocalPosition;
                endRotation = fileVisual.parent != null ? fileVisual.parent.rotation * originalLocalRotation : originalLocalRotation;
                endScale = originalLocalScale;
            }

            Vector3 control = Vector3.Lerp(startPosition, endPosition, 0.5f) + liftControlOffset;
            float timer = 0f;
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = moveEase.Evaluate(Mathf.Clamp01(timer / Mathf.Max(0.001f, moveDuration)));
                fileVisual.position = QuadraticBezier(startPosition, control, endPosition, t);
                fileVisual.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                fileVisual.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            fileVisual.position = endPosition;
            fileVisual.rotation = endRotation;
            fileVisual.localScale = endScale;

            if (!toReadPose)
            {
                fileVisual.localPosition = originalLocalPosition;
                fileVisual.localRotation = originalLocalRotation;
                fileVisual.localScale = originalLocalScale;
                fileVisual.gameObject.SetActive(originalActiveSelf);
            }
        }

        private void SelectPage(int index)
        {
            if (pages == null || pages.Length == 0)
            {
                pageIndex = 0;
            }
            else
            {
                pageIndex = (index % pages.Length + pages.Length) % pages.Length;
            }

            RefreshPage();
        }

        private void RefreshPage()
        {
            CaseFilePage page = pages != null && pages.Length > 0 ? pages[Mathf.Clamp(pageIndex, 0, pages.Length - 1)] : null;
            if (titleText != null)
            {
                titleText.text = page != null ? page.title : "EXPEDIENTE";
            }

            if (bodyText != null)
            {
                bodyText.text = page != null ? page.body : "No hay paginas configuradas.";
            }

            if (counterText != null)
            {
                counterText.text = pages != null && pages.Length > 0 ? $"{pageIndex + 1:00}/{pages.Length:00}" : "00/00";
            }
        }

        private void RestoreCursor()
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void SetUiVisible(bool visible)
        {
            if (rootGroup == null)
            {
                return;
            }

            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
            rootGroup.gameObject.SetActive(visible);
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.gameObject.SetActive(visible);
            promptText.text = $"({interactKey.ToString().ToUpperInvariant()}) {hoverPrompt}";
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new("CaseFileCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            Image panel = CreateImage("PagePanel", canvasRect, new Color(0.04f, 0.045f, 0.04f, 0.94f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -12f);
            panelRect.sizeDelta = new Vector2(940f, 620f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.47f, 0.74f, 0.68f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            rootGroup = panel.gameObject.AddComponent<CanvasGroup>();

            titleText = CreateText("Title", panelRect, string.Empty, 30f, TextAlignmentOptions.TopLeft);
            titleText.color = new Color(0.8f, 0.96f, 0.9f, 1f);
            SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(56f, 512f), new Vector2(-56f, -32f));

            bodyText = CreateText("Body", panelRect, string.Empty, 23f, TextAlignmentOptions.TopLeft);
            bodyText.color = new Color(0.86f, 0.9f, 0.84f, 1f);
            SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(56f, 96f), new Vector2(-56f, -120f));

            counterText = CreateText("Counter", panelRect, "00/00", 18f, TextAlignmentOptions.Bottom);
            counterText.color = new Color(0.62f, 0.78f, 0.72f, 1f);
            SetRect(counterText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0f), new Vector2(390f, 38f), new Vector2(-390f, -548f));

            TMP_Text footer = CreateText("Footer", panelRect, "CLICK / DERECHA: SIGUIENTE  //  A / IZQUIERDA: ANTERIOR  //  ESC: CERRAR", 17f, TextAlignmentOptions.BottomRight);
            footer.color = new Color(0.62f, 0.78f, 0.72f, 0.86f);
            SetRect(footer.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(56f, 34f), new Vector2(-56f, -548f));

            promptText = CreateText("Prompt", canvasRect, string.Empty, 22f, TextAlignmentOptions.Center);
            promptText.color = new Color(0.78f, 0.96f, 0.92f, 1f);
            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, -110f);
            promptRect.sizeDelta = new Vector2(420f, 42f);
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static bool WasPressed(Key key)
        {
            return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        private bool WasInteractPressed()
        {
            return WasPressed(interactKey) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            Vector3 ab = Vector3.Lerp(a, b, t);
            Vector3 bc = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(ab, bc, t);
        }
    }
}
