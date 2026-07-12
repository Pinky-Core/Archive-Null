using System.Collections.Generic;
using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Accusation
{
    [DisallowMultipleComponent]
    public sealed class AccusationPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Dropdown culpritDropdown;
        [SerializeField] private TMP_Dropdown methodDropdown;
        [SerializeField] private TMP_Dropdown motiveDropdown;
        [SerializeField] private TMP_Dropdown manipulationDropdown;
        [SerializeField] private TMP_Dropdown discardSuspectsDropdown;
        [SerializeField] private Button validateButton;
        [SerializeField] private SimpleMessageUI messageUI;

        [Header("Correct Answers")]
        [SerializeField] private string correctCulprit = "Víctor";
        [SerializeField] private string correctMethod = "Poisoned drink";
        [SerializeField] private string correctMotive = "Obra Salas";
        [SerializeField] private string correctManipulation = "Locked door from outside";
        [SerializeField] private string correctDiscard = "Nicolás and Sofía discarded";

        [Header("Required Evidence")]
        [SerializeField] private string[] requiredEvidenceIds;
        [SerializeField] private string[] requiredConclusionIds;

        private void Awake()
        {
            if (validateButton != null)
            {
                validateButton.onClick.AddListener(ValidateAccusation);
            }
        }

        public void ValidateAccusation()
        {
            List<string> missing = new List<string>();

            ValidateDropdown(culpritDropdown, correctCulprit, "culpable", missing);
            ValidateDropdown(methodDropdown, correctMethod, "metodo", missing);
            ValidateDropdown(motiveDropdown, correctMotive, "motivo", missing);
            ValidateDropdown(manipulationDropdown, correctManipulation, "manipulacion de escena", missing);
            ValidateDropdown(discardSuspectsDropdown, correctDiscard, "descartar sospechosos", missing);

            if (requiredEvidenceIds != null)
            {
                for (int i = 0; i < requiredEvidenceIds.Length; i++)
                {
                    string evidenceId = requiredEvidenceIds[i];
                    if (!string.IsNullOrWhiteSpace(evidenceId) && !EvidenceInventory.Instance.HasEvidence(evidenceId))
                    {
                        missing.Add("falta evidencia: " + evidenceId);
                    }
                }
            }

            if (requiredConclusionIds != null)
            {
                for (int i = 0; i < requiredConclusionIds.Length; i++)
                {
                    string conclusionId = requiredConclusionIds[i];
                    if (!string.IsNullOrWhiteSpace(conclusionId) && !BoardSessionState.UnlockedConclusions.Contains(conclusionId))
                    {
                        missing.Add("falta conclusion: " + conclusionId);
                    }
                }
            }

            if (missing.Count == 0)
            {
                ShowMessage("Caso resuelto. Acusacion correcta.");
            }
            else
            {
                ShowMessage("Acusacion incompleta: " + string.Join(", ", missing));
            }
        }

        private static void ValidateDropdown(TMP_Dropdown dropdown, string correctValue, string label, List<string> missing)
        {
            if (dropdown == null)
            {
                return;
            }

            string selected = dropdown.options.Count > dropdown.value ? dropdown.options[dropdown.value].text : string.Empty;
            if (!string.Equals(selected, correctValue, System.StringComparison.OrdinalIgnoreCase))
            {
                missing.Add("revisar " + label);
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
                Debug.Log("[Accusation] " + message);
            }
        }
    }
}
