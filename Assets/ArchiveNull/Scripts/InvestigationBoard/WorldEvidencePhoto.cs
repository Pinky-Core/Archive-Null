using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldEvidencePhoto : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer photoRenderer;
        [SerializeField] private Image photoImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Collider interactionCollider;
        [SerializeField] private bool normalizeFrameFacing = true;
        [SerializeField] private bool mirrorFrameX = true;
        [SerializeField] private Vector3 frameLocalEuler = Vector3.zero;

        private EvidenceData evidenceData;

        public EvidenceData EvidenceData => evidenceData;
        public string EvidenceId => evidenceData != null ? evidenceData.evidenceId : string.Empty;
        public Collider InteractionCollider => interactionCollider;

        private void Awake()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponentInChildren<Collider>();
            }

            if (photoRenderer == null)
            {
                photoRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (photoImage == null)
            {
                photoImage = GetComponentInChildren<Image>();
            }

            NormalizeFrameFacing();
        }

        public void Bind(EvidenceData data)
        {
            evidenceData = data;

            Sprite photo = data != null ? data.photoSprite : null;
            if (photo == null && data != null)
            {
                photo = CreatePlaceholderSprite(data.category);
            }
            if (photoImage != null)
            {
                photoImage.sprite = photo;
                photoImage.enabled = photo != null;
                photoImage.preserveAspect = true;
                RectTransform imageRect = photoImage.rectTransform;
                imageRect.localRotation = Quaternion.identity;
                Vector3 imageScale = imageRect.localScale;
                imageRect.localScale = new Vector3(Mathf.Abs(imageScale.x), Mathf.Abs(imageScale.y), Mathf.Abs(imageScale.z));
            }

            if (photoRenderer != null)
            {
                photoRenderer.sprite = photo;
                photoRenderer.enabled = photo != null && photoImage == null;
            }

            if (titleText != null)
            {
                titleText.text = data != null ? EvidenceTextLocalization.Name(data) : GameLocalization.Text("Evidencia", "Evidence");
            }

            if (categoryText != null)
            {
                categoryText.text = data != null ? data.category.ToString() : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data != null ? EvidenceTextLocalization.Description(data) : string.Empty;
            }
        }

        private static Sprite CreatePlaceholderSprite(EvidenceCategory category)
        {
            const int width = 320;
            const int height = 220;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            Color paper = new(0.76f, 0.72f, 0.61f, 1f);
            Color ink = category == EvidenceCategory.Document
                ? new Color(0.1f, 0.28f, 0.24f, 1f)
                : new Color(0.24f, 0.2f, 0.16f, 1f);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x < 7 || x >= width - 7 || y < 7 || y >= height - 7;
                    bool documentLine = y > 42 && y < 178 && y % 28 < 5 && x > 52 && x < 268;
                    bool phone = category == EvidenceCategory.Document && x > 112 && x < 208 && y > 30 && y < 190;
                    pixels[y * width + x] = border || documentLine || phone ? ink : paper;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "EvidencePlaceholder_" + category;
            return sprite;
        }

        private void NormalizeFrameFacing()
        {
            if (!normalizeFrameFacing)
            {
                return;
            }

            Canvas frameCanvas = GetComponentInChildren<Canvas>(true);
            if (frameCanvas == null)
            {
                return;
            }

            RectTransform frameRect = frameCanvas.transform as RectTransform;
            if (frameRect == null)
            {
                return;
            }

            Vector3 scale = frameRect.localScale;
            frameRect.localScale = new Vector3(
                mirrorFrameX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
            frameRect.localPosition = Vector3.zero;
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.localRotation = Quaternion.Euler(frameLocalEuler);
        }
    }
}
