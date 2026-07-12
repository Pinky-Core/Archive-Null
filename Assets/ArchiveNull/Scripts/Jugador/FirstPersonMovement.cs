using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ArchiveNull.Evidence;

public class FirstPersonMovement : MonoBehaviour
{
    public float speed = 5;

    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    public float runSpeed = 9;

    [Header("Physics Safety")]
    [SerializeField] private float groundedCheckDistance = 0.22f;
    [SerializeField] private float safePositionInterval = 0.35f;
    [SerializeField] private float fallRecoveryDistance = 8f;
    [SerializeField] private float collisionSkin = 0.035f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("First Person View")]
    [SerializeField] private Transform viewTransform;
    [SerializeField] private float lookSensitivity = 1.5f;
    [SerializeField] private float lookSmoothing = 1.5f;
    [SerializeField] private float minimumPitch = -85f;
    [SerializeField] private float maximumPitch = 85f;

    [Header("World Collider Size")]
    [SerializeField] private float worldColliderRadius = 0.5f;
    [SerializeField] private float worldColliderHeight = 1.8f;

    private Rigidbody cachedRigidbody;
    private CapsuleCollider capsuleCollider;
    private CharacterController characterController;
    private Transform movementTransform;
    private Vector2 lookAngles;
    private Vector2 smoothedLookDelta;
    private float verticalVelocity;
    private float nextDebugLogTime;
    private float nextSafePositionTime;
    private string lastBlockReason = string.Empty;
    private Vector3 lastPosition;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private bool hasSafePosition;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] movementHits = new RaycastHit[24];
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    public Vector3 LastSafePosition => hasSafePosition ? lastSafePosition : initialPosition;
    public Quaternion LastSafeRotation => hasSafePosition ? lastSafeRotation : initialRotation;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponentInParent<Rigidbody>();
        }

        if (cachedRigidbody == null)
        {
            Debug.LogError("[FirstPersonMovement] No Rigidbody found on this object or parents. Disabling script.", this);
            enabled = false;
            return;
        }

        movementTransform = cachedRigidbody.transform;
        capsuleCollider = cachedRigidbody.GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            capsuleCollider = cachedRigidbody.GetComponentInChildren<CapsuleCollider>();
        }

        characterController = cachedRigidbody.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = cachedRigidbody.gameObject.AddComponent<CharacterController>();
        }

        ConfigureCharacterController();
        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        if (viewTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            viewTransform = playerCamera != null ? playerCamera.transform : null;
        }

        lookAngles.x = movementTransform.eulerAngles.y;
        if (viewTransform != null)
        {
            float initialPitch = viewTransform.localEulerAngles.x;
            lookAngles.y = initialPitch > 180f ? initialPitch - 360f : initialPitch;
        }

        cachedRigidbody.useGravity = false;
        cachedRigidbody.isKinematic = true;
        cachedRigidbody.detectCollisions = false;
        cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        cachedRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        initialPosition = movementTransform.position;
        initialRotation = movementTransform.rotation;
        lastSafePosition = initialPosition;
        lastSafeRotation = initialRotation;
        lastPosition = movementTransform.position;

        // If this script is on a camera/child and parent already has one, disable duplicate writer.
        FirstPersonMovement parentMovement = cachedRigidbody.GetComponent<FirstPersonMovement>();
        if (parentMovement != null && parentMovement != this)
        {
            Debug.LogWarning("[FirstPersonMovement] Duplicate movement script detected. Disable the one on camera/child.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(GetBlockReason()) || Cursor.lockState != CursorLockMode.Locked)
        {
            smoothedLookDelta = Vector2.zero;
            return;
        }

        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 targetDelta = mouseDelta * (lookSensitivity * 0.1f);
        float smoothing = Mathf.Max(1f, lookSmoothing);
        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, targetDelta, 1f / smoothing);
        lookAngles.x += smoothedLookDelta.x;
        lookAngles.y = Mathf.Clamp(lookAngles.y - smoothedLookDelta.y, minimumPitch, maximumPitch);

        if (viewTransform != null)
        {
            viewTransform.localRotation = Quaternion.Euler(lookAngles.y, 0f, 0f);
        }
    }

    void FixedUpdate()
    {
        UpdateSafetyState();
        string blockReason = GetBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            IsRunning = false;
            LogBlockState(blockReason);
            lastPosition = movementTransform.position;
            return;
        }

        movementTransform.rotation = Quaternion.Euler(0f, lookAngles.x, 0f);
        IsRunning = canRun && GlobalInputBindings.IsPressed(GameInputAction.Run);

        float targetMovingSpeed = IsRunning ? runSpeed : speed;
        if (speedOverrides.Count > 0)
        {
            targetMovingSpeed = speedOverrides[speedOverrides.Count - 1]();
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (GlobalInputBindings.IsPressed(GameInputAction.MoveLeft)) horizontal -= 1f;
        if (GlobalInputBindings.IsPressed(GameInputAction.MoveRight)) horizontal += 1f;
        if (GlobalInputBindings.IsPressed(GameInputAction.MoveBackward)) vertical -= 1f;
        if (GlobalInputBindings.IsPressed(GameInputAction.MoveForward)) vertical += 1f;

        // Fallback if saved bindings are invalid or missing.
        if (horizontal == 0f && vertical == 0f && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            if (Keyboard.current.wKey.isPressed) vertical += 1f;
        }

        Vector3 inputDirection = movementTransform.rotation * new Vector3(horizontal, 0f, vertical);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        Vector3 planarDelta = inputDirection * (targetMovingSpeed * Time.fixedDeltaTime);
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.fixedDeltaTime;
        }

        Vector3 movementDelta = new Vector3(
            planarDelta.x,
            verticalVelocity * Time.fixedDeltaTime,
            planarDelta.z);
        characterController.Move(movementDelta);

        float movedDistance = Vector3.Distance(movementTransform.position, lastPosition);
        bool hasInput = Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f;
        if (hasInput && movedDistance < 0.0002f && Time.unscaledTime >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.unscaledTime + 1f;
            Debug.Log(
                $"[FirstPersonMovement] input but no displacement. rb={cachedRigidbody.name} " +
                $"constraints={cachedRigidbody.constraints} isKinematic={cachedRigidbody.isKinematic} " +
                $"attemptedDelta={planarDelta} pos={movementTransform.position}",
                this);
        }

        lastPosition = movementTransform.position;
    }

    public void ConfigureLook(Transform cameraTransform, float sensitivity, float smoothing)
    {
        if (cameraTransform != null)
        {
            viewTransform = cameraTransform;
        }

        lookSensitivity = sensitivity;
        lookSmoothing = smoothing;
    }

    public void SetLookSensitivity(float sensitivity)
    {
        lookSensitivity = sensitivity;
    }

    private void ConfigureCharacterController()
    {
        if (characterController == null)
        {
            return;
        }

        Vector3 scale = characterController.transform.lossyScale;
        float horizontalScale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float verticalScale = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        characterController.radius = worldColliderRadius / horizontalScale;
        characterController.height = Mathf.Max(
            worldColliderHeight / verticalScale,
            characterController.radius * 2f);
        characterController.center = Vector3.up * (characterController.height * 0.5f);
        characterController.skinWidth = Mathf.Max(0.01f, 0.08f / horizontalScale);
        characterController.stepOffset = Mathf.Min(0.3f / verticalScale, characterController.height * 0.35f);
        characterController.slopeLimit = 50f;
        characterController.minMoveDistance = 0f;
        Physics.SyncTransforms();
    }

    private void MoveWithCollision(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return;
        }

        Vector3 direction = displacement / distance;
        GetCapsuleWorld(out Vector3 bottom, out Vector3 top, out float radius);
        if (!TryGetNearestMovementHit(bottom, top, radius, direction, distance + collisionSkin, out RaycastHit hit))
        {
            cachedRigidbody.MovePosition(cachedRigidbody.position + displacement);
            return;
        }

        float allowedDistance = Mathf.Max(0f, hit.distance - collisionSkin);
        Vector3 firstMove = direction * Mathf.Min(distance, allowedDistance);
        Vector3 positionAfterFirstMove = cachedRigidbody.position + firstMove;
        Vector3 remaining = displacement - firstMove;

        Vector3 wallNormal = hit.normal;
        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude <= 0.0001f)
        {
            cachedRigidbody.MovePosition(positionAfterFirstMove);
            return;
        }

        wallNormal.Normalize();
        Vector3 slide = Vector3.ProjectOnPlane(remaining, wallNormal);
        float slideDistance = slide.magnitude;
        if (slideDistance <= 0.0001f)
        {
            cachedRigidbody.MovePosition(positionAfterFirstMove);
            return;
        }

        Vector3 localOffset = positionAfterFirstMove - cachedRigidbody.position;
        Vector3 slideBottom = bottom + localOffset;
        Vector3 slideTop = top + localOffset;
        Vector3 slideDirection = slide / slideDistance;
        if (TryGetNearestMovementHit(slideBottom, slideTop, radius, slideDirection, slideDistance + collisionSkin, out RaycastHit slideHit))
        {
            slideDistance = Mathf.Max(0f, Mathf.Min(slideDistance, slideHit.distance - collisionSkin));
        }

        cachedRigidbody.MovePosition(positionAfterFirstMove + slideDirection * slideDistance);
    }

    private bool TryGetNearestMovementHit(
        Vector3 bottom,
        Vector3 top,
        float radius,
        Vector3 direction,
        float distance,
        out RaycastHit nearestHit)
    {
        int hitCount = Physics.CapsuleCastNonAlloc(
            bottom,
            top,
            radius * 0.96f,
            direction,
            movementHits,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        nearestHit = default;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = movementHits[i];
            bool floorOrCeiling = Mathf.Abs(candidate.normal.y) > 0.65f;
            if (IsSelfCollider(candidate.collider) || floorOrCeiling || candidate.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = candidate.distance;
            nearestHit = candidate;
            found = true;
        }

        return found;
    }

    public bool TryRestorePosition(Vector3 requestedPosition, Quaternion requestedRotation)
    {
        Vector3 safePosition = requestedPosition;
        if (!TryFindGroundedPosition(requestedPosition, out safePosition) || IsPositionBlocked(safePosition))
        {
            safePosition = initialPosition;
            requestedRotation = initialRotation;
            if (!TryFindGroundedPosition(initialPosition, out Vector3 groundedInitial))
            {
                groundedInitial = initialPosition;
            }

            safePosition = groundedInitial;
        }

        Teleport(safePosition, requestedRotation);
        lastSafePosition = safePosition;
        lastSafeRotation = requestedRotation;
        hasSafePosition = true;
        return true;
    }

    private void UpdateSafetyState()
    {
        if (movementTransform.position.y < LastSafePosition.y - Mathf.Max(2f, fallRecoveryDistance))
        {
            Teleport(LastSafePosition, LastSafeRotation);
            return;
        }

        if (Time.unscaledTime < nextSafePositionTime || !IsGrounded() || IsPositionBlocked(movementTransform.position))
        {
            return;
        }

        nextSafePositionTime = Time.unscaledTime + safePositionInterval;
        lastSafePosition = movementTransform.position;
        lastSafeRotation = movementTransform.rotation;
        hasSafePosition = true;
    }

    private void Teleport(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        movementTransform.SetPositionAndRotation(position, rotation);
        verticalVelocity = 0f;
        if (characterController != null)
        {
            characterController.enabled = wasEnabled;
        }

        Physics.SyncTransforms();
    }

    private bool IsGrounded()
    {
        if (characterController != null && characterController.enabled)
        {
            return characterController.isGrounded;
        }

        GetCapsuleWorld(out Vector3 bottom, out _, out float radius);
        Vector3 origin = bottom + Vector3.up * (radius + 0.03f);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius * 0.8f,
            Vector3.down,
            groundHits,
            groundedCheckDistance + radius,
            collisionLayers,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsSelfCollider(groundHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindGroundedPosition(Vector3 requestedPosition, out Vector3 groundedPosition)
    {
        float castHeight = Mathf.Max(3f, fallRecoveryDistance);
        Vector3 origin = requestedPosition + Vector3.up * 1.5f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, castHeight, collisionLayers, QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearestHit = default;
        bool foundGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            if (IsSelfCollider(groundHits[i].collider) || groundHits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = groundHits[i].distance;
            nearestHit = groundHits[i];
            foundGround = true;
        }

        if (foundGround)
        {
            float rootHeightAboveGround = 0.9f;
            if (capsuleCollider != null)
            {
                float scaleY = Mathf.Abs(capsuleCollider.transform.lossyScale.y);
                float centerY = capsuleCollider.center.y * scaleY;
                float halfHeight = capsuleCollider.height * scaleY * 0.5f;
                rootHeightAboveGround = halfHeight - centerY;
            }

            groundedPosition = nearestHit.point + Vector3.up * (rootHeightAboveGround + 0.03f);
            return true;
        }

        groundedPosition = requestedPosition;
        return false;
    }

    private bool IsSelfCollider(Collider candidate)
    {
        return candidate == null ||
               candidate == capsuleCollider ||
               candidate.transform == movementTransform ||
               candidate.transform.IsChildOf(movementTransform);
    }

    private bool IsPositionBlocked(Vector3 position)
    {
        if (capsuleCollider == null)
        {
            return false;
        }

        GetCapsuleWorld(position, out Vector3 bottom, out Vector3 top, out float radius);
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius * 0.92f, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap != null && overlap != capsuleCollider && !overlap.transform.IsChildOf(movementTransform))
            {
                return true;
            }
        }

        return false;
    }

    private void GetCapsuleWorld(out Vector3 bottom, out Vector3 top, out float radius)
    {
        GetCapsuleWorld(movementTransform.position, out bottom, out top, out radius);
    }

    private void GetCapsuleWorld(Vector3 rootPosition, out Vector3 bottom, out Vector3 top, out float radius)
    {
        if (capsuleCollider == null)
        {
            radius = 0.35f;
            bottom = rootPosition + Vector3.up * radius;
            top = rootPosition + Vector3.up * 1.5f;
            return;
        }

        Vector3 scale = capsuleCollider.transform.lossyScale;
        radius = capsuleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(capsuleCollider.height * Mathf.Abs(scale.y), radius * 2f);
        Vector3 center = rootPosition + movementTransform.rotation * Vector3.Scale(capsuleCollider.center, scale);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);
        bottom = center - Vector3.up * halfLine;
        top = center + Vector3.up * halfLine;
    }

    private string GetBlockReason()
    {
        if (EvidenceNotebookUI.IsAnyNotebookOpen)
        {
            return "NotebookOpen";
        }

        if (Keypad.IsAnyOpen)
        {
            return "KeypadOpen";
        }

        if (PhoneEvidenceReader.IsAnyOpen)
        {
            return "PhoneOpen";
        }

        if (EvidenceCameraController.IsAnyRadialMenuOpen)
        {
            return "RadialOpen";
        }

        return string.Empty;
    }

    private void LogBlockState(string reason)
    {
        if (reason == lastBlockReason && Time.unscaledTime < nextDebugLogTime)
        {
            return;
        }

        lastBlockReason = reason;
        nextDebugLogTime = Time.unscaledTime + 1f;
        Debug.Log($"[FirstPersonMovement] blocked={reason} rb={cachedRigidbody.name} constraints={cachedRigidbody.constraints} isKinematic={cachedRigidbody.isKinematic}", this);
    }
}
