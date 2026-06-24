using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class PhoneEvidenceReader : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] private string inventoryDisplayName = "TELEFONO";
        [SerializeField] private Sprite inventoryIcon;
        [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.22f, -0.25f, 0.48f);
        [SerializeField] private Vector3 heldLocalEuler = new Vector3(8f, -8f, 0f);
        [SerializeField] private Vector3 heldLocalScale = Vector3.one;

        [Header("References")]
        [SerializeField] private EvidenceTarget evidenceTarget;
        [SerializeField] private CanvasGroup screenGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image screenGlow;
        [SerializeField] private Light screenLight;
        [SerializeField] private Collider[] pickupColliders;

        [Header("Content")]
        [SerializeField] private string header = "TELEFONO";
        [TextArea(2, 5)]
        [SerializeField] private string[] messages =
        {
            "Ultimo mensaje enviado: \"No puedo seguir con esto.\"",
            "Llamada perdida: Sofia Roldan, 22:14.",
            "Bateria baja. Pantalla bloqueada parcialmente."
        };

        [Header("Screen")]
        [SerializeField] private float powerOnDuration = 0.42f;
        [SerializeField] private float screenLightIntensity = 1.1f;

        private Coroutine powerRoutine;
        private int page;
        private bool collected;
        private bool equipped;

        public string InventoryDisplayName => inventoryDisplayName;
        public Sprite InventoryIcon => inventoryIcon;
        public bool IsCollected => collected;
        public bool IsEquipped => equipped;
        public bool IsOpen => equipped;
        public static bool IsAnyOpen { get; private set; }

        private void Awake()
        {
            if (evidenceTarget == null)
            {
                evidenceTarget = GetComponent<EvidenceTarget>();
            }

            if (pickupColliders == null || pickupColliders.Length == 0)
            {
                pickupColliders = GetComponentsInChildren<Collider>(true);
            }

            FindPrefabUi();
            if (screenGroup == null || titleText == null || bodyText == null)
            {
                BuildFallbackUi();
            }

            Refresh();
            SetScreenVisible(false);
        }

        private void OnEnable()
        {
            if (equipped)
            {
                StartPowerOn();
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            if (!collected &&
                evidenceTarget != null &&
                evidenceTarget.EvidenceData != null &&
                EvidenceInventory.Instance.HasEvidence(evidenceTarget.EvidenceData.evidenceId))
            {
                Collect();
            }
        }

        private void OnDisable()
        {
            StopPowerRoutine();
            IsAnyOpen = false;
            SetScreenVisible(false);
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            EvidenceCameraController controller = FindObjectOfType<EvidenceCameraController>();
            if (controller == null)
            {
                return;
            }

            collected = true;
            if (!controller.RegisterCollectedPhone(this))
            {
                collected = false;
                return;
            }

            SetPickupColliders(false);
            if (evidenceTarget != null && evidenceTarget.CanRegister(out _))
            {
                EvidenceInventory.Instance.RegisterEvidence(evidenceTarget.CreateCapturedEvidence(null));
            }
        }

        // Kept for existing scene bindings.
        public void Open()
        {
            Collect();
        }

        public void AttachToInventory(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            transform.SetParent(anchor, false);
            transform.localPosition = heldLocalPosition;
            transform.localRotation = Quaternion.Euler(heldLocalEuler);
            transform.localScale = heldLocalScale;
            SetPickupColliders(false);
            SetScreenVisible(false);
            gameObject.SetActive(false);
        }

        public void SetEquippedState(bool value)
        {
            equipped = value;
            IsAnyOpen = value;
            page = Mathf.Clamp(page, 0, Mathf.Max(0, messages != null ? messages.Length - 1 : 0));
            Refresh();

            if (!value)
            {
                StopPowerRoutine();
                SetScreenVisible(false);
                return;
            }

            if (gameObject.activeInHierarchy)
            {
                StartPowerOn();
            }
        }

        public void HandleEquippedInput()
        {
            if (!equipped || messages == null || messages.Length == 0)
            {
                return;
            }

            bool next = false;
            bool previous = false;
            if (Keyboard.current != null)
            {
                next = Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                       Keyboard.current.downArrowKey.wasPressedThisFrame ||
                       Keyboard.current.dKey.wasPressedThisFrame ||
                       Keyboard.current.sKey.wasPressedThisFrame ||
                       Keyboard.current.enterKey.wasPressedThisFrame;
                previous = Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                           Keyboard.current.upArrowKey.wasPressedThisFrame ||
                           Keyboard.current.aKey.wasPressedThisFrame ||
                           Keyboard.current.wKey.wasPressedThisFrame;
            }

            if (Mouse.current != null)
            {
                next |= Mouse.current.leftButton.wasPressedThisFrame;
                float scroll = Mouse.current.scroll.ReadValue().y;
                next |= scroll < -0.01f;
                previous |= scroll > 0.01f || Mouse.current.rightButton.wasPressedThisFrame;
            }

            if (next)
            {
                SelectPage(page + 1);
            }
            else if (previous)
            {
                SelectPage(page - 1);
            }
        }

        private void FindPrefabUi()
        {
            if (screenGroup == null)
            {
                screenGroup = GetComponentInChildren<CanvasGroup>(true);
            }

            if (titleText != null && bodyText != null)
            {
                return;
            }

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (titleText == null && lowerName.Contains("title"))
                {
                    titleText = texts[i];
                }
                else if (bodyText == null && (lowerName.Contains("body") || lowerName.Contains("message") || lowerName.Contains("content")))
                {
                    bodyText = texts[i];
                }
            }
        }

        private void SelectPage(int index)
        {
            int count = Mathf.Max(1, messages != null ? messages.Length : 0);
            page = (index % count + count) % count;
            Refresh();
        }

        private void Refresh()
        {
            if (titleText != null)
            {
                titleText.text = header;
            }

            if (bodyText != null)
            {
                string message = messages != null && messages.Length > 0
                    ? messages[Mathf.Clamp(page, 0, messages.Length - 1)]
                    : "Sin datos.";
                bodyText.text = message + $"\n\n{page + 1:00}/{Mathf.Max(1, messages != null ? messages.Length : 0):00}";
            }
        }

        private void StartPowerOn()
        {
            StopPowerRoutine();
            powerRoutine = StartCoroutine(PowerOnScreen());
        }

        private void StopPowerRoutine()
        {
            if (powerRoutine != null)
            {
                StopCoroutine(powerRoutine);
                powerRoutine = null;
            }
        }

        private IEnumerator PowerOnScreen()
        {
            if (screenGroup != null)
            {
                screenGroup.gameObject.SetActive(true);
            }

            if (screenLight != null)
            {
                screenLight.enabled = true;
                screenLight.intensity = 0f;
            }

            float timer = 0f;
            while (timer < powerOnDuration && equipped)
            {
                timer += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(timer / Mathf.Max(0.001f, powerOnDuration));
                float flicker = Random.value < 0.22f ? Random.Range(0.05f, 1f) : normalized;
                ApplyScreenAlpha(Mathf.Clamp01(normalized * flicker));
                yield return null;
            }

            if (equipped)
            {
                ApplyScreenAlpha(1f);
            }

            powerRoutine = null;
        }

        private void SetScreenVisible(bool visible)
        {
            if (screenGroup != null)
            {
                screenGroup.alpha = visible ? 1f : 0f;
                screenGroup.interactable = false;
                screenGroup.blocksRaycasts = false;
                screenGroup.gameObject.SetActive(visible);
            }

            if (screenLight != null)
            {
                screenLight.enabled = visible;
                screenLight.intensity = visible ? screenLightIntensity : 0f;
            }
        }

        private void ApplyScreenAlpha(float alpha)
        {
            if (screenGroup != null)
            {
                screenGroup.alpha = alpha;
            }

            if (screenGlow != null)
            {
                Color color = screenGlow.color;
                color.a = alpha * 0.65f;
                screenGlow.color = color;
            }

            if (screenLight != null)
            {
                screenLight.intensity = alpha * screenLightIntensity;
            }
        }

        private void SetPickupColliders(bool value)
        {
            if (pickupColliders == null)
            {
                return;
            }

            for (int i = 0; i < pickupColliders.Length; i++)
            {
                if (pickupColliders[i] != null)
                {
                    pickupColliders[i].enabled = value;
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new("PhoneScreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(420f, 720f);
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.001f;

            screenGroup = canvasObject.GetComponent<CanvasGroup>();
            Image background = CreateImage("Screen", canvasRect, new Color(0.018f, 0.022f, 0.023f, 0.98f));
            Stretch(background.rectTransform);

            titleText = CreateText("Title", canvasRect, 26f, FontStyles.Bold, TextAlignmentOptions.Top);
            SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(28f, 628f), new Vector2(-28f, -24f));

            bodyText = CreateText("Body", canvasRect, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(32f, 64f), new Vector2(-32f, -112f));
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.78f, 0.96f, 0.9f, 1f);
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
