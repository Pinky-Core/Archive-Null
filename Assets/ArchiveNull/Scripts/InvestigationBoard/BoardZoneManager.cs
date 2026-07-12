using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class BoardZoneManager : MonoBehaviour
    {
        [SerializeField] private Canvas boardCanvas;
        [SerializeField] private BoardZone defaultZone;
        [SerializeField] private BoardZone[] zones;

        private readonly Dictionary<string, BoardZone> zoneById = new Dictionary<string, BoardZone>();

        private void Awake()
        {
            if (boardCanvas == null)
            {
                boardCanvas = GetComponentInParent<Canvas>();
            }

            if (zones == null || zones.Length == 0)
            {
                zones = GetComponentsInChildren<BoardZone>(true);
            }

            zoneById.Clear();
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && !zoneById.ContainsKey(zones[i].ZoneId))
                {
                    zoneById.Add(zones[i].ZoneId, zones[i]);
                }
            }
        }

        public void AssignCardToZone(EvidenceCardUI card, Vector2 screenPosition)
        {
            if (card == null || card.EvidenceData == null)
            {
                return;
            }

            BoardZone zone = GetZoneAt(screenPosition);
            if (zone == null)
            {
                zone = defaultZone;
            }

            string evidenceId = card.EvidenceId;
            foreach (BoardZone existingZone in zoneById.Values)
            {
                existingZone.RemoveEvidence(evidenceId);
            }

            if (zone != null)
            {
                zone.AddEvidence(card.EvidenceData);
            }
        }

        public BoardZone GetZone(string zoneId)
        {
            return !string.IsNullOrWhiteSpace(zoneId) && zoneById.TryGetValue(zoneId, out BoardZone zone) ? zone : null;
        }

        private BoardZone GetZoneAt(Vector2 screenPosition)
        {
            Camera eventCamera = boardCanvas != null && boardCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? boardCanvas.worldCamera : null;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsScreenPoint(screenPosition, eventCamera))
                {
                    return zones[i];
                }
            }

            return null;
        }
    }
}
