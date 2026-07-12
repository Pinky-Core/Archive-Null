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
        [SerializeField] private WorldBoardConnectionManager connectionManager;

        [Header("Layout")]
        [SerializeField] private Vector2 firstPhotoLocalPosition = new Vector2(-0.32f, 0.24f);
        [SerializeField] private Vector2 photoSpacing = new Vector2(0.22f, -0.24f);
        [SerializeField] private int photosPerRow = 4;
        [SerializeField] private float surfaceOffset = 0.045f;
        [SerializeField] private Vector3 photoRotationOffset = Vector3.zero;

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
        private Vector3 dragSurfaceOffset;
        private bool draggedEnough;
        private CanvasGroup reticleGroup;
        private RectTransform reticleRoot;
        private Image reticleDot;
        private Image reticleFrame;
        private Transform runtimePhotoContainer;
        private int horizontalAxis;
        private int verticalAxis = 1;
        private int normalAxis = 2;

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

            DetectBoardAxes();

            if (photoContainer == null || photoContainer == photoPrefab?.transform)
            {
                photoContainer = GetRuntimePhotoContainer();
            }

            if (connectionManager == null)
            {
                connectionManager = GetComponentInChildren<WorldBoardConnectionManager>(true);
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

            // Keep spawned photos under the same unscaled parent as the authored template.
            // Parenting them to the board inherits its non-uniform scale and deforms the prefab.
            Transform spawnParent = photoPrefab.transform.parent;
            Quaternion templateLocalRotation = photoPrefab.transform.localRotation;
            Vector3 templateLocalScale = photoPrefab.transform.localScale;
            WorldEvidencePhoto photo = Instantiate(photoPrefab, spawnParent, false);
            photo.transform.localRotation = templateLocalRotation;
            photo.transform.localScale = templateLocalScale;
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
                Vector2 localPosition = GetInitialPhotoLocalPosition(row, column);
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

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                connectionManager?.TryRemoveConnection(ray, interactionDistance);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                draggedPhoto = RaycastPhoto();
                draggedEnough = false;
                if (draggedPhoto != null)
                {
                    dragStartPosition = draggedPhoto.transform.position;
                    if (TryGetBoardPoint(out Vector3 boardPoint))
                    {
                        dragSurfaceOffset = draggedPhoto.transform.position - GetSurfaceOffsetPoint(boardPoint);
                    }
                    else
                    {
                        dragSurfaceOffset = Vector3.zero;
                    }
                }
            }

            if (draggedPhoto != null && Mouse.current.leftButton.isPressed)
            {
                if (TryGetBoardPoint(out Vector3 boardPoint))
                {
                    SetPhotoWorldPoint(draggedPhoto, boardPoint + dragSurfaceOffset);
                    draggedEnough |= Vector3.Distance(dragStartPosition, draggedPhoto.transform.position) > clickMaxDragDistance;
                    SavePhotoPosition(draggedPhoto);
                }
            }

            if (draggedPhoto != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
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
            return RaycastPhoto(ray);
        }

        private bool TryGetBoardPoint(out Vector3 point)
        {
            Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (boardCollider != null && boardCollider.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                point = hit.point;
                return true;
            }

            Plane plane = new Plane(GetBoardNormal(), boardSurface.position);
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

        private void SetPhotoLocalPosition(WorldEvidencePhoto photo, Vector2 localPosition)
        {
            if (photo == null || boardSurface == null)
            {
                return;
            }

            Vector2 distributedPosition = ClampLocalBoardPosition(localPosition);
            Vector3 position = BoardLocalPositionToWorld(distributedPosition);
            if (boardCollider != null)
            {
                position = boardCollider.ClosestPoint(position);
            }

            SetPhotoWorldPoint(photo, position);
            SavePhotoPosition(photo);
        }

        private void SavePhotoPosition(WorldEvidencePhoto photo)
        {
            if (photo == null || boardSurface == null || string.IsNullOrWhiteSpace(photo.EvidenceId))
            {
                return;
            }

            BoardSessionState.WorldPhotoPositions[photo.EvidenceId] = WorldToBoardLocalPosition(photo.transform.position);
        }

        private Vector3 GetSurfaceOffsetPoint(Vector3 point)
        {
            return point + GetBoardNormal() * surfaceOffset;
        }

        private Transform GetRuntimePhotoContainer()
        {
            if (runtimePhotoContainer != null)
            {
                return runtimePhotoContainer;
            }

            Transform containerParent = boardSurface != null ? boardSurface : transform;
            Transform existing = containerParent.Find("SpawnedEvidencePhotos");
            if (existing != null)
            {
                runtimePhotoContainer = existing;
                return runtimePhotoContainer;
            }

            GameObject container = new GameObject("SpawnedEvidencePhotos");
            container.transform.SetParent(containerParent, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;
            runtimePhotoContainer = container.transform;
            return runtimePhotoContainer;
        }

        private void SetPhotoWorldPoint(WorldEvidencePhoto photo, Vector3 worldPoint)
        {
            if (photo == null)
            {
                return;
            }

            Vector2 clamped = ClampLocalBoardPosition(WorldToBoardLocalPosition(worldPoint));
            Vector3 clampedWorldPoint = BoardLocalPositionToWorld(clamped);
            Vector3 finalPoint = GetSurfaceOffsetPoint(clampedWorldPoint);
            photo.transform.position = finalPoint;
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
            WorldEvidencePhoto closestPhoto = null;
            float closestDistance = interactionDistance;
            foreach (WorldEvidencePhoto photo in photosById.Values)
            {
                if (photo == null || photo.InteractionCollider == null)
                {
                    continue;
                }

                if (photo.InteractionCollider.Raycast(ray, out RaycastHit hit, interactionDistance) && hit.distance <= closestDistance)
                {
                    closestPhoto = photo;
                    closestDistance = hit.distance;
                }
            }

            return closestPhoto;
        }

        private Vector2 ClampLocalBoardPosition(Vector2 localPosition)
        {
            if (!TryGetLocalBoardRect(out Rect boardRect))
            {
                return localPosition;
            }

            const float margin = 0.08f;
            return new Vector2(
                Mathf.Clamp(localPosition.x, boardRect.xMin + margin, boardRect.xMax - margin),
                Mathf.Clamp(localPosition.y, boardRect.yMin + margin, boardRect.yMax - margin));
        }

        private Vector2 GetInitialPhotoLocalPosition(int row, int column)
        {
            Vector2 spacing = new Vector2(Mathf.Abs(photoSpacing.x), -Mathf.Abs(photoSpacing.y));
            Vector2 configuredPosition = firstPhotoLocalPosition + new Vector2(spacing.x * column, spacing.y * row);
            return ClampLocalBoardPosition(configuredPosition);
        }

        private bool TryGetLocalBoardRect(out Rect rect)
        {
            rect = default;
            if (boardCollider == null || boardSurface == null)
            {
                return false;
            }

            Bounds bounds = boardCollider.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            Vector2 first = WorldToBoardLocalPosition(corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 local = WorldToBoardLocalPosition(corners[i]);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 0.001f && rect.height > 0.001f;
        }

        private Vector2 WorldToBoardLocalPosition(Vector3 worldPoint)
        {
            Vector3 local = boardSurface.InverseTransformPoint(worldPoint);
            return new Vector2(GetAxis(local, horizontalAxis), GetAxis(local, verticalAxis));
        }

        private Vector3 BoardLocalPositionToWorld(Vector2 localPosition)
        {
            Vector3 local = Vector3.zero;
            SetAxis(ref local, horizontalAxis, localPosition.x);
            SetAxis(ref local, verticalAxis, localPosition.y);
            return boardSurface.TransformPoint(local);
        }

        private Vector3 GetBoardNormal()
        {
            Vector3 normal = boardSurface.TransformDirection(GetUnitAxis(normalAxis)).normalized;
            if (interactionCamera != null &&
                Vector3.Dot(normal, interactionCamera.transform.position - boardSurface.position) < 0f)
            {
                normal = -normal;
            }

            return normal;
        }

        private Vector3 GetBoardVertical()
        {
            return boardSurface.TransformDirection(GetUnitAxis(verticalAxis)).normalized;
        }

        private void DetectBoardAxes()
        {
            if (boardCollider == null || boardSurface == null)
            {
                return;
            }

            Bounds bounds = boardCollider.bounds;
            Vector3 localMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new(
                            x == 0 ? bounds.min.x : bounds.max.x,
                            y == 0 ? bounds.min.y : bounds.max.y,
                            z == 0 ? bounds.min.z : bounds.max.z);
                        Vector3 local = boardSurface.InverseTransformPoint(corner);
                        localMin = Vector3.Min(localMin, local);
                        localMax = Vector3.Max(localMax, local);
                    }
                }
            }

            Vector3 extents = localMax - localMin;
            normalAxis = extents.x <= extents.y && extents.x <= extents.z ? 0 : extents.y <= extents.z ? 1 : 2;
            int axisA = normalAxis == 0 ? 1 : 0;
            int axisB = normalAxis == 2 ? 1 : 2;
            if (axisA == normalAxis)
            {
                axisA = 0;
            }

            Vector3 worldA = boardSurface.TransformDirection(GetUnitAxis(axisA)).normalized;
            Vector3 worldB = boardSurface.TransformDirection(GetUnitAxis(axisB)).normalized;
            verticalAxis = Mathf.Abs(Vector3.Dot(worldA, Vector3.up)) >= Mathf.Abs(Vector3.Dot(worldB, Vector3.up))
                ? axisA
                : axisB;
            horizontalAxis = verticalAxis == axisA ? axisB : axisA;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static void SetAxis(ref Vector3 value, int axis, float component)
        {
            if (axis == 0) value.x = component;
            else if (axis == 1) value.y = component;
            else value.z = component;
        }

        private static Vector3 GetUnitAxis(int axis)
        {
            return axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
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
