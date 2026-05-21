using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceNotebookUI : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.Tab;
        [SerializeField] private Key previousKey = Key.Q;
        [SerializeField] private Key nextKey = Key.E;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Optional Custom UI")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image photoImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private TMP_InputField noteInput;

        private readonly List<EvidenceData> evidence = new List<EvidenceData>();
        private int currentIndex;
        private bool visible;
        private bool updatingNote;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private FirstPersonLook firstPersonLook;
        private FirstPersonMovement firstPersonMovement;
        private bool lookWasEnabled;
        private bool movementWasEnabled;

        public void SetToggleKey(Key key)
        {
            if (key != Key.None)
            {
                toggleKey = key;
            }
        }

        public static bool IsAnyNotebookOpen { get; private set; }

        private void Awake()
        {
            CachePlayerControls();

            if (rootGroup == null)
            {
                CreateRuntimeUi();
            }

            if (noteInput != null)
            {
                noteInput.onValueChanged.AddListener(HandleNoteChanged);
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            EvidenceInventory.Instance.OnInventoryChanged += RefreshEvidence;
            RefreshEvidence();
        }

        private void OnDisable()
        {
            if (EvidenceInventory.ExistingInstance != null)
            {
                EvidenceInventory.ExistingInstance.OnInventoryChanged -= RefreshEvidence;
            }

            if (noteInput != null)
            {
                noteInput.onValueChanged.RemoveListener(HandleNoteChanged);
            }

            if (visible)
            {
                RestorePlayerControls();
                RestoreCursor();
            }

            visible = false;
            IsAnyNotebookOpen = false;
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == mainMenuSceneName)
            {
                SetVisible(false);
                return;
            }

            if (Keyboard.current == null)
            {
                return;
            }

            if (WasPressed(toggleKey))
            {
                SetVisible(!visible);
                return;
            }

            if (!visible || evidence.Count == 0 || IsEditingNote())
            {
                return;
            }

            if (WasPressed(previousKey) || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                Select(currentIndex - 1);
            }
            else if (WasPressed(nextKey) || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                Select(currentIndex + 1);
            }
        }

        private void RefreshEvidence()
        {
            evidence.Clear();
            evidence.AddRange(EvidenceInventory.Instance.GetAllEvidence());
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, evidence.Count - 1));
            RefreshView();
        }

        private void Select(int index)
        {
            if (evidence.Count == 0)
            {
                currentIndex = 0;
            }
            else
            {
                currentIndex = (index % evidence.Count + evidence.Count) % evidence.Count;
            }

            RefreshView();
        }

        private void RefreshView()
        {
            EvidenceData data = evidence.Count > 0 ? evidence[currentIndex] : null;

            if (photoImage != null)
            {
                photoImage.sprite = data != null ? data.photoSprite : null;
                photoImage.enabled = photoImage.sprite != null;
            }

            if (titleText != null)
            {
                titleText.text = data != null ? data.evidenceName : "Sin fotos";
            }

            if (categoryText != null)
            {
                categoryText.text = data != null ? data.category.ToString() : "REGISTRO VACIO";
            }

            if (descriptionText != null)
            {
                descriptionText.text = data != null ? data.description : "Las fotos de evidencia tomadas en esta sesion apareceran aqui.";
            }

            if (counterText != null)
            {
                counterText.text = evidence.Count > 0 ? $"{currentIndex + 1:00}/{evidence.Count:00}" : "00/00";
            }

            if (noteInput != null)
            {
                updatingNote = true;
                noteInput.text = data != null ? EvidenceInventory.Instance.GetNote(data.evidenceId) : string.Empty;
                noteInput.interactable = data != null;
                updatingNote = false;
            }
        }

        private void HandleNoteChanged(string value)
        {
            if (updatingNote || evidence.Count == 0)
            {
                return;
            }

            EvidenceInventory.Instance.SetNote(evidence[currentIndex].evidenceId, value);
        }

        private void SetVisible(bool value)
        {
            if (visible == value && rootGroup != null)
            {
                rootGroup.alpha = value ? 1f : 0f;
                return;
            }

            visible = value;
            IsAnyNotebookOpen = value;
            if (rootGroup == null)
            {
                return;
            }

            rootGroup.alpha = value ? 1f : 0f;
            rootGroup.interactable = value;
            rootGroup.blocksRaycasts = value;

            if (value)
            {
                CachePlayerControls();
                DisablePlayerControls();
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshView();
            }
            else
            {
                RestorePlayerControls();
                RestoreCursor();
                if (EventSystem.current != null && noteInput != null && EventSystem.current.currentSelectedGameObject == noteInput.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        private void RestoreCursor()
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void CachePlayerControls()
        {
            if (firstPersonLook == null)
            {
                firstPersonLook = GetComponent<FirstPersonLook>();
                if (firstPersonLook == null)
                {
                    firstPersonLook = GetComponentInChildren<FirstPersonLook>(true);
                }
            }

            if (firstPersonMovement == null)
            {
                firstPersonMovement = GetComponentInParent<FirstPersonMovement>();
            }
        }

        private void DisablePlayerControls()
        {
            if (firstPersonLook != null)
            {
                lookWasEnabled = firstPersonLook.enabled;
                firstPersonLook.enabled = false;
            }

            if (firstPersonMovement != null)
            {
                movementWasEnabled = firstPersonMovement.enabled;
                firstPersonMovement.enabled = false;
                Rigidbody body = firstPersonMovement.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
                }
            }
        }

        private void RestorePlayerControls()
        {
            if (firstPersonLook != null)
            {
                firstPersonLook.enabled = lookWasEnabled;
            }

            if (firstPersonMovement != null)
            {
                firstPersonMovement.enabled = movementWasEnabled;
            }
        }

        private bool IsEditingNote()
        {
            return noteInput != null && noteInput.isFocused;
        }

        private static bool WasPressed(Key key)
        {
            return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new GameObject("EvidenceNotebookCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);
            rootGroup = canvasObject.AddComponent<CanvasGroup>();

            Image dim = CreateImage("Dim", canvasRect, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);

            Image panel = CreateImage("Notebook", canvasRect, new Color(0.055f, 0.064f, 0.062f, 0.98f));
            RectTransform panelRect = panel.rectTransform;
            Center(panelRect);
            panelRect.sizeDelta = new Vector2(1260f, 760f);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.76f, 0.69f, 0.32f);
            outline.effectDistance = new Vector2(2f, -2f);

            Image photoFrame = CreateImage("PhotoFrame", panelRect, new Color(0.015f, 0.018f, 0.018f, 1f));
            RectTransform photoRect = photoFrame.rectTransform;
            SetRect(photoRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(52f, 70f), new Vector2(730f, -70f));

            photoImage = CreateImage("CapturedPhoto", photoRect, Color.white);
            Stretch(photoImage.rectTransform, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            photoImage.preserveAspect = true;

            titleText = CreateText("Title", panelRect, "Sin fotos", 35f, TextAlignmentOptions.TopLeft);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(778f, -88f), new Vector2(-60f, -38f));
            categoryText = CreateText("Category", panelRect, "REGISTRO VACIO", 18f, TextAlignmentOptions.TopLeft);
            categoryText.color = new Color(0.48f, 0.86f, 0.77f, 1f);
            SetRect(categoryText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(780f, -132f), new Vector2(-60f, -102f));

            descriptionText = CreateText("Description", panelRect, string.Empty, 22f, TextAlignmentOptions.TopLeft);
            SetPointRect(descriptionText.rectTransform, new Vector2(0f, 0f), new Vector2(780f, 472f), new Vector2(420f, 132f));

            TMP_Text noteLabel = CreateText("NoteLabel", panelRect, "ANOTACIONES", 18f, TextAlignmentOptions.TopLeft);
            noteLabel.color = categoryText.color;
            SetPointRect(noteLabel.rectTransform, new Vector2(0f, 0f), new Vector2(780f, 382f), new Vector2(420f, 30f));
            noteInput = CreateNoteInput(panelRect);

            counterText = CreateText("Counter", panelRect, "00/00", 20f, TextAlignmentOptions.Bottom);
            SetRect(counterText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(280f, 24f), new Vector2(500f, 56f));

            CreateButton("Previous", panelRect, "<", new Vector2(96f, 42f), new Vector2(160f, 40f), () => Select(currentIndex - 1));
            CreateButton("Next", panelRect, ">", new Vector2(96f, 42f), new Vector2(620f, 40f), () => Select(currentIndex + 1));
            CreateButton("Close", panelRect, "X", new Vector2(52f, 42f), new Vector2(1180f, 696f), () => SetVisible(false));

            TMP_Text hint = CreateText("Hint", panelRect, "Q/E: FOTO ANTERIOR/SIGUIENTE  //  TAB: CERRAR", 16f, TextAlignmentOptions.BottomRight);
            hint.color = new Color(0.7f, 0.84f, 0.81f, 0.8f);
            SetRect(hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(780f, 24f), new Vector2(-60f, 58f));
        }

        private TMP_InputField CreateNoteInput(RectTransform parent)
        {
            Image fieldImage = CreateImage("NoteInput", parent, new Color(0.018f, 0.024f, 0.023f, 1f));
            RectTransform fieldRect = fieldImage.rectTransform;
            SetPointRect(fieldRect, new Vector2(0f, 0f), new Vector2(780f, 96f), new Vector2(420f, 270f));

            TMP_InputField input = fieldImage.gameObject.AddComponent<TMP_InputField>();
            TMP_Text text = CreateText("Text", fieldRect, string.Empty, 20f, TextAlignmentOptions.TopLeft);
            text.color = new Color(0.88f, 0.94f, 0.91f, 1f);
            Stretch(text.rectTransform, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            TMP_Text placeholder = CreateText("Placeholder", fieldRect, "Escriba observaciones del operador...", 20f, TextAlignmentOptions.TopLeft);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.58f, 0.66f, 0.63f, 0.8f);
            Stretch(placeholder.rectTransform, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textViewport = fieldRect;
            return input;
        }

        private void CreateButton(string name, RectTransform parent, string label, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            Image image = CreateImage(name, parent, new Color(0.11f, 0.16f, 0.15f, 1f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            TMP_Text text = CreateText("Label", rect, label, 24f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
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
            text.color = new Color(0.82f, 0.96f, 0.91f, 1f);
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

        private static void SetPointRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
