using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceHoverInfoUI : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private Camera playerCamera;
        [SerializeField] private float hoverDistance = 4f;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.Equals(scene.name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindObjectOfType<EvidenceHoverInfoUI>() != null)
            {
                return;
            }

            GameObject host = new GameObject("EvidenceHoverInfoUI");
            host.AddComponent<EvidenceHoverInfoUI>();
        }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (rootGroup == null)
            {
                CreateRuntimeUi();
            }

            SetVisible(false);
        }

        private void Update()
        {
            if (EvidenceNotebookUI.IsAnyNotebookOpen || global::Keypad.IsAnyOpen)
            {
                SetVisible(false);
                return;
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    SetVisible(false);
                    return;
                }
            }

            EvidenceTarget target = GetHoveredEvidence();
            EvidenceData data = target != null ? target.EvidenceData : null;
            if (data == null || string.IsNullOrWhiteSpace(data.evidenceId) || !EvidenceInventory.Instance.HasEvidence(data.evidenceId))
            {
                SetVisible(false);
                return;
            }

            if (titleText != null)
            {
                titleText.text = data.evidenceName;
            }

            if (categoryText != null)
            {
                categoryText.text = data.category.ToString().ToUpperInvariant();
            }

            if (descriptionText != null)
            {
                descriptionText.text = data.description;
            }

            SetVisible(true);
        }

        private EvidenceTarget GetHoveredEvidence()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int hitCount = Physics.RaycastNonAlloc(ray, hits, hoverDistance, ~0, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return null;
            }

            SortHitsByDistance(hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                EvidenceTarget target = hitCollider.GetComponent<EvidenceTarget>();
                if (target == null)
                {
                    target = hitCollider.GetComponentInParent<EvidenceTarget>();
                }

                if (target != null)
                {
                    return target;
                }

                if (!hitCollider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private void SortHitsByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = current;
            }
        }

        private void SetVisible(bool visible)
        {
            if (rootGroup == null)
            {
                return;
            }

            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new GameObject("EvidenceHoverInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            Image panel = CreateImage("InfoPanel", canvasRect, new Color(0.025f, 0.035f, 0.034f, 0.9f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-54f, 0f);
            panelRect.sizeDelta = new Vector2(430f, 220f);

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.76f, 0.69f, 0.34f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            rootGroup = panel.gameObject.AddComponent<CanvasGroup>();
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            titleText = CreateText("Title", panelRect, string.Empty, 24f, TextAlignmentOptions.TopLeft);
            SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(24f, 142f), new Vector2(-24f, -20f));

            categoryText = CreateText("Category", panelRect, string.Empty, 15f, TextAlignmentOptions.TopLeft);
            categoryText.color = new Color(0.48f, 0.86f, 0.77f, 1f);
            SetRect(categoryText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(24f, 112f), new Vector2(-24f, -56f));

            descriptionText = CreateText("Description", panelRect, string.Empty, 18f, TextAlignmentOptions.TopLeft);
            SetRect(descriptionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(24f, 24f), new Vector2(-24f, -92f));
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
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
    }
}
