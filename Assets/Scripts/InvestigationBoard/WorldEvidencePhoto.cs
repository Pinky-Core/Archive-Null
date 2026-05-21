using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldEvidencePhoto : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer photoRenderer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Collider interactionCollider;

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
        }

        public void Bind(EvidenceData data)
        {
            evidenceData = data;

            if (photoRenderer != null)
            {
                photoRenderer.sprite = data != null ? data.photoSprite : null;
                photoRenderer.enabled = photoRenderer.sprite != null;
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
    }
}
