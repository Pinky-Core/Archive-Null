using UnityEngine;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceTarget : MonoBehaviour
    {
        [SerializeField] private EvidenceData evidenceData;
        [SerializeField] private bool validEvidence = true;
        [SerializeField] private string invalidMessage = "No hay evidencia util en este objetivo.";

        public EvidenceData EvidenceData => evidenceData;
        public string InvalidMessage => invalidMessage;

        public bool CanRegister(out string message)
        {
            if (!validEvidence || evidenceData == null)
            {
                message = invalidMessage;
                return false;
            }

            if (EvidenceInventory.Instance.HasEvidence(evidenceData.evidenceId))
            {
                message = "Evidencia ya registrada.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
