using System.Collections.Generic;
using ArchiveNull.Evidence;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class BoardZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = "Unclassified";
        [SerializeField] private RectTransform zoneRect;

        private readonly HashSet<string> evidenceIds = new HashSet<string>();

        public string ZoneId => zoneId;
        public IReadOnlyCollection<string> EvidenceIds => evidenceIds;

        private void Awake()
        {
            if (zoneRect == null)
            {
                zoneRect = transform as RectTransform;
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            return zoneRect != null && RectTransformUtility.RectangleContainsScreenPoint(zoneRect, screenPoint, eventCamera);
        }

        public void AddEvidence(EvidenceData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.evidenceId))
            {
                return;
            }

            evidenceIds.Add(data.evidenceId);
            BoardSessionState.EvidenceZones[data.evidenceId] = zoneId;
        }

        public void RemoveEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                return;
            }

            evidenceIds.Remove(evidenceId);
        }
    }
}
