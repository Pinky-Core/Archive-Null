using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceCameraController : MonoBehaviour
    {
        [Header("Capture")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float maxCaptureDistance = 4f;
        [SerializeField, Min(1f)] private float minimumUnzoomedCaptureDistance = 6f;
        [SerializeField] private float zoomedCaptureDistanceMultiplier = 2.25f;
        [SerializeField, Min(0f)] private float captureAssistRadius = 0.18f;
        [SerializeField, Range(0.02f, 0.35f)] private float captureScreenAssistRadius = 0.16f;
        [SerializeField] private int capturedPhotoWidth = 1024;
        [SerializeField] private bool createNotebookIfMissing = true;
        [SerializeField] private Key notebookToggleKey = Key.Tab;
        [SerializeField] private Key inventoryWheelKey = Key.G;

        [Header("Zoom")]
        [SerializeField] private float minZoomFov = 24f;
        [SerializeField] private float zoomStepPerScroll = 2.2f;
        [SerializeField] private float zoomLerpTime = 0.18f;
        [SerializeField] private float zoomIdleReturnSpeed = 3.2f;
        [SerializeField] private float zoomBlurMaxAlpha = 0.38f;
        [SerializeField] private float zoomBlurDecaySpeed = 5.4f;

        [Header("Optional Custom UI")]
        [SerializeField] private SimpleMessageUI messageUI;
        [SerializeField] private GameObject cameraModeUI;

        [Header("Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cameraOpenClip;
        [SerializeField] private AudioClip cameraCloseClip;
        [SerializeField] private AudioClip shutterClip;
        [SerializeField] private float cameraTransitionDuration = 0.22f;
        [SerializeField] private float captureFeedbackDuration = 0.18f;
        [SerializeField] private float captureCooldown = 0.12f;
        [SerializeField] private float hudOpenScale = 0.965f;
        [SerializeField] private float hudCaptureScale = 1.025f;

        [Header("Generated UI")]
        [SerializeField] private bool createUiIfMissing = true;
        [SerializeField] private string cameraModeLabel = "CAMARA DE EVIDENCIA";
        [SerializeField] private string captureHint = "CLICK: FOTO  //  F: ABRIR/CERRAR";
        [SerializeField] private string wheelHint = "SOLTA G O CLICK: EQUIPAR";
        [SerializeField] private Color hudColor = new Color(0.78f, 0.96f, 0.92f, 1f);
        [SerializeField] private Sprite handWheelIcon;
        [SerializeField] private Sprite cameraWheelIcon;
        [SerializeField] private Sprite uvWheelIcon;
        [SerializeField] private Sprite inventoryWheelIcon;

        [Header("Held Tools")]
        [SerializeField] private GameObject handToolObject;
        [SerializeField] private GameObject cameraToolObject;
        [SerializeField] private GameObject uvLightToolObject;
        [SerializeField] private Transform heldItemAnchor;
        [SerializeField] private float equipAnimationDuration = 0.22f;
        [SerializeField] private Vector3 hiddenToolLocalOffset = new Vector3(0f, -0.45f, -0.08f);
        [SerializeField] private Vector3 cameraFaceLocalPosition = new Vector3(0f, -0.08f, 0.24f);
        [SerializeField] private Vector3 cameraFaceLocalEuler = new Vector3(-8f, 0f, 0f);

        [Header("UV Light")]
        [SerializeField] private Key uvToggleKey = Key.F;
        [SerializeField] private Light uvSpotlight;
        [SerializeField] private float uvRevealDistance = 4f;
        [SerializeField] private float uvRevealRadius = 0.22f;
        [SerializeField] private LayerMask uvRevealLayers = ~0;

        public bool IsCameraModeActive { get; private set; }
        public static bool IsAnyCameraModeActive { get; private set; }
        public static bool IsAnyRadialMenuOpen { get; private set; }
        public static bool IsAnyUvLightActive { get; private set; }

        private CanvasGroup hudGroup;
        private RectTransform hudAnimatedRoot;
        private Image captureFlash;
        private Coroutine cameraModeRoutine;
        private Coroutine captureFeedbackRoutine;
        private float nextCaptureTime;
        private float defaultFov;
        private float targetFov;
        private float zoomVelocity;
        private float zoomBlurAlpha;
        private float lastZoomInputTime;
        private bool radialMenuOpen;
        private TMP_Text zoomText;
        private TMP_Text focusStateText;
        private TMP_Text toolStateText;
        private Image zoomBarFill;
        private TMP_Text recordingText;
        private Image transitionBlackOverlay;
        private Image zoomBlurOverlay;
        private CanvasGroup inventoryWheelGroup;
        private RectTransform inventoryWheelRoot;
        private Image wheelBackdrop;
        private readonly List<WheelSegmentVisual> wheelSegments = new List<WheelSegmentVisual>(4);
        private CanvasGroup itemSubInventoryGroup;
        private RectTransform itemSubInventoryRoot;
        private Image phoneInventorySlot;
        private Image phoneInventoryIcon;
        private TMP_Text phoneInventoryLabel;
        private ToolSlot equippedTool = ToolSlot.Hand;
        private ToolSlot hoveredTool = ToolSlot.Hand;
        private PhoneEvidenceReader collectedPhone;
        private bool phoneSlotHovered;
        private readonly RaycastHit[] captureHits = new RaycastHit[24];
        private readonly RaycastHit[] uvHits = new RaycastHit[24];
        private readonly Collider[] uvOverlapHits = new Collider[64];
        private readonly Dictionary<GameObject, ToolPose> toolPoses = new Dictionary<GameObject, ToolPose>();
        private Coroutine equipRoutine;
        private Coroutine cameraPoseRoutine;
        private bool uvLightOn;

        private readonly struct ToolPose
        {
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;

            public ToolPose(Transform transform)
            {
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }
        }

        private enum ToolSlot
        {
            Hand,
            Camera,
            UvLight,
            InventoryItem
        }

        private sealed class WheelSegmentVisual
        {
            public ToolSlot Tool;
            public Image Arc;
            public Image ActiveBorder;
            public Image IconImage;
            public float CenterAngle;
            public float HalfWidth;
        }

        private void Awake()
        {
            radialMenuOpen = false;
            IsAnyRadialMenuOpen = false;
            equippedTool = ToolSlot.Hand;
            hoveredTool = ToolSlot.Hand;
            collectedPhone = null;
            phoneSlotHovered = false;

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (playerCamera != null)
            {
                defaultFov = playerCamera.fieldOfView;
                targetFov = defaultFov;
            }

            if (createUiIfMissing && (messageUI == null || cameraModeUI == null))
            {
                CreateRuntimeUi();
            }

            EnsureAudio();
            EnsureHudAnimationReferences();
            EnsureNotebook();
            CacheToolPoses();
            ApplyEquippedToolImmediate();
            SetCameraModeImmediate(false);
        }

        private void OnDisable()
        {
            IsAnyRadialMenuOpen = false;
            IsAnyUvLightActive = false;
            SetUvLight(false);
            if (IsCameraModeActive)
            {
                SetCameraModeImmediate(false);
            }
        }

        private void Update()
        {
            if (EvidenceNotebookUI.IsAnyNotebookOpen || global::Keypad.IsAnyOpen)
            {
                if (radialMenuOpen)
                {
                    SetInventoryWheel(false);
                }

                IsAnyUvLightActive = false;
                SetUvLight(false);
                RestoreCameraFov();
                return;
            }

            bool wheelHold = IsHeld(inventoryWheelKey);
            if (wheelHold && !radialMenuOpen && !IsCameraModeActive && !global::InspectObject.IsAnyInspecting)
            {
                SetInventoryWheel(true);
            }

            if (radialMenuOpen)
            {
                IsAnyUvLightActive = false;
                UpdateInventoryWheelSelection();
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    TryEquipWheelSelection(true);
                }
                else if (!wheelHold)
                {
                    TryEquipWheelSelection(true);
                }

                return;
            }

            if (equippedTool == ToolSlot.InventoryItem)
            {
                collectedPhone?.HandleEquippedInput();
                RestoreCameraFov();
                return;
            }

            if (equippedTool == ToolSlot.UvLight && (GlobalInputBindings.WasPressed(GameInputAction.Camera) || WasPressed(uvToggleKey)))
            {
                SetUvLight(!uvLightOn);
            }

            IsAnyUvLightActive = equippedTool == ToolSlot.UvLight && uvLightOn;
            if (equippedTool == ToolSlot.UvLight)
            {
                SetCameraMode(false);
                if (uvLightOn)
                {
                    UpdateUvReveal();
                }
            }

            if (equippedTool == ToolSlot.Camera && GlobalInputBindings.WasPressed(GameInputAction.Camera) && !global::InspectObject.IsAnyInspecting)
            {
                SetCameraMode(!IsCameraModeActive);
            }

            if (!IsCameraModeActive)
            {
                if (equippedTool != ToolSlot.UvLight)
                {
                    IsAnyUvLightActive = false;
                    SetUvLight(false);
                }

                RestoreCameraFov();
                return;
            }

            if (global::InspectObject.IsAnyInspecting)
            {
                return;
            }

            UpdateCameraZoom();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryCapture();
            }
        }

        private void SetCameraMode(bool active)
        {
            if (active && equippedTool != ToolSlot.Camera)
            {
                return;
            }

            if (IsCameraModeActive == active)
            {
                return;
            }

            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            AnimateCameraHeldPose(active);
            if (cameraModeRoutine != null)
            {
                StopCoroutine(cameraModeRoutine);
            }

            cameraModeRoutine = StartCoroutine(AnimateCameraMode(active));
        }

        private void SetInventoryWheel(bool active)
        {
            radialMenuOpen = active;
            IsAnyRadialMenuOpen = active;
            if (inventoryWheelGroup != null)
            {
                inventoryWheelGroup.alpha = active ? 1f : 0f;
                inventoryWheelGroup.interactable = active;
                inventoryWheelGroup.blocksRaycasts = active;
            }
            if (wheelBackdrop != null)
            {
                wheelBackdrop.enabled = active;
            }

            if (active)
            {
                hoveredTool = equippedTool;
                phoneSlotHovered = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetCameraMode(false);
            }
            else if (!EvidenceNotebookUI.IsAnyNotebookOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            UpdateWheelVisuals();
            UpdateItemSubInventory();
        }

        private void UpdateInventoryWheelSelection()
        {
            if (Mouse.current == null || inventoryWheelRoot == null)
            {
                return;
            }

            phoneSlotHovered = collectedPhone != null &&
                               phoneInventorySlot != null &&
                               RectTransformUtility.RectangleContainsScreenPoint(phoneInventorySlot.rectTransform, Mouse.current.position.ReadValue());
            if (phoneSlotHovered)
            {
                hoveredTool = ToolSlot.InventoryItem;
                UpdateWheelVisuals();
                UpdateItemSubInventory();
                return;
            }

            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, inventoryWheelRoot.position);
            Vector2 delta = Mouse.current.position.ReadValue() - center;
            if (delta.sqrMagnitude < 2500f)
            {
                hoveredTool = equippedTool;
                UpdateWheelVisuals();
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            hoveredTool = GetToolFromAngle(angle);
            UpdateWheelVisuals();
            UpdateItemSubInventory();
        }

        private ToolSlot GetToolFromAngle(float angle)
        {
            if (wheelSegments.Count == 0)
            {
                return ToolSlot.Hand;
            }

            float wrapped = NormalizeAngle180(angle);
            float bestDelta = float.MaxValue;
            ToolSlot best = ToolSlot.Hand;
            for (int i = 0; i < wheelSegments.Count; i++)
            {
                WheelSegmentVisual segment = wheelSegments[i];
                float delta = Mathf.Abs(Mathf.DeltaAngle(wrapped, segment.CenterAngle));
                if (delta <= segment.HalfWidth && delta < bestDelta)
                {
                    bestDelta = delta;
                    best = segment.Tool;
                }
            }

            return best;
        }

        private void UpdateWheelVisuals()
        {
            if (inventoryWheelRoot == null)
            {
                return;
            }

            for (int i = 0; i < wheelSegments.Count; i++)
            {
                WheelSegmentVisual segment = wheelSegments[i];
                bool isHovered = segment.Tool == hoveredTool;
                if (segment.Arc != null)
                {
                    segment.Arc.color = isHovered ? new Color(0.92f, 0.97f, 0.96f, 0.25f) : new Color(0.04f, 0.08f, 0.08f, 0.82f);
                }
                if (segment.ActiveBorder != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8.5f);
                    float alpha = isHovered ? Mathf.Lerp(0.55f, 0.95f, pulse) : 0f;
                    segment.ActiveBorder.color = new Color(1f, 1f, 1f, alpha);
                }

                if (segment.IconImage != null)
                {
                    segment.IconImage.color = isHovered ? Color.white : new Color(0.82f, 0.92f, 0.9f, 0.9f);
                    segment.IconImage.rectTransform.sizeDelta = isHovered ? new Vector2(64f, 64f) : new Vector2(58f, 58f);
                }
            }
        }

        private void UpdateItemSubInventory()
        {
            if (itemSubInventoryGroup == null)
            {
                return;
            }

            bool visible = radialMenuOpen && hoveredTool == ToolSlot.InventoryItem;
            itemSubInventoryGroup.alpha = visible ? 1f : 0f;
            itemSubInventoryGroup.interactable = visible;
            itemSubInventoryGroup.blocksRaycasts = visible;

            bool available = collectedPhone != null;
            if (phoneInventorySlot != null)
            {
                phoneInventorySlot.gameObject.SetActive(available);
                phoneInventorySlot.color = phoneSlotHovered
                    ? new Color(0.84f, 0.96f, 0.92f, 0.98f)
                    : new Color(0.07f, 0.12f, 0.115f, 0.96f);
            }

            if (phoneInventoryLabel != null)
            {
                phoneInventoryLabel.text = available ? collectedPhone.InventoryDisplayName : "SIN OBJETOS";
                phoneInventoryLabel.color = phoneSlotHovered ? new Color(0.03f, 0.06f, 0.055f, 1f) : hudColor;
            }

            if (phoneInventoryIcon != null)
            {
                phoneInventoryIcon.gameObject.SetActive(available && collectedPhone.InventoryIcon != null);
                phoneInventoryIcon.sprite = available ? collectedPhone.InventoryIcon : null;
            }
        }

        private void TryEquipWheelSelection(bool closeWheel)
        {
            if (hoveredTool == ToolSlot.InventoryItem)
            {
                if (collectedPhone == null)
                {
                    if (closeWheel)
                    {
                        SetInventoryWheel(false);
                    }

                    ShowActionMessage("No hay objetos recogidos.");
                    return;
                }

                if (!phoneSlotHovered)
                {
                    if (!IsHeld(inventoryWheelKey) && closeWheel)
                    {
                        SetInventoryWheel(false);
                    }

                    return;
                }

                EquipHoveredTool(closeWheel);
                return;
            }

            EquipHoveredTool(closeWheel);
        }

        private void EquipHoveredTool(bool closeWheel)
        {
            if (equippedTool == hoveredTool)
            {
                if (closeWheel)
                {
                    SetInventoryWheel(false);
                }

                return;
            }

            bool unequippingPhone = equippedTool == ToolSlot.InventoryItem && hoveredTool != ToolSlot.InventoryItem;
            if (unequippingPhone)
            {
                collectedPhone?.SetEquippedState(false);
            }

            equippedTool = hoveredTool;
            if (equippedTool != ToolSlot.Camera)
            {
                SetCameraMode(false);
            }

            if (equippedTool != ToolSlot.UvLight)
            {
                SetUvLight(false);
            }

            IsAnyUvLightActive = equippedTool == ToolSlot.UvLight && uvLightOn;
            AnimateToolEquip(equippedTool);
            if (equippedTool == ToolSlot.InventoryItem)
            {
                collectedPhone?.SetEquippedState(true);
            }

            if (closeWheel)
            {
                SetInventoryWheel(false);
            }

            ShowActionMessage("Herramienta equipada: " + GetToolLabel(equippedTool));
            if (toolStateText != null)
            {
                toolStateText.text = "HERRAMIENTA: " + GetToolLabel(equippedTool);
            }
        }

        private string GetToolLabel(ToolSlot tool)
        {
            return tool switch
            {
                ToolSlot.Hand => "MANO",
                ToolSlot.Camera => "CAMARA",
                ToolSlot.UvLight => "LUZ UV",
                ToolSlot.InventoryItem => collectedPhone != null ? collectedPhone.InventoryDisplayName : "OBJETO",
                _ => "MANO"
            };
        }

        private Sprite GetToolIconSprite(ToolSlot tool)
        {
            return tool switch
            {
                ToolSlot.Hand => handWheelIcon,
                ToolSlot.Camera => cameraWheelIcon,
                ToolSlot.UvLight => uvWheelIcon,
                ToolSlot.InventoryItem => collectedPhone != null && collectedPhone.InventoryIcon != null
                    ? collectedPhone.InventoryIcon
                    : inventoryWheelIcon,
                _ => null
            };
        }

        private static string GetToolIcon(ToolSlot tool)
        {
            return tool switch
            {
                ToolSlot.Hand => "M",
                ToolSlot.Camera => "C",
                ToolSlot.UvLight => "UV",
                ToolSlot.InventoryItem => "OBJ",
                _ => "M"
            };
        }

        private void UpdateUvReveal()
        {
            if (playerCamera == null && uvSpotlight == null)
            {
                return;
            }

            if (uvSpotlight != null)
            {
                UpdateUvRevealFromSpotlight();
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int hitCount = Physics.SphereCastNonAlloc(ray, uvRevealRadius, uvHits, uvRevealDistance, ~0, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return;
            }

            SortHitsByDistance(uvHits, hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = uvHits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                UvRevealTarget revealTarget = hitCollider.GetComponent<UvRevealTarget>();
                if (revealTarget == null)
                {
                    revealTarget = hitCollider.GetComponentInParent<UvRevealTarget>();
                }

                if (revealTarget != null)
                {
                    revealTarget.ReceiveUvIllumination(1f);
                    continue;
                }

                if (!hitCollider.isTrigger)
                {
                    return;
                }
            }
        }

        private void UpdateUvRevealFromSpotlight()
        {
            Transform spotTransform = uvSpotlight.transform;
            float range = Mathf.Min(uvRevealDistance, uvSpotlight.range > 0f ? uvSpotlight.range : uvRevealDistance);
            float halfAngle = Mathf.Max(1f, uvSpotlight.spotAngle * 0.5f);
            int hitCount = Physics.OverlapSphereNonAlloc(spotTransform.position, range, uvOverlapHits, uvRevealLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = uvOverlapHits[i];
                if (candidate == null)
                {
                    continue;
                }

                UvRevealTarget revealTarget = candidate.GetComponent<UvRevealTarget>();
                if (revealTarget == null)
                {
                    revealTarget = candidate.GetComponentInParent<UvRevealTarget>();
                }

                if (revealTarget == null)
                {
                    continue;
                }

                Vector3 toTarget = revealTarget.RevealPosition - spotTransform.position;
                float distance = toTarget.magnitude;
                if (distance <= 0.001f || distance > range)
                {
                    continue;
                }

                Vector3 direction = toTarget / distance;
                float angle = Vector3.Angle(spotTransform.forward, direction);
                if (angle > halfAngle)
                {
                    continue;
                }

                if (Physics.Raycast(spotTransform.position, direction, out RaycastHit blocker, distance, uvRevealLayers, QueryTriggerInteraction.Ignore))
                {
                    UvRevealTarget blockedTarget = blocker.collider.GetComponent<UvRevealTarget>();
                    if (blockedTarget == null)
                    {
                        blockedTarget = blocker.collider.GetComponentInParent<UvRevealTarget>();
                    }

                    if (blockedTarget != revealTarget)
                    {
                        continue;
                    }
                }

                float angleStrength = 1f - Mathf.Clamp01(angle / halfAngle);
                float distanceStrength = 1f - Mathf.Clamp01(distance / range);
                revealTarget.ReceiveUvIllumination(Mathf.Clamp01(0.25f + angleStrength * 0.55f + distanceStrength * 0.2f));
            }
        }

        private void SetUvLight(bool active)
        {
            uvLightOn = active && equippedTool == ToolSlot.UvLight;
            IsAnyUvLightActive = uvLightOn;
            if (uvSpotlight != null)
            {
                uvSpotlight.enabled = uvLightOn;
            }
        }

        private void CacheToolPoses()
        {
            toolPoses.Clear();
            CacheToolPose(handToolObject);
            CacheToolPose(cameraToolObject);
            CacheToolPose(uvLightToolObject);
            if (collectedPhone != null)
            {
                CacheToolPose(collectedPhone.gameObject);
            }

            if (uvSpotlight == null && uvLightToolObject != null)
            {
                uvSpotlight = uvLightToolObject.GetComponentInChildren<Light>(true);
            }

            SetUvLight(false);
        }

        private void CacheToolPose(GameObject toolObject)
        {
            if (toolObject == null || toolPoses.ContainsKey(toolObject))
            {
                return;
            }

            toolPoses.Add(toolObject, new ToolPose(toolObject.transform));
        }

        public bool RegisterCollectedPhone(PhoneEvidenceReader phone)
        {
            if (phone == null)
            {
                return false;
            }

            if (collectedPhone != null && collectedPhone != phone)
            {
                return false;
            }

            collectedPhone = phone;
            Transform anchor = heldItemAnchor != null
                ? heldItemAnchor
                : playerCamera != null
                    ? playerCamera.transform
                    : transform;
            collectedPhone.AttachToInventory(anchor);
            toolPoses.Remove(collectedPhone.gameObject);
            CacheToolPose(collectedPhone.gameObject);
            ApplyToolImmediate(collectedPhone.gameObject, equippedTool == ToolSlot.InventoryItem);
            UpdateItemSubInventory();
            ShowActionMessage("Objeto recogido: " + collectedPhone.InventoryDisplayName);
            return true;
        }

        private void ApplyEquippedToolImmediate()
        {
            ApplyToolImmediate(handToolObject, equippedTool == ToolSlot.Hand);
            ApplyToolImmediate(cameraToolObject, equippedTool == ToolSlot.Camera);
            ApplyToolImmediate(uvLightToolObject, equippedTool == ToolSlot.UvLight);
            if (collectedPhone != null)
            {
                ApplyToolImmediate(collectedPhone.gameObject, equippedTool == ToolSlot.InventoryItem);
            }
        }

        private void ApplyToolImmediate(GameObject toolObject, bool visible)
        {
            if (toolObject == null)
            {
                return;
            }

            if (!toolPoses.TryGetValue(toolObject, out ToolPose pose))
            {
                pose = new ToolPose(toolObject.transform);
            }

            Transform toolTransform = toolObject.transform;
            toolTransform.localPosition = visible ? pose.LocalPosition : pose.LocalPosition + hiddenToolLocalOffset;
            toolTransform.localRotation = pose.LocalRotation;
            toolTransform.localScale = pose.LocalScale;
            toolObject.SetActive(visible);
        }

        private void AnimateToolEquip(ToolSlot tool)
        {
            if (equipRoutine != null)
            {
                StopCoroutine(equipRoutine);
            }

            equipRoutine = StartCoroutine(AnimateToolEquipRoutine(GetToolObject(tool)));
        }

        private IEnumerator AnimateToolEquipRoutine(GameObject nextTool)
        {
            GameObject previousHand = handToolObject != nextTool && handToolObject != null && handToolObject.activeSelf ? handToolObject : null;
            GameObject previousCamera = cameraToolObject != nextTool && cameraToolObject != null && cameraToolObject.activeSelf ? cameraToolObject : null;
            GameObject previousUv = uvLightToolObject != nextTool && uvLightToolObject != null && uvLightToolObject.activeSelf ? uvLightToolObject : null;
            GameObject previousInventoryItem = collectedPhone != null && collectedPhone.gameObject != nextTool && collectedPhone.gameObject.activeSelf
                ? collectedPhone.gameObject
                : null;

            yield return AnimateToolsDown(previousHand, previousCamera, previousUv, previousInventoryItem);

            HideTool(previousHand);
            HideTool(previousCamera);
            HideTool(previousUv);
            HideTool(previousInventoryItem);

            if (nextTool != null)
            {
                if (!toolPoses.TryGetValue(nextTool, out ToolPose pose))
                {
                    pose = new ToolPose(nextTool.transform);
                    toolPoses[nextTool] = pose;
                }

                nextTool.SetActive(true);
                Transform nextTransform = nextTool.transform;
                Vector3 startPosition = pose.LocalPosition + hiddenToolLocalOffset;
                Quaternion startRotation = pose.LocalRotation;
                nextTransform.localPosition = startPosition;
                nextTransform.localRotation = startRotation;
                nextTransform.localScale = pose.LocalScale;

                float timer = 0f;
                while (timer < equipAnimationDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    float t = Smooth01(timer / Mathf.Max(0.001f, equipAnimationDuration));
                    nextTransform.localPosition = Vector3.Lerp(startPosition, pose.LocalPosition, t);
                    nextTransform.localRotation = Quaternion.Slerp(startRotation, pose.LocalRotation, t);
                    yield return null;
                }

                nextTransform.localPosition = pose.LocalPosition;
                nextTransform.localRotation = pose.LocalRotation;
            }

            equipRoutine = null;
        }

        private IEnumerator AnimateToolsDown(params GameObject[] toolObjects)
        {
            float timer = 0f;
            while (timer < equipAnimationDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Smooth01(timer / Mathf.Max(0.001f, equipAnimationDuration));
                for (int i = 0; i < toolObjects.Length; i++)
                {
                    GameObject toolObject = toolObjects[i];
                    if (toolObject == null || !toolObject.activeSelf || !toolPoses.TryGetValue(toolObject, out ToolPose pose))
                    {
                        continue;
                    }

                    toolObject.transform.localPosition = Vector3.Lerp(pose.LocalPosition, pose.LocalPosition + hiddenToolLocalOffset, t);
                }

                yield return null;
            }
        }

        private void HideTool(GameObject toolObject)
        {
            if (toolObject == null)
            {
                return;
            }

            if (toolPoses.TryGetValue(toolObject, out ToolPose pose))
            {
                toolObject.transform.localPosition = pose.LocalPosition + hiddenToolLocalOffset;
            }

            toolObject.SetActive(false);
        }

        private GameObject GetToolObject(ToolSlot tool)
        {
            return tool switch
            {
                ToolSlot.Hand => handToolObject,
                ToolSlot.Camera => cameraToolObject,
                ToolSlot.UvLight => uvLightToolObject,
                ToolSlot.InventoryItem => collectedPhone != null ? collectedPhone.gameObject : null,
                _ => null
            };
        }

        private void AnimateCameraHeldPose(bool toFace)
        {
            if (cameraToolObject == null)
            {
                return;
            }

            if (cameraPoseRoutine != null)
            {
                StopCoroutine(cameraPoseRoutine);
            }

            cameraPoseRoutine = StartCoroutine(AnimateCameraHeldPoseRoutine(toFace));
        }

        private IEnumerator AnimateCameraHeldPoseRoutine(bool toFace)
        {
            if (!toolPoses.TryGetValue(cameraToolObject, out ToolPose pose))
            {
                yield break;
            }

            Transform toolTransform = cameraToolObject.transform;
            Vector3 fromPosition = toolTransform.localPosition;
            Quaternion fromRotation = toolTransform.localRotation;
            Vector3 toPosition = toFace ? cameraFaceLocalPosition : pose.LocalPosition;
            Quaternion toRotation = toFace ? Quaternion.Euler(cameraFaceLocalEuler) : pose.LocalRotation;

            float timer = 0f;
            while (timer < cameraTransitionDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Smooth01(timer / Mathf.Max(0.001f, cameraTransitionDuration));
                toolTransform.localPosition = Vector3.Lerp(fromPosition, toPosition, t);
                toolTransform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
                yield return null;
            }

            toolTransform.localPosition = toPosition;
            toolTransform.localRotation = toRotation;
            cameraPoseRoutine = null;
        }

        private void TryCapture()
        {
            if (Time.unscaledTime < nextCaptureTime)
            {
                return;
            }

            nextCaptureTime = Time.unscaledTime + captureCooldown;
            PlaySound(shutterClip);
            PlayCaptureFeedback();

            if (playerCamera == null)
            {
                ShowMessage(GameLocalization.Text("Cámara no disponible.", "Camera unavailable."));
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!TryGetCaptureTarget(ray, out EvidenceTarget target, out bool blocked))
            {
                ShowMessage(blocked
                    ? GameLocalization.Text("Objetivo no registrable.", "Target cannot be recorded.")
                    : GameLocalization.Text("No hay evidencia enfocada.", "No evidence in focus."));
                return;
            }

            if (!target.CanRegister(out string validationMessage))
            {
                ShowMessage(validationMessage);
                return;
            }

            Sprite capturedPhoto = CaptureCameraPhoto();
            EvidenceData capturedEvidence = target.CreateCapturedEvidence(capturedPhoto);
            bool registered = EvidenceInventory.Instance.RegisterEvidence(capturedEvidence);
            ShowMessage(registered ? BuildEvidenceSubtitle(capturedEvidence) : GameLocalization.Text("Evidencia ya registrada.", "Evidence already recorded."));
        }

        private static string BuildEvidenceSubtitle(EvidenceData data)
        {
            if (data == null)
            {
                return GameLocalization.Text("Evidencia registrada.", "Evidence recorded.");
            }

            string localizedName = EvidenceTextLocalization.Name(data);
            string localizedDescription = EvidenceTextLocalization.Description(data);
            if (string.IsNullOrWhiteSpace(localizedDescription))
            {
                return GameLocalization.Text("Evidencia registrada: ", "Evidence recorded: ") + localizedName;
            }

            return localizedName + "\n" + localizedDescription;
        }

        private bool TryGetCaptureTarget(Ray ray, out EvidenceTarget target, out bool blocked)
        {
            target = null;
            blocked = false;

            float distance = GetEffectiveCaptureDistance();
            int hitCount = captureAssistRadius > 0.001f
                ? Physics.SphereCastNonAlloc(ray, captureAssistRadius, captureHits, distance, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(ray, captureHits, distance, ~0, QueryTriggerInteraction.Collide);
            if (hitCount > 0)
            {
                SortHitsByDistance(captureHits, hitCount);
                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCollider = captureHits[i].collider;
                    if (hitCollider == null)
                    {
                        continue;
                    }

                    target = ResolveEvidenceTarget(hitCollider, captureHits[i].point);

                    if (target != null)
                    {
                        return true;
                    }

                    if (!hitCollider.isTrigger && !IsPlayerOrToolCollider(hitCollider))
                    {
                        blocked = true;
                        break;
                    }
                }
            }

            if (TryGetFramedEvidenceTarget(distance, out target))
            {
                blocked = false;
                return true;
            }

            return false;
        }

        private bool TryGetFramedEvidenceTarget(float maxDistance, out EvidenceTarget bestTarget)
        {
            bestTarget = null;
            if (playerCamera == null)
            {
                return false;
            }

            EvidenceTarget[] candidates = FindObjectsOfType<EvidenceTarget>(true);
            float bestScore = float.PositiveInfinity;
            Vector3 cameraPosition = playerCamera.transform.position;
            for (int i = 0; i < candidates.Length; i++)
            {
                EvidenceTarget candidate = candidates[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.CanRegister(out _))
                {
                    continue;
                }

                Vector3 aimPoint = GetEvidenceAimPoint(candidate);
                float worldDistance = Vector3.Distance(cameraPosition, aimPoint);
                if (worldDistance > maxDistance)
                {
                    continue;
                }

                Vector3 viewport = playerCamera.WorldToViewportPoint(aimPoint);
                if (viewport.z <= 0f)
                {
                    continue;
                }

                float screenDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
                if (screenDistance > captureScreenAssistRadius)
                {
                    continue;
                }

                float score = screenDistance * 10f + worldDistance / Mathf.Max(1f, maxDistance);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget != null;
        }

        private static Vector3 GetEvidenceAimPoint(EvidenceTarget target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                return bounds.center;
            }

            Collider collider = target.GetComponentInChildren<Collider>(true);
            return collider != null ? collider.bounds.center : target.transform.position;
        }

        private static EvidenceTarget ResolveEvidenceTarget(Collider hitCollider, Vector3 hitPoint)
        {
            if (hitCollider == null)
            {
                return null;
            }

            EvidenceTarget direct = hitCollider.GetComponent<EvidenceTarget>() ??
                                    hitCollider.GetComponentInParent<EvidenceTarget>() ??
                                    hitCollider.GetComponentInChildren<EvidenceTarget>(true);
            if (direct != null)
            {
                return direct;
            }

            Transform ancestor = hitCollider.transform.parent;
            for (int depth = 0; depth < 3 && ancestor != null; depth++, ancestor = ancestor.parent)
            {
                EvidenceTarget[] candidates = ancestor.GetComponentsInChildren<EvidenceTarget>(true);
                EvidenceTarget nearest = null;
                float nearestSqrDistance = 0.45f * 0.45f;
                for (int i = 0; i < candidates.Length; i++)
                {
                    EvidenceTarget candidate = candidates[i];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    float sqrDistance = (candidate.transform.position - hitPoint).sqrMagnitude;
                    if (sqrDistance <= nearestSqrDistance)
                    {
                        nearestSqrDistance = sqrDistance;
                        nearest = candidate;
                    }
                }

                if (nearest != null)
                {
                    return nearest;
                }
            }

            return null;
        }

        private bool IsPlayerOrToolCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            Transform candidateTransform = candidate.transform;
            if (playerCamera != null && candidateTransform.IsChildOf(playerCamera.transform.root))
            {
                return true;
            }

            return (cameraToolObject != null && candidateTransform.IsChildOf(cameraToolObject.transform)) ||
                   (uvLightToolObject != null && candidateTransform.IsChildOf(uvLightToolObject.transform)) ||
                   (handToolObject != null && candidateTransform.IsChildOf(handToolObject.transform));
        }

        private float GetEffectiveCaptureDistance()
        {
            if (playerCamera == null || defaultFov <= 0.001f || maxCaptureDistance <= 0f)
            {
                return Mathf.Max(maxCaptureDistance, minimumUnzoomedCaptureDistance);
            }

            float zoom01 = Mathf.InverseLerp(defaultFov, minZoomFov, playerCamera.fieldOfView);
            float multiplier = Mathf.Lerp(1f, Mathf.Max(1f, zoomedCaptureDistanceMultiplier), zoom01);
            return Mathf.Max(maxCaptureDistance, minimumUnzoomedCaptureDistance) * multiplier;
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = current;
            }
        }

        private void ShowMessage(string message)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(message);
            }
        }

        private void ShowActionMessage(string message)
        {
            if (PlayerAssistanceSettings.ActionFeedbackEnabled)
            {
                ShowMessage(message);
            }
        }

        private void EnsureNotebook()
        {
            EvidenceNotebookUI notebook = GetComponent<EvidenceNotebookUI>();
            if (notebook != null)
            {
                notebook.SetToggleKey(notebookToggleKey);
                return;
            }

            if (createNotebookIfMissing)
            {
                notebook = gameObject.AddComponent<EvidenceNotebookUI>();
                notebook.SetToggleKey(notebookToggleKey);
            }
        }

        private Sprite CaptureCameraPhoto()
        {
            if (playerCamera == null)
            {
                return null;
            }

            int side = Mathf.Clamp(capturedPhotoWidth, 256, 4096);

            RenderTexture previousTarget = playerCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(side, side, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGB24, false);

            playerCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            playerCamera.Render();
            texture.ReadPixels(new Rect(0f, 0f, side, side), 0, 0);
            texture.Apply();

            playerCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);

            texture.name = "CapturedEvidencePhoto";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "CapturedEvidencePhotoSprite";
            return sprite;
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new GameObject("EvidenceCameraRuntimeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            if (cameraModeUI == null)
            {
                cameraModeUI = CreateCameraHud(canvasRect);
            }

            CreateInventoryWheel(canvasRect);

            if (messageUI == null)
            {
                messageUI = CreateMessageUi(canvasRect);
            }
        }

        private GameObject CreateCameraHud(RectTransform parent)
        {
            GameObject root = CreateRectObject("CameraModeHUD", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            hudGroup = root.AddComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
            hudAnimatedRoot = rootRect;

            Image dim = CreateImage("CameraTint", rootRect, new Color(0.02f, 0.06f, 0.055f, 0.12f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            RectTransform frame = CreateRectObject("Viewfinder", rootRect).GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1040f, 640f);

            CreateCorner(frame, "Corner_TL", new Vector2(0f, 1f), new Vector2(1f, -1f));
            CreateCorner(frame, "Corner_TR", new Vector2(1f, 1f), new Vector2(-1f, -1f));
            CreateCorner(frame, "Corner_BL", new Vector2(0f, 0f), new Vector2(1f, 1f));
            CreateCorner(frame, "Corner_BR", new Vector2(1f, 0f), new Vector2(-1f, 1f));

            Image crossHorizontal = CreateImage("CrosshairHorizontal", frame, hudColor);
            crossHorizontal.rectTransform.sizeDelta = new Vector2(46f, 2f);
            Center(crossHorizontal.rectTransform);
            crossHorizontal.raycastTarget = false;

            Image crossVertical = CreateImage("CrosshairVertical", frame, hudColor);
            crossVertical.rectTransform.sizeDelta = new Vector2(2f, 46f);
            Center(crossVertical.rectTransform);
            crossVertical.raycastTarget = false;

            TMP_Text title = CreateText("ModeLabel", rootRect, cameraModeLabel, 24f, TextAlignmentOptions.TopLeft);
            title.color = hudColor;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(70f, -52f), new Vector2(-70f, -16f));

            TMP_Text hint = CreateText("CaptureHint", rootRect, captureHint, 20f, TextAlignmentOptions.BottomRight);
            hint.color = hudColor;
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(70f, 18f), new Vector2(-70f, 54f));

            zoomText = CreateText("ZoomValue", rootRect, "ZOOM 1.0X", 18f, TextAlignmentOptions.BottomLeft);
            zoomText.color = hudColor;
            SetRect(zoomText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(70f, 18f), new Vector2(-70f, 54f));

            toolStateText = CreateText("ToolState", rootRect, "HERRAMIENTA: " + GetToolLabel(equippedTool), 18f, TextAlignmentOptions.TopRight);
            toolStateText.color = hudColor;
            SetRect(toolStateText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(70f, -90f), new Vector2(-70f, -62f));

            recordingText = CreateText("RecText", rootRect, "REC", 22f, TextAlignmentOptions.TopRight);
            recordingText.color = new Color(1f, 0.24f, 0.24f, 0.95f);
            SetRect(recordingText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(70f, -52f), new Vector2(-70f, -22f));

            focusStateText = CreateText("FocusState", rootRect, "ENFOCADO", 17f, TextAlignmentOptions.Bottom);
            focusStateText.color = new Color(hudColor.r, hudColor.g, hudColor.b, 0.84f);
            SetRect(focusStateText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(650f, 18f), new Vector2(1260f, 56f));

            captureFlash = CreateImage("CaptureFlash", rootRect, Color.white);
            Stretch(captureFlash.rectTransform);
            captureFlash.raycastTarget = false;
            SetImageAlpha(captureFlash, 0f);

            zoomBlurOverlay = CreateImage("ZoomBlurOverlay", rootRect, new Color(0.08f, 0.16f, 0.16f, 0f));
            Stretch(zoomBlurOverlay.rectTransform);
            zoomBlurOverlay.raycastTarget = false;

            RectTransform zoomBarRoot = CreateRectObject("ZoomBarRoot", rootRect).GetComponent<RectTransform>();
            zoomBarRoot.anchorMin = new Vector2(0.5f, 0f);
            zoomBarRoot.anchorMax = new Vector2(0.5f, 0f);
            zoomBarRoot.pivot = new Vector2(0.5f, 0f);
            zoomBarRoot.anchoredPosition = new Vector2(0f, 76f);
            zoomBarRoot.sizeDelta = new Vector2(300f, 14f);
            Image zoomBarBg = CreateImage("ZoomBarBg", zoomBarRoot, new Color(0.05f, 0.11f, 0.1f, 0.9f));
            Stretch(zoomBarBg.rectTransform);
            RectTransform fillRect = CreateRectObject("ZoomBarFill", zoomBarRoot).GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(12f, 0f);
            zoomBarFill = fillRect.gameObject.AddComponent<Image>();
            zoomBarFill.color = new Color(0.78f, 0.96f, 0.92f, 0.95f);

            transitionBlackOverlay = CreateImage("TransitionBlack", rootRect, new Color(0f, 0f, 0f, 0f));
            Stretch(transitionBlackOverlay.rectTransform);
            transitionBlackOverlay.raycastTarget = false;
            transitionBlackOverlay.transform.SetAsLastSibling();
            captureFlash.transform.SetAsLastSibling();

            return root;
        }

        private void CreateInventoryWheel(RectTransform parent)
        {
            wheelSegments.Clear();

            RectTransform blurLayer = CreateRectObject("WheelBackdrop", parent).GetComponent<RectTransform>();
            Stretch(blurLayer);
            wheelBackdrop = blurLayer.gameObject.AddComponent<Image>();
            wheelBackdrop.color = new Color(0f, 0f, 0f, 0.45f);
            wheelBackdrop.raycastTarget = true;
            wheelBackdrop.enabled = false;

            GameObject root = CreateRectObject("InvestigationWheel", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(620f, 620f);
            inventoryWheelRoot = rootRect;
            inventoryWheelGroup = root.AddComponent<CanvasGroup>();
            inventoryWheelGroup.alpha = 0f;
            inventoryWheelGroup.interactable = false;
            inventoryWheelGroup.blocksRaycasts = false;

            Image outerRing = CreateImage("OuterRing", rootRect, new Color(0.03f, 0.06f, 0.06f, 0.88f));
            Stretch(outerRing.rectTransform);
            outerRing.sprite = CreateRingSprite(512, 0.48f, 0.2f);

            CreateWheelSegment(rootRect, ToolSlot.Hand, 90f, 45f);
            CreateWheelSegment(rootRect, ToolSlot.Camera, 180f, 45f);
            CreateWheelSegment(rootRect, ToolSlot.UvLight, 270f, 45f);
            CreateWheelSegment(rootRect, ToolSlot.InventoryItem, 0f, 45f);

            RectTransform centerDisk = CreateRectObject("CenterDisk", rootRect).GetComponent<RectTransform>();
            centerDisk.anchorMin = new Vector2(0.5f, 0.5f);
            centerDisk.anchorMax = new Vector2(0.5f, 0.5f);
            centerDisk.pivot = new Vector2(0.5f, 0.5f);
            centerDisk.sizeDelta = new Vector2(255f, 255f);
            Image centerBg = centerDisk.gameObject.AddComponent<Image>();
            centerBg.color = new Color(0.05f, 0.09f, 0.09f, 0.94f);
            centerBg.sprite = CreateCircleSprite(256);

            TMP_Text hint = CreateText("WheelHint", centerDisk, wheelHint, 17f, TextAlignmentOptions.Center);
            hint.color = new Color(0.78f, 0.96f, 0.92f, 0.88f);
            Stretch(hint.rectTransform, new Vector2(28f, 72f), new Vector2(-28f, -72f));

            CreateItemSubInventory(rootRect);
        }

        private void CreateItemSubInventory(RectTransform wheelRoot)
        {
            itemSubInventoryRoot = CreateRectObject("ItemSubInventory", wheelRoot).GetComponent<RectTransform>();
            itemSubInventoryRoot.anchorMin = new Vector2(1f, 0.5f);
            itemSubInventoryRoot.anchorMax = new Vector2(1f, 0.5f);
            itemSubInventoryRoot.pivot = new Vector2(0f, 0.5f);
            itemSubInventoryRoot.anchoredPosition = new Vector2(26f, 0f);
            itemSubInventoryRoot.sizeDelta = new Vector2(250f, 116f);

            Image background = itemSubInventoryRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.05f, 0.048f, 0.96f);
            Outline outline = itemSubInventoryRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.95f, 0.86f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);

            itemSubInventoryGroup = itemSubInventoryRoot.gameObject.AddComponent<CanvasGroup>();

            phoneInventorySlot = CreateImage("PhoneSlot", itemSubInventoryRoot, new Color(0.07f, 0.12f, 0.115f, 0.96f));
            RectTransform slotRect = phoneInventorySlot.rectTransform;
            slotRect.anchorMin = new Vector2(0f, 0.5f);
            slotRect.anchorMax = new Vector2(0f, 0.5f);
            slotRect.pivot = new Vector2(0f, 0.5f);
            slotRect.anchoredPosition = new Vector2(12f, 0f);
            slotRect.sizeDelta = new Vector2(226f, 88f);

            phoneInventoryLabel = CreateText("PhoneLabel", slotRect, "SIN OBJETOS", 17f, TextAlignmentOptions.Center);
            phoneInventoryLabel.color = hudColor;
            Stretch(phoneInventoryLabel.rectTransform, new Vector2(58f, 8f), new Vector2(-10f, -8f));

            phoneInventoryIcon = CreateImage("PhoneIcon", slotRect, Color.white);
            phoneInventoryIcon.preserveAspect = true;
            RectTransform iconRect = phoneInventoryIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(34f, 0f);
            iconRect.sizeDelta = new Vector2(42f, 58f);

            itemSubInventoryGroup.alpha = 0f;
            itemSubInventoryGroup.interactable = false;
            itemSubInventoryGroup.blocksRaycasts = false;
        }

        private void CreateWheelSegment(RectTransform parent, ToolSlot slot, float centerAngle, float halfWidth)
        {
            RectTransform segmentRoot = CreateRectObject(slot + "Segment", parent).GetComponent<RectTransform>();
            Stretch(segmentRoot);

            Image segmentArc = CreateImage("Arc", segmentRoot, new Color(0.04f, 0.08f, 0.08f, 0.82f));
            Stretch(segmentArc.rectTransform);
            segmentArc.sprite = CreateRingSegmentSprite(768, centerAngle, halfWidth, 0.48f, 0.2f);

            Image borderArc = CreateImage("ActiveBorder", segmentRoot, new Color(1f, 1f, 1f, 0f));
            Stretch(borderArc.rectTransform);
            borderArc.sprite = CreateRingSegmentSprite(768, centerAngle, halfWidth, 0.485f, 0.46f);

            RectTransform iconRect = CreateRectObject("IconRoot", segmentRoot).GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = DegreeToCircle(centerAngle, 195f);
            iconRect.sizeDelta = new Vector2(90f, 90f);
            Sprite iconSprite = GetToolIconSprite(slot);
            Image iconImage = null;
            if (iconSprite != null)
            {
                iconImage = CreateImage("IconImage", iconRect, new Color(0.82f, 0.92f, 0.9f, 0.9f));
                iconImage.sprite = iconSprite;
                iconImage.preserveAspect = true;
                iconImage.rectTransform.sizeDelta = new Vector2(58f, 58f);
                Center(iconImage.rectTransform);
            }
            else
            {
                iconImage = CreateImage("IconFallback", iconRect, new Color(0.82f, 0.92f, 0.9f, 0.9f));
                iconImage.sprite = CreateCircleSprite(84);
                iconImage.preserveAspect = true;
                iconImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
                Center(iconImage.rectTransform);
                TMP_Text fallbackText = CreateText("IconLabel", iconRect, GetToolIcon(slot), 19f, TextAlignmentOptions.Center);
                fallbackText.color = new Color(0.02f, 0.05f, 0.05f, 0.92f);
                Stretch(fallbackText.rectTransform);
            }

            wheelSegments.Add(new WheelSegmentVisual
            {
                Tool = slot,
                Arc = segmentArc,
                ActiveBorder = borderArc,
                IconImage = iconImage,
                CenterAngle = NormalizeAngle180(centerAngle),
                HalfWidth = halfWidth
            });
        }

        private SimpleMessageUI CreateMessageUi(RectTransform parent)
        {
            GameObject panelObject = CreateRectObject("EvidenceMessage", parent);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-34f, -142f);
            panelRect.sizeDelta = new Vector2(430f, 64f);

            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.7f);
            panel.raycastTarget = false;

            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TMP_Text text = CreateText("MessageText", panelRect, string.Empty, 20f, TextAlignmentOptions.MidlineLeft);
            text.color = hudColor;
            Stretch(text.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -8f));

            SimpleMessageUI simpleMessage = panelObject.AddComponent<SimpleMessageUI>();
            simpleMessage.Configure(text, group);
            return simpleMessage;
        }

        private void CreateCorner(RectTransform parent, string name, Vector2 anchor, Vector2 direction)
        {
            RectTransform corner = CreateRectObject(name, parent).GetComponent<RectTransform>();
            corner.anchorMin = anchor;
            corner.anchorMax = anchor;
            corner.pivot = new Vector2(0.5f, 0.5f);
            corner.anchoredPosition = new Vector2(28f * direction.x, 28f * direction.y);
            corner.sizeDelta = Vector2.zero;

            Image horizontal = CreateImage("Horizontal", corner, hudColor);
            Center(horizontal.rectTransform);
            horizontal.rectTransform.pivot = new Vector2(direction.x > 0f ? 0f : 1f, 0.5f);
            horizontal.rectTransform.sizeDelta = new Vector2(72f, 3f);
            horizontal.raycastTarget = false;

            Image vertical = CreateImage("Vertical", corner, hudColor);
            Center(vertical.rectTransform);
            vertical.rectTransform.pivot = new Vector2(0.5f, direction.y > 0f ? 0f : 1f);
            vertical.rectTransform.sizeDelta = new Vector2(3f, 72f);
            vertical.raycastTarget = false;
        }

        private void EnsureHudAnimationReferences()
        {
            if (cameraModeUI == null)
            {
                return;
            }

            if (hudGroup == null)
            {
                hudGroup = cameraModeUI.GetComponent<CanvasGroup>();
                if (hudGroup == null)
                {
                    hudGroup = cameraModeUI.AddComponent<CanvasGroup>();
                }
            }

            if (hudAnimatedRoot == null)
            {
                hudAnimatedRoot = cameraModeUI.transform as RectTransform;
            }
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            if (cameraOpenClip == null)
            {
                cameraOpenClip = CreateToneClip("EvidenceCameraOpen", 0.12f, 430f, 740f, 0.16f);
            }

            if (cameraCloseClip == null)
            {
                cameraCloseClip = CreateToneClip("EvidenceCameraClose", 0.11f, 620f, 280f, 0.13f);
            }

            if (shutterClip == null)
            {
                shutterClip = CreateShutterClip();
            }
        }

        private void SetCameraModeImmediate(bool active)
        {
            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            if (cameraModeUI != null)
            {
                cameraModeUI.SetActive(active);
            }

            if (!active)
            {
                RestoreCameraFov(true);
            }

            if (hudGroup != null)
            {
                hudGroup.alpha = active ? 1f : 0f;
            }

            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = Vector3.one;
            }

            if (captureFlash != null)
            {
                SetImageAlpha(captureFlash, 0f);
            }
        }

        private IEnumerator AnimateCameraMode(bool active)
        {
            EnsureHudAnimationReferences();
            if (cameraModeUI == null || hudGroup == null)
            {
                if (cameraModeUI != null)
                {
                    cameraModeUI.SetActive(active);
                }

                yield break;
            }

            if (active)
            {
                cameraModeUI.SetActive(true);
                PlaySound(cameraOpenClip);
                if (transitionBlackOverlay != null)
                {
                    SetImageAlpha(transitionBlackOverlay, 1f);
                }
            }
            else
            {
                PlaySound(cameraCloseClip);
            }

            float fromAlpha = hudGroup.alpha;
            float toAlpha = active ? 1f : 0f;
            Vector3 fromScale = hudAnimatedRoot != null ? hudAnimatedRoot.localScale : Vector3.one;
            Vector3 toScale = active ? Vector3.one : Vector3.one * hudOpenScale;
            if (active && hudAnimatedRoot != null && fromAlpha <= 0.001f)
            {
                fromScale = Vector3.one * hudOpenScale;
                hudAnimatedRoot.localScale = fromScale;
            }

            float timer = 0f;
            while (timer < cameraTransitionDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Smooth01(timer / Mathf.Max(0.001f, cameraTransitionDuration));
                hudGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                if (hudAnimatedRoot != null)
                {
                    hudAnimatedRoot.localScale = Vector3.Lerp(fromScale, toScale, t);
                }
                if (active && transitionBlackOverlay != null)
                {
                    SetImageAlpha(transitionBlackOverlay, 1f - t);
                }

                yield return null;
            }

            hudGroup.alpha = toAlpha;
            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = toScale;
            }

            if (!active)
            {
                if (transitionBlackOverlay != null)
                {
                    SetImageAlpha(transitionBlackOverlay, 0f);
                }
                cameraModeUI.SetActive(false);
                RestoreCameraFov(true);
                if (hudAnimatedRoot != null)
                {
                    hudAnimatedRoot.localScale = Vector3.one;
                }
            }

            cameraModeRoutine = null;
        }

        private void PlayCaptureFeedback()
        {
            if (captureFeedbackRoutine != null)
            {
                StopCoroutine(captureFeedbackRoutine);
            }

            captureFeedbackRoutine = StartCoroutine(CaptureFeedbackRoutine());
        }

        private IEnumerator CaptureFeedbackRoutine()
        {
            float timer = 0f;
            Vector3 startScale = hudAnimatedRoot != null ? hudAnimatedRoot.localScale : Vector3.one;
            while (timer < captureFeedbackDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, captureFeedbackDuration));
                float flash = t < 0.32f ? Mathf.Lerp(0f, 0.82f, t / 0.32f) : Mathf.Lerp(0.82f, 0f, (t - 0.32f) / 0.68f);
                if (captureFlash != null)
                {
                    SetImageAlpha(captureFlash, flash);
                }

                if (hudAnimatedRoot != null)
                {
                    float scale = Mathf.Lerp(hudCaptureScale, 1f, Smooth01(t));
                    hudAnimatedRoot.localScale = startScale * scale;
                }

                yield return null;
            }

            if (captureFlash != null)
            {
                SetImageAlpha(captureFlash, 0f);
            }

            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = Vector3.one;
            }

            captureFeedbackRoutine = null;
        }

        private void UpdateCameraZoom()
        {
            if (playerCamera == null || Mouse.current == null)
            {
                return;
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float normalizedStep = Mathf.Clamp(scroll / 120f, -1.5f, 1.5f);
                if (Mathf.Abs(normalizedStep) < 0.05f)
                {
                    normalizedStep = Mathf.Sign(scroll) * 0.1f;
                }

                targetFov = Mathf.Clamp(targetFov - normalizedStep * zoomStepPerScroll, minZoomFov, defaultFov);
                zoomBlurAlpha = zoomBlurMaxAlpha;
                lastZoomInputTime = Time.unscaledTime;
            }

            playerCamera.fieldOfView = Mathf.SmoothDamp(playerCamera.fieldOfView, targetFov, ref zoomVelocity, zoomLerpTime, Mathf.Infinity, Time.unscaledDeltaTime);
            UpdateZoomFocusFeedback();
            RefreshZoomText();
        }

        private void RestoreCameraFov(bool immediate = false)
        {
            if (playerCamera == null || defaultFov <= 0f)
            {
                return;
            }

            targetFov = defaultFov;
            zoomVelocity = 0f;
            playerCamera.fieldOfView = immediate ? defaultFov : Mathf.Lerp(playerCamera.fieldOfView, defaultFov, Time.unscaledDeltaTime * zoomIdleReturnSpeed);
            zoomBlurAlpha = Mathf.Max(0f, zoomBlurAlpha - zoomBlurDecaySpeed * Time.unscaledDeltaTime);
            UpdateZoomFocusFeedback();
            RefreshZoomText();
        }

        private void RefreshZoomText()
        {
            if (zoomText == null || defaultFov <= 0f || playerCamera == null)
            {
                return;
            }

            float zoom = Mathf.Clamp(defaultFov / Mathf.Max(1f, playerCamera.fieldOfView), 1f, 9.9f);
            zoomText.text = $"ZOOM {zoom:0.0}X";
            if (zoomBarFill != null)
            {
                float t = Mathf.InverseLerp(defaultFov, minZoomFov, playerCamera.fieldOfView);
                RectTransform fillRect = zoomBarFill.rectTransform;
                fillRect.sizeDelta = new Vector2(Mathf.Lerp(12f, 300f, t), fillRect.sizeDelta.y);
            }
        }

        private void UpdateZoomFocusFeedback()
        {
            if (zoomBlurOverlay != null)
            {
                zoomBlurAlpha = Mathf.Max(0f, zoomBlurAlpha - zoomBlurDecaySpeed * Time.unscaledDeltaTime);
                SetImageAlpha(zoomBlurOverlay, zoomBlurAlpha);
            }

            if (focusStateText != null)
            {
                bool focusing = Time.unscaledTime - lastZoomInputTime < 0.22f || zoomBlurAlpha > 0.03f;
                focusStateText.text = focusing ? "ENFOCANDO..." : "ENFOCADO";
                focusStateText.color = focusing
                    ? new Color(hudColor.r, hudColor.g, hudColor.b, 0.72f)
                    : new Color(hudColor.r, hudColor.g, hudColor.b, 0.92f);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private static GameObject CreateRectObject(string name, RectTransform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static AudioClip CreateToneClip(string name, float duration, float startFrequency, float endFrequency, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(phase * Mathf.PI * 2f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateShutterClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.16f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float clickEnvelope = Mathf.Exp(-t * 42f);
                float shutterEnvelope = Mathf.Clamp01(1f - Mathf.Abs(t - 0.36f) * 3.2f) * Mathf.Exp(-t * 4f);
                float click = Mathf.Sin(t * 4100f) * clickEnvelope * 0.34f;
                float shutter = (Mathf.PerlinNoise(i * 0.031f, 0.37f) * 2f - 1f) * shutterEnvelope * 0.18f;
                samples[i] = Mathf.Clamp(click + shutter, -0.8f, 0.8f);
            }

            AudioClip clip = AudioClip.Create("EvidenceCameraShutter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Sprite CreateRingSprite(int size, float outerRadiusRatio, float innerRadiusRatio)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * Mathf.Clamp(outerRadiusRatio, 0.05f, 0.5f);
            float inner = size * Mathf.Clamp(innerRadiusRatio, 0f, outerRadiusRatio - 0.02f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = dist <= outer && dist >= inner ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, dist <= radius ? 1f : 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRingSegmentSprite(int size, float centerAngle, float halfWidth, float outerRadiusRatio, float innerRadiusRatio)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * Mathf.Clamp(outerRadiusRatio, 0.05f, 0.5f);
            float inner = size * Mathf.Clamp(innerRadiusRatio, 0f, outerRadiusRatio - 0.02f);
            float normalizedCenter = NormalizeAngle180(centerAngle);
            float clampedHalfWidth = Mathf.Clamp(halfWidth, 1f, 179f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    Vector2 toPoint = point - center;
                    float dist = toPoint.magnitude;
                    if (dist < inner || dist > outer)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(toPoint.y, toPoint.x) * Mathf.Rad2Deg;
                    float delta = Mathf.Abs(Mathf.DeltaAngle(normalizedCenter, NormalizeAngle180(angle)));
                    texture.SetPixel(x, y, delta <= clampedHalfWidth ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Vector2 DegreeToCircle(float angle, float radius)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }

        private static float NormalizeAngle180(float angle)
        {
            float value = angle % 360f;
            if (value > 180f)
            {
                value -= 360f;
            }
            else if (value < -180f)
            {
                value += 360f;
            }

            return value;
        }

        private static bool WasPressed(Key key)
        {
            return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        private static bool IsHeld(Key key)
        {
            return key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
        }
    }
}
