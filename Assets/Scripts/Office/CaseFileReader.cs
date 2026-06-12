using System.Collections;
using System.Text;
using ArchiveNull.Evidence;
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
            public bool showPortrait;
            public string portraitName;
            public Color portraitAccent = new(0.52f, 0.45f, 0.36f, 1f);
        }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private Transform fileVisual;
        [SerializeField] private Transform folderCover;
        [SerializeField] private Transform folderCoverHinge;
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
        [SerializeField] private bool usePhysicalOpenAnimation;
        [SerializeField] private float moveDuration = 0.45f;
        [SerializeField] private bool alignReadRotationToCamera;
        [SerializeField] private Vector3 cameraReadOffset = new Vector3(0f, -0.18f, 0.62f);
        [SerializeField] private Vector3 cameraReadEulerOffset = new Vector3(68f, 0f, 0f);
        [SerializeField] private Vector3 readPoseEulerOffset;
        [SerializeField] private Vector3 liftControlOffset = new Vector3(0f, 0.22f, 0f);
        [SerializeField] private float readScaleMultiplier = 1.08f;
        [SerializeField] private float openCoverDuration = 0.28f;
        [SerializeField] private Vector3 openCoverLocalEuler = new Vector3(0f, 0f, -105f);
        [SerializeField] private Vector3 coverHingeAxis = Vector3.up;
        [SerializeField] private float openCoverHingeAngle = -105f;
        [SerializeField] private AnimationCurve moveEase;
        [SerializeField] private AnimationCurve coverEase;

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
                body = "Arquitecto. Administraba la casa familiar y llevaba registros escritos de temas legales y obras pendientes.\n\nPerfil preliminar: metodico, reservado, con conflictos familiares recientes por la propiedad.",
                showPortrait = true,
                portraitName = "JULIAN HERRERA",
                portraitAccent = new Color(0.46f, 0.42f, 0.34f, 1f)
            },
            new()
            {
                title = "NICOLAS HERRERA",
                body = "Hermano menor de Julian. Tenia interes en vender la casa familiar.\n\nDato preliminar: discutio con la victima durante el ultimo dia conocido. Motivo economico posible, sin confirmacion de presencia durante la muerte.",
                showPortrait = true,
                portraitName = "NICOLAS HERRERA",
                portraitAccent = new Color(0.33f, 0.43f, 0.48f, 1f)
            },
            new()
            {
                title = "SOFIA ROLDAN",
                body = "Expareja de Julian. Recibio el mensaje final y conserva vinculos emocionales con la victima.\n\nDato preliminar: hubo una discusion previa. No asumir culpabilidad por cercania emocional.",
                showPortrait = true,
                portraitName = "SOFIA ROLDAN",
                portraitAccent = new Color(0.54f, 0.38f, 0.38f, 1f)
            },
            new()
            {
                title = "VICTOR SALAS",
                body = "Vecino y antiguo contratista vinculado a trabajos en la casa.\n\nDato preliminar: testigo secundario. Conocia accesos y rutinas del lugar por trabajos anteriores.",
                showPortrait = true,
                portraitName = "VICTOR SALAS",
                portraitAccent = new Color(0.42f, 0.35f, 0.28f, 1f)
            },
            new()
            {
                title = "ORDEN DE TRABAJO",
                body = "1. Entrar a la memoria disponible.\n2. Observar la sala y la cocina.\n3. Fotografiar evidencias.\n4. Usar UV si una superficie parece limpia o manipulada.\n5. Volver a la oficina y revisar el tablero antes de acusar."
            }
        };

        [Header("Runtime UI")]
        [SerializeField] private bool useGeneratedPaperUi = true;
        [SerializeField] private bool forceScreenSpaceUi = true;
        [SerializeField] private bool detachUiFromFile = true;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private Image backdropImage;
        [SerializeField] private Image portraitDividerImage;
        [SerializeField] private Image portraitFrameImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text portraitLabelText;

        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Vector3 coverClosedLocalPosition;
        private Quaternion coverClosedLocalRotation;
        private Quaternion coverOpenLocalRotation;
        private float coverOpenAmount;
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

            if (coverEase == null || coverEase.length == 0)
            {
                coverEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (fileVisual != null)
            {
                originalLocalPosition = fileVisual.localPosition;
                originalLocalRotation = fileVisual.localRotation;
                originalLocalScale = fileVisual.localScale;
                originalActiveSelf = fileVisual.gameObject.activeSelf;
            }

            if (folderCover != null)
            {
                coverClosedLocalPosition = folderCover.localPosition;
                coverClosedLocalRotation = folderCover.localRotation;
                coverOpenLocalRotation = coverClosedLocalRotation * Quaternion.Euler(openCoverLocalEuler);
            }

            if (useGeneratedPaperUi)
            {
                CreateRuntimeUi();
            }
            else if (rootGroup == null)
            {
                CreateRuntimeUi();
            }
            else if (forceScreenSpaceUi)
            {
                EnsureScreenSpaceUi();
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

            if (usePhysicalOpenAnimation)
            {
                yield return AnimateFile(true);
                yield return AnimateCover(true);
            }

            pageIndex = 0;
            RefreshPage();
            SetUiVisible(true);
            busy = false;
        }

        private IEnumerator CloseRoutine()
        {
            busy = true;
            SetUiVisible(false);
            if (usePhysicalOpenAnimation)
            {
                yield return AnimateCover(false);
                yield return AnimateFile(false);
            }

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
                Transform target = targetCamera != null ? targetCamera.transform : fileVisual;
                endPosition = readPose != null ? readPose.position : target.TransformPoint(cameraReadOffset);
                Quaternion baseRotation = alignReadRotationToCamera || readPose == null ? target.rotation : readPose.rotation;
                endRotation = baseRotation * Quaternion.Euler(cameraReadEulerOffset + readPoseEulerOffset);
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
                if (folderCover != null)
                {
                    ApplyCoverPose(0f);
                }
                fileVisual.gameObject.SetActive(originalActiveSelf);
            }
        }

        private IEnumerator AnimateCover(bool openCover)
        {
            if (folderCover == null)
            {
                yield break;
            }

            float startAmount = coverOpenAmount;
            float endAmount = openCover ? 1f : 0f;
            float timer = 0f;
            while (timer < openCoverDuration)
            {
                timer += Time.deltaTime;
                float t = coverEase.Evaluate(Mathf.Clamp01(timer / Mathf.Max(0.001f, openCoverDuration)));
                ApplyCoverPose(Mathf.Lerp(startAmount, endAmount, t));
                yield return null;
            }

            ApplyCoverPose(endAmount);
        }

        private void ApplyCoverPose(float amount)
        {
            if (folderCover == null)
            {
                return;
            }

            coverOpenAmount = Mathf.Clamp01(amount);
            if (folderCoverHinge == null)
            {
                folderCover.localPosition = coverClosedLocalPosition;
                folderCover.localRotation = Quaternion.Slerp(coverClosedLocalRotation, coverOpenLocalRotation, coverOpenAmount);
                return;
            }

            Vector3 axis = coverHingeAxis.sqrMagnitude > 0.0001f ? coverHingeAxis.normalized : Vector3.up;
            folderCover.localPosition = coverClosedLocalPosition;
            folderCover.localRotation = coverClosedLocalRotation;
            folderCover.RotateAround(folderCoverHinge.position, folderCoverHinge.TransformDirection(axis), openCoverHingeAngle * coverOpenAmount);
        }

        private void SelectPage(int index)
        {
            if (pages == null || pages.Length == 0)
            {
                pageIndex = 0;
            }
            else
            {
                int pageCount = GetPageCount();
                pageIndex = (index % pageCount + pageCount) % pageCount;
            }

            RefreshPage();
        }

        private void RefreshPage()
        {
            int configuredPageCount = pages != null ? pages.Length : 0;
            bool dynamicEvidencePage = IsEvidenceSummaryPage(pageIndex);
            CaseFilePage page = !dynamicEvidencePage && configuredPageCount > 0 ? pages[Mathf.Clamp(pageIndex, 0, configuredPageCount - 1)] : null;
            if (titleText != null)
            {
                titleText.text = dynamicEvidencePage ? "EVIDENCIAS REGISTRADAS" : page != null ? page.title : "EXPEDIENTE";
            }

            if (bodyText != null)
            {
                bodyText.text = dynamicEvidencePage ? BuildEvidenceSummary() : page != null ? page.body : "No hay paginas configuradas.";
            }

            if (counterText != null)
            {
                int pageCount = GetPageCount();
                counterText.text = pageCount > 0 ? $"{pageIndex + 1:00}/{pageCount:00}" : "00/00";
            }

            bool showPortrait = !dynamicEvidencePage && page != null && page.showPortrait;
            if (bodyText != null)
            {
                SetRect(
                    bodyText.rectTransform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(58f, 190f),
                    showPortrait ? new Vector2(-286f, -150f) : new Vector2(-58f, -150f));
            }

            if (portraitDividerImage != null)
            {
                portraitDividerImage.gameObject.SetActive(showPortrait);
            }

            if (portraitImage != null)
            {
                if (portraitFrameImage != null)
                {
                    portraitFrameImage.gameObject.SetActive(showPortrait);
                }

                portraitImage.gameObject.SetActive(showPortrait);
                if (showPortrait)
                {
                    portraitImage.sprite = GeneratePortraitSprite(page.portraitName, page.portraitAccent);
                }
            }

            if (portraitLabelText != null)
            {
                portraitLabelText.gameObject.SetActive(showPortrait);
                portraitLabelText.text = showPortrait ? page.portraitName : string.Empty;
            }
        }

        private int GetPageCount()
        {
            int configuredCount = pages != null ? pages.Length : 0;
            return Mathf.Max(1, configuredCount + (HasRegisteredEvidence() ? 1 : 0));
        }

        private bool IsEvidenceSummaryPage(int index)
        {
            int configuredCount = pages != null ? pages.Length : 0;
            return HasRegisteredEvidence() && index >= configuredCount;
        }

        private static bool HasRegisteredEvidence()
        {
            return EvidenceInventory.ExistingInstance != null && EvidenceInventory.Instance.GetAllEvidence().Count > 0;
        }

        private static string BuildEvidenceSummary()
        {
            if (EvidenceInventory.ExistingInstance == null)
            {
                return "Todavia no hay evidencias registradas.";
            }

            var evidence = EvidenceInventory.Instance.GetAllEvidence();
            if (evidence.Count == 0)
            {
                return "Todavia no hay evidencias registradas.";
            }

            StringBuilder builder = new();
            builder.AppendLine("Elementos agregados al tablero de investigacion:");
            builder.AppendLine();
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceData data = evidence[i];
                if (data == null)
                {
                    continue;
                }

                builder.Append("- ");
                builder.Append(data.evidenceName);
                builder.Append(" [");
                builder.Append(data.category);
                builder.AppendLine("]");
                if (!string.IsNullOrWhiteSpace(data.description))
                {
                    builder.AppendLine(data.description);
                }

                if (i < evidence.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
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

            if (backdropImage != null)
            {
                backdropImage.gameObject.SetActive(visible);
            }
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
            if (rootGroup != null)
            {
                Canvas existingCanvas = rootGroup.GetComponentInParent<Canvas>();
                if (existingCanvas != null)
                {
                    Destroy(existingCanvas.gameObject);
                }
            }

            GameObject canvasObject = new("CaseFileCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(detachUiFromFile ? null : transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9100;
            ResetUiTransform(canvasObject.transform);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            backdropImage = CreateImage("Backdrop", canvasRect, new Color(0.02f, 0.018f, 0.014f, 0.74f));
            Stretch(backdropImage.rectTransform);

            Image panel = CreateImage("CasePaper", canvasRect, new Color(0.8f, 0.75f, 0.64f, 0.985f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -8f);
            panelRect.sizeDelta = new Vector2(760f, 900f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.19f, 0.14f, 0.1f, 0.46f);
            outline.effectDistance = new Vector2(3f, -3f);
            rootGroup = panel.gameObject.AddComponent<CanvasGroup>();

            Image headerRule = CreateImage("HeaderRule", panelRect, new Color(0.23f, 0.18f, 0.12f, 0.7f));
            SetRect(headerRule.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(48f, 780f), new Vector2(-48f, -116f));

            TMP_Text stamp = CreateText("Stamp", panelRect, "ARCHIVE NULL\nEXPEDIENTE PRELIMINAR", 15f, TextAlignmentOptions.TopRight);
            stamp.color = new Color(0.48f, 0.08f, 0.06f, 0.68f);
            SetRect(stamp.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(448f, 808f), new Vector2(-52f, -34f));

            titleText = CreateText("Title", panelRect, string.Empty, 30f, TextAlignmentOptions.TopLeft);
            titleText.color = new Color(0.18f, 0.13f, 0.09f, 1f);
            titleText.fontStyle = FontStyles.Bold;
            SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(52f, 792f), new Vector2(-330f, -34f));

            bodyText = CreateText("Body", panelRect, string.Empty, 23f, TextAlignmentOptions.TopLeft);
            bodyText.color = new Color(0.15f, 0.12f, 0.09f, 1f);
            SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(58f, 190f), new Vector2(-286f, -150f));

            portraitDividerImage = CreateImage("Divider", panelRect, new Color(0.25f, 0.18f, 0.12f, 0.34f));
            SetRect(portraitDividerImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0.5f), new Vector2(486f, 178f), new Vector2(-270f, -126f));

            portraitFrameImage = CreateImage("PhotoFrame", panelRect, new Color(0.91f, 0.86f, 0.74f, 1f));
            SetRect(portraitFrameImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(512f, 548f), new Vector2(-54f, -142f));
            Outline photoOutline = portraitFrameImage.gameObject.AddComponent<Outline>();
            photoOutline.effectColor = new Color(0.18f, 0.13f, 0.09f, 0.45f);
            photoOutline.effectDistance = new Vector2(2f, -2f);

            portraitImage = CreateImage("Portrait", portraitFrameImage.rectTransform, Color.white);
            Stretch(portraitImage.rectTransform);
            portraitImage.rectTransform.offsetMin = new Vector2(12f, 42f);
            portraitImage.rectTransform.offsetMax = new Vector2(-12f, -12f);

            portraitLabelText = CreateText("PortraitLabel", portraitFrameImage.rectTransform, string.Empty, 16f, TextAlignmentOptions.Center);
            portraitLabelText.color = new Color(0.12f, 0.09f, 0.07f, 1f);
            SetRect(portraitLabelText.rectTransform, Vector2.zero, Vector2.right, new Vector2(0.5f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 36f));

            counterText = CreateText("Counter", panelRect, "00/00", 18f, TextAlignmentOptions.Bottom);
            counterText.color = new Color(0.24f, 0.17f, 0.11f, 1f);
            SetRect(counterText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(52f, 42f), new Vector2(-560f, -812f));

            TMP_Text footer = CreateText("Footer", panelRect, "CLICK / DERECHA: SIGUIENTE  //  A / IZQUIERDA: ANTERIOR  //  ESC: CERRAR", 16f, TextAlignmentOptions.BottomRight);
            footer.color = new Color(0.24f, 0.17f, 0.11f, 0.74f);
            SetRect(footer.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(220f, 42f), new Vector2(-52f, -812f));

            promptText = CreateText("Prompt", canvasRect, string.Empty, 22f, TextAlignmentOptions.Center);
            promptText.color = new Color(0.78f, 0.96f, 0.92f, 1f);
            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, -110f);
            promptRect.sizeDelta = new Vector2(420f, 42f);
        }

        private void EnsureScreenSpaceUi()
        {
            if (rootGroup == null)
            {
                return;
            }

            Canvas canvas = rootGroup.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new("CaseFileCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(detachUiFromFile ? null : transform, false);
                canvas = canvasObject.GetComponent<Canvas>();
                rootGroup.transform.SetParent(canvasObject.transform, false);
            }
            else if (detachUiFromFile)
            {
                canvas.transform.SetParent(null, true);
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 9100);
            ResetUiTransform(canvas.transform);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            ResetUiTransform(rootGroup.transform);
        }

        private static void ResetUiTransform(Transform uiTransform)
        {
            if (uiTransform == null)
            {
                return;
            }

            uiTransform.localPosition = Vector3.zero;
            uiTransform.localRotation = Quaternion.identity;
            uiTransform.localScale = Vector3.one;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Sprite GeneratePortraitSprite(string personName, Color accent)
        {
            const int width = 180;
            const int height = 220;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            int seed = StableHash(personName);
            int hairStyle = Mathf.Abs(seed) % 5;
            int faceVariant = Mathf.Abs(seed / 7) % 4;
            int bodyVariant = Mathf.Abs(seed / 13) % 4;
            bool hasGlasses = Mathf.Abs(seed / 17) % 3 == 0;
            bool sharpJaw = Mathf.Abs(seed / 23) % 2 == 0;
            bool longHair = personName != null && personName.ToUpperInvariant().Contains("SOFIA");
            Color paper = new(0.68f, 0.63f, 0.54f, 1f);
            Color shadow = new(0.22f, 0.18f, 0.15f, 1f);
            Color skin = Color.Lerp(new Color(0.58f, 0.43f, 0.32f, 1f), new Color(0.82f, 0.66f, 0.49f, 1f), Mathf.Abs(seed % 100) / 100f);
            Color hair = Color.Lerp(new Color(0.08f, 0.06f, 0.05f, 1f), accent, 0.28f);
            Color jacket = Color.Lerp(new Color(0.12f, 0.12f, 0.11f, 1f), accent, 0.5f);
            Color shirt = Color.Lerp(new Color(0.74f, 0.69f, 0.58f, 1f), accent, 0.18f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float noise = ((x * 17 + y * 31 + seed) & 7) * 0.006f;
                    texture.SetPixel(x, y, paper + new Color(noise, noise, noise, 0f));
                }
            }

            int shoulderWidth = 56 + bodyVariant * 8;
            int shoulderHeight = 48 + bodyVariant * 5;
            FillEllipse(texture, width / 2, 52, shoulderWidth + 18, 44, shadow);
            FillEllipse(texture, width / 2, 73, shoulderWidth, shoulderHeight, jacket);
            FillEllipse(texture, width / 2, 80, 18 + bodyVariant * 2, 20, shirt);

            int faceWidth = 29 + faceVariant * 3;
            int faceHeight = sharpJaw ? 45 : 39;
            FillEllipse(texture, width / 2, 137, faceWidth, faceHeight, skin);
            if (sharpJaw)
            {
                DrawRect(texture, width / 2 - faceWidth + 6, 104, faceWidth * 2 - 12, 22, skin);
            }

            if (longHair)
            {
                FillEllipse(texture, width / 2, 139, faceWidth + 12, 52, hair);
                FillEllipse(texture, width / 2, 134, faceWidth, 40, skin);
                DrawRect(texture, width / 2 - faceWidth - 5, 106, 13, 46, hair);
                DrawRect(texture, width / 2 + faceWidth - 8, 106, 13, 46, hair);
            }
            else
            {
                switch (hairStyle)
                {
                    case 0:
                        FillEllipse(texture, width / 2, 166, faceWidth + 8, 18, hair);
                        DrawRect(texture, width / 2 - faceWidth - 2, 150, faceWidth * 2 + 4, 16, hair);
                        break;
                    case 1:
                        FillEllipse(texture, width / 2 - 6, 166, faceWidth + 6, 20, hair);
                        DrawRect(texture, width / 2 - faceWidth - 3, 143, 16, 22, hair);
                        break;
                    case 2:
                        FillEllipse(texture, width / 2, 162, faceWidth + 2, 13, hair);
                        break;
                    case 3:
                        FillEllipse(texture, width / 2, 168, faceWidth + 9, 21, hair);
                        DrawRect(texture, width / 2 + faceWidth - 9, 140, 12, 25, hair);
                        break;
                    default:
                        FillEllipse(texture, width / 2, 170, faceWidth + 5, 16, hair);
                        DrawRect(texture, width / 2 - 10, 158, 28, 12, hair);
                        break;
                }
            }

            int eyeY = 139 + faceVariant % 2;
            FillEllipse(texture, width / 2 - 15, eyeY, 4, 3, shadow);
            FillEllipse(texture, width / 2 + 15, eyeY, 4, 3, shadow);
            if (hasGlasses)
            {
                DrawRect(texture, width / 2 - 25, eyeY - 4, 18, 2, shadow);
                DrawRect(texture, width / 2 + 7, eyeY - 4, 18, 2, shadow);
                DrawRect(texture, width / 2 - 7, eyeY - 4, 14, 2, shadow);
            }

            FillEllipse(texture, width / 2, 128, 3, 8, new Color(0.46f, 0.3f, 0.24f, 1f));
            int mouthWidth = 9 + Mathf.Abs(seed / 29) % 7;
            FillEllipse(texture, width / 2, 116, mouthWidth, 3, new Color(0.38f, 0.18f, 0.15f, 1f));

            DrawRect(texture, 0, 0, width, 8, new Color(0.13f, 0.1f, 0.08f, 1f));
            DrawRect(texture, 0, height - 8, width, 8, new Color(0.13f, 0.1f, 0.08f, 1f));
            DrawRect(texture, 0, 0, 8, height, new Color(0.13f, 0.1f, 0.08f, 1f));
            DrawRect(texture, width - 8, 0, 8, height, new Color(0.13f, 0.1f, 0.08f, 1f));

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (string.IsNullOrEmpty(value))
                {
                    return hash;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash;
            }
        }

        private static void FillEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
        {
            int minX = Mathf.Max(0, centerX - radiusX);
            int maxX = Mathf.Min(texture.width - 1, centerX + radiusX);
            int minY = Mathf.Max(0, centerY - radiusY);
            int maxY = Mathf.Min(texture.height - 1, centerY + radiusY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x - centerX) / (float)Mathf.Max(1, radiusX);
                    float dy = (y - centerY) / (float)Mathf.Max(1, radiusY);
                    if (dx * dx + dy * dy <= 1f)
                    {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        private static void DrawRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
        {
            int endX = Mathf.Min(texture.width, startX + width);
            int endY = Mathf.Min(texture.height, startY + height);

            for (int y = Mathf.Max(0, startY); y < endY; y++)
            {
                for (int x = Mathf.Max(0, startX); x < endX; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
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
