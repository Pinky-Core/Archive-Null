using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceCameraController : MonoBehaviour
    {
        [Header("Capture")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float maxCaptureDistance = 4f;
        [SerializeField] private LayerMask captureLayers = ~0;

        [Header("Optional Custom UI")]
        [SerializeField] private SimpleMessageUI messageUI;
        [SerializeField] private GameObject cameraModeUI;

        [Header("Generated UI")]
        [SerializeField] private bool createUiIfMissing = true;
        [SerializeField] private string cameraModeLabel = "CAMARA DE EVIDENCIA";
        [SerializeField] private string captureHint = "CLICK: FOTO  //  F: CERRAR";
        [SerializeField] private Color hudColor = new Color(0.78f, 0.96f, 0.92f, 1f);

        public bool IsCameraModeActive { get; private set; }
        public static bool IsAnyCameraModeActive { get; private set; }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (createUiIfMissing && (messageUI == null || cameraModeUI == null))
            {
                CreateRuntimeUi();
            }

            SetCameraMode(false);
        }

        private void OnDisable()
        {
            if (IsCameraModeActive)
            {
                SetCameraMode(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && !global::InspectObject.IsAnyInspecting)
            {
                SetCameraMode(!IsCameraModeActive);
            }

            if (!IsCameraModeActive)
            {
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryCapture();
            }
        }

        private void SetCameraMode(bool active)
        {
            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            if (cameraModeUI != null)
            {
                cameraModeUI.SetActive(active);
            }
        }

        private void TryCapture()
        {
            if (playerCamera == null)
            {
                ShowMessage("Camara no disponible.");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxCaptureDistance, captureLayers, QueryTriggerInteraction.Ignore))
            {
                ShowMessage("No hay evidencia en foco.");
                return;
            }

            EvidenceTarget target = hit.collider.GetComponent<EvidenceTarget>();
            if (target == null)
            {
                target = hit.collider.GetComponentInParent<EvidenceTarget>();
            }

            if (target == null)
            {
                ShowMessage("Objetivo no registrable.");
                return;
            }

            if (!target.CanRegister(out string validationMessage))
            {
                ShowMessage(validationMessage);
                return;
            }

            bool registered = EvidenceInventory.Instance.RegisterEvidence(target.EvidenceData);
            ShowMessage(registered ? "Evidencia registrada: " + target.EvidenceData.evidenceName : "Evidencia ya registrada.");
        }

        private void ShowMessage(string message)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(message);
            }
            else
            {
                Debug.Log("[EvidenceCamera] " + message);
            }
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new GameObject("EvidenceCameraRuntimeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            if (cameraModeUI == null)
            {
                cameraModeUI = CreateCameraHud(canvasRect);
            }

            if (messageUI == null)
            {
                messageUI = CreateMessageUi(canvasRect);
            }
        }

        private GameObject CreateCameraHud(RectTransform parent)
        {
            GameObject root = CreateRectObject("CameraModeHUD", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image dim = CreateImage("CameraTint", rootRect, new Color(0.02f, 0.06f, 0.055f, 0.12f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            RectTransform frame = CreateRectObject("Viewfinder", rootRect).GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1040f, 640f);

            CreateCorner(frame, "Corner_TL", new Vector2(0f, 1f), new Vector2(1f, -1f));
            CreateCorner(frame, "Corner_TR", new Vector2(1f, 1f), new Vector2(-1f, -1f));
            CreateCorner(frame, "Corner_BL", new Vector2(0f, 0f), new Vector2(1f, 1f));
            CreateCorner(frame, "Corner_BR", new Vector2(1f, 0f), new Vector2(-1f, 1f));

            Image crossHorizontal = CreateImage("CrosshairHorizontal", frame, hudColor);
            crossHorizontal.rectTransform.sizeDelta = new Vector2(46f, 2f);
            Center(crossHorizontal.rectTransform);
            crossHorizontal.raycastTarget = false;

            Image crossVertical = CreateImage("CrosshairVertical", frame, hudColor);
            crossVertical.rectTransform.sizeDelta = new Vector2(2f, 46f);
            Center(crossVertical.rectTransform);
            crossVertical.raycastTarget = false;

            TMP_Text title = CreateText("ModeLabel", rootRect, cameraModeLabel, 24f, TextAlignmentOptions.TopLeft);
            title.color = hudColor;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(70f, -52f), new Vector2(-70f, -16f));

            TMP_Text hint = CreateText("CaptureHint", rootRect, captureHint, 20f, TextAlignmentOptions.BottomRight);
            hint.color = hudColor;
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(70f, 18f), new Vector2(-70f, 54f));

            return root;
        }

        private SimpleMessageUI CreateMessageUi(RectTransform parent)
        {
            GameObject panelObject = CreateRectObject("EvidenceMessage", parent);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 96f);
            panelRect.sizeDelta = new Vector2(760f, 72f);

            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.7f);
            panel.raycastTarget = false;

            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TMP_Text text = CreateText("MessageText", panelRect, string.Empty, 25f, TextAlignmentOptions.Center);
            text.color = hudColor;
            Stretch(text.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -8f));

            SimpleMessageUI simpleMessage = panelObject.AddComponent<SimpleMessageUI>();
            simpleMessage.Configure(text, group);
            return simpleMessage;
        }

        private void CreateCorner(RectTransform parent, string name, Vector2 anchor, Vector2 direction)
        {
            RectTransform corner = CreateRectObject(name, parent).GetComponent<RectTransform>();
            corner.anchorMin = anchor;
            corner.anchorMax = anchor;
            corner.pivot = anchor;
            corner.anchoredPosition = new Vector2(28f * direction.x, 28f * direction.y);
            corner.sizeDelta = new Vector2(84f, 84f);

            Image horizontal = CreateImage("Horizontal", corner, hudColor);
            horizontal.rectTransform.anchorMin = new Vector2(direction.x > 0f ? 0f : 1f, direction.y > 0f ? 0f : 1f);
            horizontal.rectTransform.anchorMax = horizontal.rectTransform.anchorMin;
            horizontal.rectTransform.pivot = horizontal.rectTransform.anchorMin;
            horizontal.rectTransform.sizeDelta = new Vector2(72f, 3f);
            horizontal.rectTransform.localScale = new Vector3(direction.x, 1f, 1f);
            horizontal.raycastTarget = false;

            Image vertical = CreateImage("Vertical", corner, hudColor);
            vertical.rectTransform.anchorMin = horizontal.rectTransform.anchorMin;
            vertical.rectTransform.anchorMax = vertical.rectTransform.anchorMin;
            vertical.rectTransform.pivot = vertical.rectTransform.anchorMin;
            vertical.rectTransform.sizeDelta = new Vector2(3f, 72f);
            vertical.rectTransform.localScale = new Vector3(1f, direction.y, 1f);
            vertical.raycastTarget = false;
        }

        private static GameObject CreateRectObject(string name, RectTransform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
