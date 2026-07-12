using System.Collections.Generic;
using System;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldBoardConnectionManager : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float lineWidth = 0.018f;
        [SerializeField] private Vector3 lineOffset = new Vector3(0f, 0f, -0.012f);
        [SerializeField] private float deleteLineHitRadius = 0.045f;
        [SerializeField] private Color lineColor = new Color(0.8f, 0.08f, 0.08f, 0.95f);

        private readonly Dictionary<string, WorldEvidencePhoto> photosById = new Dictionary<string, WorldEvidencePhoto>();
        private readonly Dictionary<string, LineRenderer> linesByKey = new Dictionary<string, LineRenderer>();
        private WorldEvidencePhoto pendingPhoto;

        public event Action OnConnectionsChanged;

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
            EvidenceConnectionNarration.Show(a.EvidenceId, b.EvidenceId);
            OnConnectionsChanged?.Invoke();
            return true;
        }

        public bool HasConnection(string evidenceA, string evidenceB)
        {
            return BoardSessionState.Connections.Contains(BoardSessionState.GetConnectionKey(evidenceA, evidenceB));
        }

        public bool TryRemoveConnection(Ray ray, float maxDistance)
        {
            string closestKey = null;
            float closestRayDistance = maxDistance;
            float closestLineDistance = deleteLineHitRadius;
            foreach (KeyValuePair<string, LineRenderer> entry in linesByKey)
            {
                if (!TryGetLinePhotos(entry.Key, out WorldEvidencePhoto a, out WorldEvidencePhoto b))
                {
                    continue;
                }

                Vector3 start = a.transform.position + lineOffset;
                Vector3 end = b.transform.position + lineOffset;
                if (!TryGetRaySegmentDistance(ray, start, end, out float rayDistance, out float lineDistance) ||
                    rayDistance < 0f ||
                    rayDistance > maxDistance ||
                    lineDistance > closestLineDistance)
                {
                    continue;
                }

                closestKey = entry.Key;
                closestRayDistance = rayDistance;
                closestLineDistance = lineDistance;
            }

            if (string.IsNullOrWhiteSpace(closestKey))
            {
                return false;
            }

            RemoveConnection(closestKey);
            return closestRayDistance <= maxDistance;
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
                if (!TryGetLinePhotos(entry.Key, out WorldEvidencePhoto a, out WorldEvidencePhoto b) ||
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
            if (line.material == null || line.material.shader == null || !line.material.shader.isSupported)
            {
                Shader fallback = Shader.Find("Unlit/Color");
                if (fallback == null || !fallback.isSupported)
                {
                    fallback = Shader.Find("Sprites/Default");
                }

                line.material = fallback != null ? new Material(fallback) : new Material(Shader.Find("Hidden/Internal-Colored"));
            }

            if (line.material.HasProperty("_Color"))
            {
                line.material.color = lineColor;
            }

            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.startColor = lineColor;
            line.endColor = line.startColor;
            linesByKey[key] = line;
            UpdateVisuals();
        }

        private void RemoveConnection(string key)
        {
            if (!BoardSessionState.Connections.Remove(key))
            {
                return;
            }

            if (linesByKey.TryGetValue(key, out LineRenderer line))
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }

                linesByKey.Remove(key);
            }

            OnConnectionsChanged?.Invoke();
        }

        private bool TryGetLinePhotos(string key, out WorldEvidencePhoto a, out WorldEvidencePhoto b)
        {
            string[] parts = key.Split('|');
            if (parts.Length == 2 &&
                photosById.TryGetValue(parts[0], out a) &&
                photosById.TryGetValue(parts[1], out b))
            {
                return true;
            }

            a = null;
            b = null;
            return false;
        }

        private static bool TryGetRaySegmentDistance(
            Ray ray,
            Vector3 segmentStart,
            Vector3 segmentEnd,
            out float rayDistance,
            out float lineDistance)
        {
            Vector3 segment = segmentEnd - segmentStart;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= Mathf.Epsilon)
            {
                rayDistance = Vector3.Dot(segmentStart - ray.origin, ray.direction);
                Vector3 pointOnDegenerateRay = ray.GetPoint(Mathf.Max(0f, rayDistance));
                lineDistance = Vector3.Distance(pointOnDegenerateRay, segmentStart);
                return true;
            }

            Vector3 originToSegment = ray.origin - segmentStart;
            float raySegmentDot = Vector3.Dot(ray.direction, segment);
            float rayOriginDot = Vector3.Dot(ray.direction, originToSegment);
            float segmentOriginDot = Vector3.Dot(segment, originToSegment);
            float denominator = segmentLengthSqr - raySegmentDot * raySegmentDot;

            float segmentT;
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                segmentT = Mathf.Clamp01(-segmentOriginDot / segmentLengthSqr);
            }
            else
            {
                segmentT = Mathf.Clamp01((segmentOriginDot - raySegmentDot * rayOriginDot) / denominator);
            }

            Vector3 pointOnSegment = segmentStart + segment * segmentT;
            rayDistance = Vector3.Dot(pointOnSegment - ray.origin, ray.direction);
            Vector3 pointOnRay = ray.GetPoint(Mathf.Max(0f, rayDistance));
            lineDistance = Vector3.Distance(pointOnRay, pointOnSegment);
            return true;
        }
    }
}
