using System.Collections.Generic;
using ArchiveNull.Evidence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class WorldEvidenceBoardController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Collider boardCollider;
        [SerializeField] private Transform boardSurface;
        [SerializeField] private Transform photoContainer;
        [SerializeField] private WorldEvidencePhoto photoPrefab;
        [SerializeField] private WorldBoardZone[] zones;
        [SerializeField] private WorldBoardConnectionManager connectionManager;

        [Header("Layout")]
        [SerializeField] private Vector2 firstPhotoLocalPosition = new Vector2(-1.1f, 0.65f);
        [SerializeField] private Vector2 photoSpacing = new Vector2(0.34f, -0.24f);
        [SerializeField] private int photosPerRow = 5;
        [SerializeField] private float surfaceOffset = -0.018f;

        [Header("Drag")]
        [SerializeField] private LayerMask photoRaycastLayers = ~0;
        [SerializeField] private float clickMaxDragDistance = 0.025f;

        private readonly Dictionary<string, WorldEvidencePhoto> photosById = new Dictionary<string, WorldEvidencePhoto>();
        private WorldEvidencePhoto draggedPhoto;
        private Vector3 dragStartPosition;
        private bool draggedEnough;

        private void Awake()
        {
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            if (boardSurface == null)
            {
                boardSurface = transform;
            }

            if (photoContainer == null)
            {
                photoContainer = transform;
            }

            if (connectionManager == null)
            {
                connectionManager = GetComponentInChildren<WorldBoardConnectionManager>(true);
            }

            if (zones == null || zones.Length == 0)
            {
                zones = GetComponentsInChildren<WorldBoardZone>(true);
            }
        }

        private void OnEnable()
        {
            EvidenceInventory.Instance.OnEvidenceRegistered += HandleEvidenceRegistered;
            EvidenceInventory.Instance.OnInventoryChanged += RefreshFromInventory;
            RefreshFromInventory();
        }

        private void OnDisable()
        {
            EvidenceInventory.Instance.OnEvidenceRegistered -= HandleEvidenceRegistered;
            EvidenceInventory.Instance.OnInventoryChanged -= RefreshFromInventory;
        }

        private void Update()
        {
            HandleMouse();
            connectionManager?.UpdateVisuals();
        }

        private void RefreshFromInventory()
        {
            IReadOnlyList<EvidenceData> evidence = EvidenceInventory.Instance.GetAllEvidence();
            for (int i = 0; i < evidence.Count; i++)
            {
                SpawnPhotoIfNeeded(evidence[i], i);
            }
        }

        private void HandleEvidenceRegistered(EvidenceData data)
        {
            SpawnPhotoIfNeeded(data, photosById.Count);
        }

        private void SpawnPhotoIfNeeded(EvidenceData data, int index)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.evidenceId) || photosById.ContainsKey(data.evidenceId) || photoPrefab == null)
            {
                return;
            }

            WorldEvidencePhoto photo = Instantiate(photoPrefab, photoContainer);
            photo.Bind(data);
            photosById.Add(data.evidenceId, photo);

            if (BoardSessionState.WorldPhotoPositions.TryGetValue(data.evidenceId, out Vector2 savedPosition))
            {
                SetPhotoLocalPosition(photo, savedPosition);
            }
            else
            {
                int row = index / Mathf.Max(1, photosPerRow);
                int column = index % Mathf.Max(1, photosPerRow);
                Vector2 localPosition = firstPhotoLocalPosition + new Vector2(photoSpacing.x * column, photoSpacing.y * row);
                SetPhotoLocalPosition(photo, localPosition);
            }

            connectionManager?.RegisterPhoto(photo);
        }

        private void HandleMouse()
        {
            if (Mouse.current == null || interactionCamera == null)
            {
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                draggedPhoto = RaycastPhoto();
                draggedEnough = false;
                if (draggedPhoto != null)
                {
                    dragStartPosition = draggedPhoto.transform.position;
                }
            }

            if (draggedPhoto != null && Mouse.current.leftButton.isPressed)
            {
                if (TryGetBoardPoint(out Vector3 boardPoint))
                {
                    draggedPhoto.transform.position = boardPoint + boardSurface.forward * surfaceOffset;
                    draggedEnough |= Vector3.Distance(dragStartPosition, draggedPhoto.transform.position) > clickMaxDragDistance;
                    SavePhotoPosition(draggedPhoto);
                }
            }

            if (draggedPhoto != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                AssignZone(draggedPhoto);
                if (!draggedEnough)
                {
                    connectionManager?.HandlePhotoClicked(draggedPhoto);
                }

                draggedPhoto = null;
            }
        }

        private WorldEvidencePhoto RaycastPhoto()
        {
            Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, photoRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<WorldEvidencePhoto>();
        }

        private bool TryGetBoardPoint(out Vector3 point)
        {
            Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (boardCollider != null && boardCollider.Raycast(ray, out RaycastHit hit, 100f))
            {
                point = hit.point;
                return true;
            }

            Plane plane = new Plane(boardSurface.forward, boardSurface.position);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private void AssignZone(WorldEvidencePhoto photo)
        {
            if (photo == null || photo.EvidenceData == null || zones == null)
            {
                return;
            }

            for (int i = 0; i < zones.Length; i++)
            {
                zones[i]?.RemoveEvidence(photo.EvidenceId);
            }

            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].ContainsPoint(photo.transform.position))
                {
                    zones[i].AddEvidence(photo.EvidenceData);
                    return;
                }
            }
        }

        private void SetPhotoLocalPosition(WorldEvidencePhoto photo, Vector2 localPosition)
        {
            if (photo == null || boardSurface == null)
            {
                return;
            }

            photo.transform.position = boardSurface.TransformPoint(new Vector3(localPosition.x, localPosition.y, surfaceOffset));
            photo.transform.rotation = boardSurface.rotation;
            SavePhotoPosition(photo);
        }

        private void SavePhotoPosition(WorldEvidencePhoto photo)
        {
            if (photo == null || boardSurface == null || string.IsNullOrWhiteSpace(photo.EvidenceId))
            {
                return;
            }

            Vector3 local = boardSurface.InverseTransformPoint(photo.transform.position);
            BoardSessionState.WorldPhotoPositions[photo.EvidenceId] = new Vector2(local.x, local.y);
        }
    }
}
