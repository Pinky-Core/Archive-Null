using System.Collections.Generic;
using ArchiveNull.Evidence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] private float photoScaleMultiplier = 1.6f;
        [SerializeField] private Vector3 photoRotationOffset = new Vector3(0f, 180f, 0f);

        [Header("Drag")]
        [SerializeField] private LayerMask photoRaycastLayers = ~0;
        [SerializeField] private float clickMaxDragDistance = 0.025f;
        [SerializeField] private float interactionDistance = 2.75f;

        [Header("Reticle")]
        [SerializeField] private bool createReticleIfMissing = true;
        [SerializeField] private Color reticleIdleColor = new Color(0.8f, 0.92f, 0.94f, 0.52f);
        [SerializeField] private Color reticlePhotoColor = new Color(0.15f, 1f, 0.92f, 1f);

        private readonly Dictionary<string, WorldEvidencePhoto> photosById = new Dictionary<string, WorldEvidencePhoto>();
        private WorldEvidencePhoto draggedPhoto;
        private Vector3 dragStartPosition;
        private bool draggedEnough;
        private CanvasGroup reticleGroup;
        private RectTransform reticleRoot;
        private Image reticleDot;
        private Image reticleFrame;
 
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

            if (createReticleIfMissing)
            {
                CreateReticle();
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
            if (EvidenceInventory.ExistingInstance != null)
            {
                EvidenceInventory.ExistingInstance.OnEvidenceRegistered -= HandleEvidenceRegistered;
                EvidenceInventory.ExistingInstance.OnInventoryChanged -= RefreshFromInventory;
            }
        }

        private void Update()
        {
            HandleMouse();
            UpdateReticle();
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
            photo.transform.localScale *= photoScaleMultiplier;
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
                    draggedPhoto.transform.position = GetSurfaceOffsetPoint(boardPoint);
                    ApplyPhotoRotation(draggedPhoto);
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
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, photoRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<WorldEvidencePhoto>();
        }

        private bool TryGetBoardPoint(out Vector3 point)
        {
            Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (boardCollider != null && boardCollider.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                point = hit.point;
                return true;
            }

            Plane plane = new Plane(boardSurface.forward, boardSurface.position);
            if (plane.Raycast(ray, out float distance))
            {
                if (distance > interactionDistance)
                {
                    point = default;
                    return false;
                }

                point = ray.GetPoint(distance);
                if (boardCollider != null)
                {
                    point = boardCollider.ClosestPoint(point);
                }

                return boardCollider == null || Vector3.Distance(point, boardCollider.ClosestPoint(point)) <= 0.001f;
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

            Vector3 position = boardSurface.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
            if (boardCollider != null)
            {
                position = boardCollider.ClosestPoint(position);
            }

            photo.transform.position = GetSurfaceOffsetPoint(position);
            ApplyPhotoRotation(photo);
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

        private Vector3 GetSurfaceOffsetPoint(Vector3 point)
        {
            return point + boardSurface.forward * surfaceOffset;
        }

        private void ApplyPhotoRotation(WorldEvidencePhoto photo)
        {
            if (photo != null)
            {
                photo.transform.rotation = boardSurface.rotation * Quaternion.Euler(photoRotationOffset);
            }
        }

        private void UpdateReticle()
        {
            if (reticleGroup == null || interactionCamera == null)
            {
                return;
            }

            Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            WorldEvidencePhoto photo = RaycastPhoto(ray);
            bool photoHover = photo != null;
            bool boardHover = boardCollider != null && boardCollider.Raycast(ray, out _, interactionDistance);
            SetReticleState(photoHover || boardHover, photoHover);
        }

        private WorldEvidencePhoto RaycastPhoto(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, photoRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<WorldEvidencePhoto>();
        }

        private void CreateReticle()
        {
            GameObject canvasObject = new GameObject("EvidenceBoardReticleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 710;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject root = new GameObject("BoardReticle", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvasObject.transform, false);
            reticleRoot = root.GetComponent<RectTransform>();
            reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
            reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRoot.pivot = new Vector2(0.5f, 0.5f);
            reticleRoot.sizeDelta = new Vector2(26f, 26f);
            reticleGroup = root.GetComponent<CanvasGroup>();
            reticleGroup.interactable = false;
            reticleGroup.blocksRaycasts = false;

            reticleFrame = CreateReticleImage("Frame", reticleRoot, new Vector2(16f, 16f));
            Outline outline = reticleFrame.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            reticleFrame.color = new Color(0f, 0f, 0f, 0f);

            reticleDot = CreateReticleImage("Dot", reticleRoot, new Vector2(4f, 4f));
            SetReticleState(false, false);
        }

        private static Image CreateReticleImage(string name, RectTransform parent, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return image;
        }

        private void SetReticleState(bool visible, bool photoHover)
        {
            if (reticleGroup == null)
            {
                return;
            }

            reticleGroup.alpha = visible ? 1f : 0f;
            Color color = photoHover ? reticlePhotoColor : reticleIdleColor;
            if (reticleDot != null)
            {
                reticleDot.color = color;
            }

            if (reticleFrame != null)
            {
                Outline outline = reticleFrame.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = color;
                }
            }

            if (reticleRoot != null)
            {
                reticleRoot.localScale = Vector3.one * (photoHover ? 1.25f : 1f);
            }
        }
    }
}
