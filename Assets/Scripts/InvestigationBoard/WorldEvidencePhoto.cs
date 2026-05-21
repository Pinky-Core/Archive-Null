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
                titleText.text = data != null ? data.evidenceName : "Evidence";
            }

            if (categoryText != null)
            {
                categoryText.text = data != null ? data.category.ToString() : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data != null ? data.description : string.Empty;
            }
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
            frameRect.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            Vector3 localEuler = frameRect.localEulerAngles;
            frameRect.localRotation = Quaternion.Euler(localEuler.x, -Mathf.Abs(NormalizeAngle(localEuler.y)), 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
