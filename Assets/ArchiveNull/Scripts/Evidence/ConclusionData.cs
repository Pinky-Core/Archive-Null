using UnityEngine;

namespace ArchiveNull.Evidence
{
    [CreateAssetMenu(menuName = "Archive Null/Evidence/Conclusion Data", fileName = "ConclusionData")]
    public sealed class ConclusionData : ScriptableObject
    {
        public string conclusionId;
        public string conclusionName;
        [TextArea(3, 8)] public string description;
        public string[] requiredEvidenceIds;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(conclusionId))
            {
                conclusionId = name;
            }
        }
    }
}
