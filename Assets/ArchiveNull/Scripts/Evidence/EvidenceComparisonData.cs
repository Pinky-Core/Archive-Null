using UnityEngine;

namespace ArchiveNull.Evidence
{
    [CreateAssetMenu(menuName = "Archive Null/Evidence/Evidence Comparison", fileName = "EvidenceComparison")]
    public sealed class EvidenceComparisonData : ScriptableObject
    {
        public string evidenceAId;
        public string evidenceBId;
        public EvidenceData derivedEvidence;
        [TextArea(2, 6)] public string successMessage = "Comparacion util registrada.";

        public bool Matches(string a, string b)
        {
            return (a == evidenceAId && b == evidenceBId) || (a == evidenceBId && b == evidenceAId);
        }
    }
}
