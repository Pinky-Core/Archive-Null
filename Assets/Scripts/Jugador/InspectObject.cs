using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ArchiveNull.Evidence;

public class InspectObject : MonoBehaviour
{
    public float interactionDistance = 1f;
    public Transform inspectPosition;
    public Transform player;
    public FirstPersonMovement movementScript;
    public FirstPersonLook lookScript;
    public Text inspectText;
    public float rotationSpeed = 300f;
    public float zoomSpeed = 0.35f;
    public float minInspectDistance = 0.35f;
    public float maxInspectDistance = 1.25f;
    public float cameraClipPadding = 0.12f;
    public float holdRadius = 0.18f;
    public float obstructionPadding = 0.03f;
    public LayerMask obstructionLayers = ~0;
    public bool disableObjectCollisionWhileInspecting = true;

    [Header("Reticle")]
    public bool createReticleIfMissing = true;
    public Color reticleIdleColor = new Color(0.8f, 0.92f, 0.94f, 0.5f);
    public Color reticleInteractColor = new Color(0.15f, 1f, 0.92f, 1f);

    private Camera playerCamera;
    private GameObject currentObject = null;
    private bool isInspecting = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool originalKinematic;
    private Collider[] inspectedColliders;
    private bool[] inspectedColliderStates;
    private Rigidbody playerRigidbody;
    private Rigidbody inspectedRigidbody;
    private RigidbodyConstraints originalConstraints;
    private float inspectDistance;
    private float heldCollisionRadius;
    private Vector3 heldLocalCenterOffset;
    private readonly RaycastHit[] obstructionHits = new RaycastHit[16];
    private CanvasGroup reticleGroup;
    private RectTransform reticleRoot;
    private Image reticleDot;
    private Image reticleRing;

    public static bool IsAnyInspecting { get; private set; }

    void Start()
    {
        playerCamera = Camera.main;
        playerRigidbody = player.GetComponent<Rigidbody>();
        originalConstraints = playerRigidbody.constraints;
        if (inspectText != null)
        {
            inspectText.gameObject.SetActive(false);
        }

        if (createReticleIfMissing)
        {
            CreateReticle();
        }
    }

    void Update()
    {
        if (isInspecting)
        {
            UpdateHeldObjectPosition();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseObject();
                return;
            }

            if (GlobalInputBindings.WasPressed(GameInputAction.ReleaseInspect))
            {
                ReleaseObject();
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                RotateObject();
            }

            UpdateZoom();
        }
        else
        {
            if (EvidenceCameraController.IsAnyCameraModeActive)
            {
                SetReticleState(false, false);
                if (inspectText != null)
                {
                    inspectText.gameObject.SetActive(false);
                }

                return;
            }

            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = playerCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                if (hit.collider.gameObject.CompareTag("Inspectable"))
                {
                    SetReticleState(true, true);
                    if (inspectText != null)
                    {
                        inspectText.gameObject.SetActive(true);
                        inspectText.text = "(" + GlobalInputBindings.GetDisplayName(GameInputAction.Inspect) + ") Inspeccionar";
                    }

                    if (GlobalInputBindings.WasPressed(GameInputAction.Inspect))
                    {
                        TryPickObject(hit.collider.gameObject);
                    }
                }
                else
                {
                    SetReticleState(true, false);
                    if (inspectText != null)
                    {
                        inspectText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                SetReticleState(true, false);
                if (inspectText != null)
                {
                    inspectText.gameObject.SetActive(false);
                }
            }
        }
    }

    void TryPickObject(GameObject obj)
    {
        currentObject = obj;
        originalPosition = currentObject.transform.position;
        originalRotation = currentObject.transform.rotation;
        inspectedRigidbody = currentObject.GetComponent<Rigidbody>();
        if (inspectedRigidbody != null)
        {
            originalKinematic = inspectedRigidbody.isKinematic;
            inspectedRigidbody.isKinematic = true;
            inspectedRigidbody.linearVelocity = Vector3.zero;
            inspectedRigidbody.angularVelocity = Vector3.zero;
        }

        CacheAndDisableInspectedColliders();
        inspectDistance = Mathf.Clamp(Vector3.Distance(playerCamera.transform.position, inspectPosition.position), minInspectDistance, maxInspectDistance);
        currentObject.transform.rotation = inspectPosition.rotation;
        currentObject.transform.position = GetSafeInspectObjectPosition();
        isInspecting = true;
        IsAnyInspecting = true;
        movementScript.enabled = false;
        lookScript.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        SetReticleState(false, false);
        if (inspectText != null)
        {
            inspectText.gameObject.SetActive(false);
        }
    }

    void ReleaseObject()
    {
        if (currentObject != null)
        {
            if (inspectedRigidbody != null)
            {
                inspectedRigidbody.isKinematic = originalKinematic;
            }

            RestoreInspectedColliders();
            currentObject.transform.position = originalPosition;
            currentObject.transform.rotation = originalRotation;
            currentObject = null;
            inspectedRigidbody = null;
            isInspecting = false;
            IsAnyInspecting = false;
            movementScript.enabled = true;
            lookScript.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            playerRigidbody.constraints = originalConstraints;
            SetReticleState(true, false);
        }
    }

    void RotateObject()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        float rotateX = mouseDelta.x * rotationSpeed * Time.deltaTime * 0.1f;
        float rotateY = mouseDelta.y * rotationSpeed * Time.deltaTime * 0.1f;

        currentObject.transform.Rotate(playerCamera.transform.up, -rotateX, Space.World);
        currentObject.transform.Rotate(playerCamera.transform.right, rotateY, Space.World);
    }

    void UpdateZoom()
    {
        if (Mouse.current == null || currentObject == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        inspectDistance = Mathf.Clamp(inspectDistance - scroll * zoomSpeed * 0.01f, GetMinimumSafeInspectDistance(), maxInspectDistance);
        UpdateHeldObjectPosition();
    }

    void UpdateHeldObjectPosition()
    {
        if (currentObject == null)
        {
            return;
        }

        currentObject.transform.position = GetSafeInspectObjectPosition();
    }

    Vector3 GetSafeInspectObjectPosition()
    {
        Vector3 desiredCenter = GetSafeInspectCenterPosition();
        return desiredCenter - currentObject.transform.rotation * heldLocalCenterOffset;
    }

    Vector3 GetSafeInspectCenterPosition()
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        float distance = Mathf.Clamp(inspectDistance, GetMinimumSafeInspectDistance(), maxInspectDistance);
        float radius = Mathf.Max(holdRadius, heldCollisionRadius);
        int hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, obstructionHits, distance, obstructionLayers, QueryTriggerInteraction.Ignore);
        float nearestDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = obstructionHits[i].collider;
            if (hitCollider == null || IsIgnoredObstruction(hitCollider))
            {
                continue;
            }

            nearestDistance = Mathf.Min(nearestDistance, obstructionHits[i].distance);
        }

        return origin + direction * Mathf.Max(0.02f, nearestDistance - obstructionPadding);
    }

    float GetMinimumSafeInspectDistance()
    {
        float cameraNear = playerCamera != null ? playerCamera.nearClipPlane : 0.03f;
        return Mathf.Max(minInspectDistance, cameraNear + heldCollisionRadius + cameraClipPadding);
    }

    void CacheAndDisableInspectedColliders()
    {
        inspectedColliders = currentObject.GetComponentsInChildren<Collider>(true);
        inspectedColliderStates = new bool[inspectedColliders.Length];
        Bounds combinedBounds = default;
        bool hasBounds = false;
        Bounds visualBounds = default;
        bool hasVisualBounds = false;
        Renderer[] renderers = currentObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasVisualBounds)
            {
                visualBounds = renderer.bounds;
                hasVisualBounds = true;
            }
            else
            {
                visualBounds.Encapsulate(renderer.bounds);
            }
        }

        for (int i = 0; i < inspectedColliders.Length; i++)
        {
            inspectedColliderStates[i] = inspectedColliders[i] != null && inspectedColliders[i].enabled;
            if (inspectedColliders[i] != null && inspectedColliderStates[i])
            {
                if (!hasBounds)
                {
                    combinedBounds = inspectedColliders[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(inspectedColliders[i].bounds);
                }
            }

            if (disableObjectCollisionWhileInspecting && inspectedColliders[i] != null)
            {
                inspectedColliders[i].enabled = false;
            }
        }

        if (hasBounds)
        {
            heldCollisionRadius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y, combinedBounds.extents.z);
        }
        else if (hasVisualBounds)
        {
            heldCollisionRadius = Mathf.Max(visualBounds.extents.x, visualBounds.extents.y, visualBounds.extents.z);
        }
        else
        {
            heldCollisionRadius = holdRadius;
        }

        Bounds centerBounds = hasVisualBounds ? visualBounds : combinedBounds;
        heldLocalCenterOffset = (hasVisualBounds || hasBounds) ? currentObject.transform.InverseTransformPoint(centerBounds.center) : Vector3.zero;
    }

    void RestoreInspectedColliders()
    {
        if (inspectedColliders == null || inspectedColliderStates == null)
        {
            return;
        }

        for (int i = 0; i < inspectedColliders.Length; i++)
        {
            if (inspectedColliders[i] != null)
            {
                inspectedColliders[i].enabled = i < inspectedColliderStates.Length && inspectedColliderStates[i];
            }
        }

        inspectedColliders = null;
        inspectedColliderStates = null;
        heldCollisionRadius = 0f;
        heldLocalCenterOffset = Vector3.zero;
    }

    bool IsIgnoredObstruction(Collider candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        if (currentObject != null && candidate.transform.IsChildOf(currentObject.transform))
        {
            return true;
        }

        return player != null && candidate.transform.IsChildOf(player);
    }

    void CreateReticle()
    {
        if (reticleGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("InspectReticleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = new GameObject("Reticle", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasObject.transform, false);
        reticleRoot = root.GetComponent<RectTransform>();
        reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRoot.pivot = new Vector2(0.5f, 0.5f);
        reticleRoot.sizeDelta = new Vector2(28f, 28f);
        reticleGroup = root.GetComponent<CanvasGroup>();
        reticleGroup.interactable = false;
        reticleGroup.blocksRaycasts = false;

        reticleRing = CreateReticleImage("Ring", reticleRoot);
        reticleRing.rectTransform.sizeDelta = new Vector2(18f, 18f);
        reticleRing.sprite = CreateRingSprite(64);

        reticleDot = CreateReticleImage("Dot", reticleRoot);
        reticleDot.rectTransform.sizeDelta = new Vector2(4f, 4f);
        reticleDot.sprite = CreateDotSprite(32);

        SetReticleState(true, false);
    }

    Image CreateReticleImage(string name, RectTransform parent)
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
        return image;
    }

    void SetReticleState(bool visible, bool interactable)
    {
        if (reticleGroup == null)
        {
            return;
        }

        reticleGroup.alpha = visible ? 1f : 0f;
        Color color = interactable ? reticleInteractColor : reticleIdleColor;
        if (reticleDot != null) reticleDot.color = color;
        if (reticleRing != null) reticleRing.color = color;
        if (reticleRoot != null) reticleRoot.localScale = Vector3.one * (interactable ? 1.28f : 1f);
    }

    static Sprite CreateDotSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = Mathf.Clamp01((radius - Vector2.Distance(new Vector2(x, y), center)) * 0.4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    static Sprite CreateRingSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.42f;
        float thickness = size * 0.07f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float delta = Mathf.Abs(Vector2.Distance(new Vector2(x, y), center) - radius);
                float alpha = Mathf.Clamp01((thickness - delta) * 0.45f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
