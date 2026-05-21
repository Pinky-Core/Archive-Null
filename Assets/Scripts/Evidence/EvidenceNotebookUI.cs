using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceNotebookUI : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Image photoImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text counterText;

        private readonly List<EvidenceData> evidence = new List<EvidenceData>();
        private int currentIndex;
        private bool visible;

        private void Awake()
        {
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

            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                SetVisible(!visible);
            }

            if (!visible || evidence.Count == 0)
            {
                return;
            }

            if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                Select(currentIndex - 1);
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
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
                titleText.text = data != null ? data.evidenceName : "Sin evidencias";
            }

            if (categoryText != null)
            {
                categoryText.text = data != null ? data.category.ToString() : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data != null ? data.description : "Todavia no registraste fotos ni evidencias.";
            }

            if (counterText != null)
            {
                counterText.text = evidence.Count > 0 ? $"{currentIndex + 1:00}/{evidence.Count:00}" : "00/00";
            }
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (rootGroup == null)
            {
                return;
            }

            rootGroup.alpha = value ? 1f : 0f;
            rootGroup.interactable = value;
            rootGroup.blocksRaycasts = value;
        }
    }
}
