using System.Collections.Generic;
using ArchiveNull.Evidence;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldBoardZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = "Unclassified";
        [SerializeField] private Collider zoneCollider;

        private readonly HashSet<string> evidenceIds = new HashSet<string>();

        public string ZoneId => zoneId;
        public IReadOnlyCollection<string> EvidenceIds => evidenceIds;

        private void Awake()
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            return zoneCollider != null && zoneCollider.bounds.Contains(worldPoint);
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
            if (!string.IsNullOrWhiteSpace(evidenceId))
            {
                evidenceIds.Remove(evidenceId);
            }
        }
    }
}
