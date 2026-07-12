using UnityEngine;

namespace ArchiveNull.Evidence
{
    [CreateAssetMenu(menuName = "Archive Null/Evidence/Evidence Data", fileName = "EvidenceData")]
    public sealed class EvidenceData : ScriptableObject
    {
        [Header("Identity")]
        public string evidenceId;
        public string evidenceName;

        [Header("Content")]
        [TextArea(3, 8)] public string description;
        [TextArea(2, 5)] public string narrativeLine;
        [TextArea(2, 4)] public string hintText;
        public EvidenceCategory category = EvidenceCategory.Other;
        public Sprite photoSprite;
        public string sourceSceneName;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                evidenceId = name;
            }
        }
    }
}
