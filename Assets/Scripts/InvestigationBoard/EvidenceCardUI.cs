using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class EvidenceCardUI : MonoBehaviour
    {
        [SerializeField] private Image photoImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button selectButton;

        private EvidenceData evidenceData;

        public EvidenceData EvidenceData => evidenceData;
        public string EvidenceId => evidenceData != null ? evidenceData.evidenceId : string.Empty;
        public event System.Action<EvidenceCardUI> OnSelected;

        private void Awake()
        {
            if (selectButton == null)
            {
                selectButton = GetComponent<Button>();
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(() => OnSelected?.Invoke(this));
            }
        }

        public void Bind(EvidenceData data)
        {
            evidenceData = data;
            if (photoImage != null)
            {
                photoImage.sprite = data != null ? data.photoSprite : null;
                photoImage.enabled = photoImage.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = data != null ? data.evidenceName : "Evidence";
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
