using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldBoardConnectionManager : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float lineWidth = 0.018f;
        [SerializeField] private Vector3 lineOffset = new Vector3(0f, 0f, -0.012f);

        private readonly Dictionary<string, WorldEvidencePhoto> photosById = new Dictionary<string, WorldEvidencePhoto>();
        private readonly Dictionary<string, LineRenderer> linesByKey = new Dictionary<string, LineRenderer>();
        private WorldEvidencePhoto pendingPhoto;

        public void RegisterPhoto(WorldEvidencePhoto photo)
        {
            if (photo == null || string.IsNullOrWhiteSpace(photo.EvidenceId))
            {
                return;
            }

            photosById[photo.EvidenceId] = photo;
            RefreshConnections();
        }

        public void HandlePhotoClicked(WorldEvidencePhoto photo)
        {
            if (photo == null || string.IsNullOrWhiteSpace(photo.EvidenceId))
            {
                return;
            }

            if (pendingPhoto == null)
            {
                pendingPhoto = photo;
                return;
            }

            if (pendingPhoto == photo)
            {
                pendingPhoto = null;
                return;
            }

            TryCreateConnection(pendingPhoto, photo);
            pendingPhoto = null;
        }

        public bool TryCreateConnection(WorldEvidencePhoto a, WorldEvidencePhoto b)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }

            string key = BoardSessionState.GetConnectionKey(a.EvidenceId, b.EvidenceId);
            if (!BoardSessionState.Connections.Add(key))
            {
                return false;
            }

            CreateLine(key, a, b);
            return true;
        }

        public void RefreshConnections()
        {
            foreach (string key in BoardSessionState.Connections)
            {
                if (linesByKey.ContainsKey(key))
                {
                    continue;
                }

                string[] parts = key.Split('|');
                if (parts.Length == 2 &&
                    photosById.TryGetValue(parts[0], out WorldEvidencePhoto a) &&
                    photosById.TryGetValue(parts[1], out WorldEvidencePhoto b))
                {
                    CreateLine(key, a, b);
                }
            }

            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            foreach (KeyValuePair<string, LineRenderer> entry in linesByKey)
            {
                string[] parts = entry.Key.Split('|');
                if (parts.Length != 2 ||
                    !photosById.TryGetValue(parts[0], out WorldEvidencePhoto a) ||
                    !photosById.TryGetValue(parts[1], out WorldEvidencePhoto b) ||
                    entry.Value == null)
                {
                    continue;
                }

                entry.Value.SetPosition(0, a.transform.position + lineOffset);
                entry.Value.SetPosition(1, b.transform.position + lineOffset);
            }
        }

        private void CreateLine(string key, WorldEvidencePhoto a, WorldEvidencePhoto b)
        {
            GameObject lineObject = new GameObject("Connection_" + key);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 4;
            line.material = lineMaterial;
            if (line.material == null)
            {
                line.material = new Material(Shader.Find("Sprites/Default"));
            }

            line.startColor = new Color(0.75f, 0.95f, 0.88f, 0.9f);
            line.endColor = line.startColor;
            linesByKey[key] = line;
            UpdateVisuals();
        }
    }
}
