using ArchiveNull.InvestigationBoard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceComparisonController : MonoBehaviour
    {
        [SerializeField] private EvidenceComparisonData[] comparisons;
        [SerializeField] private SimpleMessageUI messageUI;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text selectedAText;
        [SerializeField] private TMP_Text selectedBText;
        [SerializeField] private Button compareButton;

        private EvidenceCardUI selectedA;
        private EvidenceCardUI selectedB;

        private void Awake()
        {
            if (compareButton != null)
            {
                compareButton.onClick.AddListener(CompareSelected);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void SelectCard(EvidenceCardUI card)
        {
            if (card == null)
            {
                return;
            }

            if (selectedA == null || selectedA == card)
            {
                selectedA = card;
            }
            else
            {
                selectedB = card;
            }

            RefreshUi();
        }

        public void OpenPanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            RefreshUi();
        }

        public void ClosePanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void ClearSelection()
        {
            selectedA = null;
            selectedB = null;
            RefreshUi();
        }

        public void CompareSelected()
        {
            if (selectedA == null || selectedB == null)
            {
                ShowMessage("Seleccione dos evidencias para comparar.");
                return;
            }

            EvidenceComparisonData comparison = FindComparison(selectedA.EvidenceId, selectedB.EvidenceId);
            if (comparison == null || comparison.derivedEvidence == null)
            {
                ShowMessage("No hay una comparacion util entre estas evidencias.");
                return;
            }

            bool registered = EvidenceInventory.Instance.RegisterEvidence(comparison.derivedEvidence);
            ShowMessage(registered ? comparison.successMessage : "La evidencia derivada ya estaba registrada.");
        }

        private EvidenceComparisonData FindComparison(string a, string b)
        {
            if (comparisons == null)
            {
                return null;
            }

            for (int i = 0; i < comparisons.Length; i++)
            {
                if (comparisons[i] != null && comparisons[i].Matches(a, b))
                {
                    return comparisons[i];
                }
            }

            return null;
        }

        private void RefreshUi()
        {
            if (selectedAText != null)
            {
                selectedAText.text = selectedA != null && selectedA.EvidenceData != null ? selectedA.EvidenceData.evidenceName : "Evidencia A";
            }

            if (selectedBText != null)
            {
                selectedBText.text = selectedB != null && selectedB.EvidenceData != null ? selectedB.EvidenceData.evidenceName : "Evidencia B";
            }
        }

        private void ShowMessage(string message)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(message);
            }
            else
            {
                Debug.Log("[EvidenceComparison] " + message);
            }
        }
    }
}
