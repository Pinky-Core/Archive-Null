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

    private Rigidbody cachedRigidbody;
    private Transform movementTransform;
    private float nextDebugLogTime;
    private string lastBlockReason = string.Empty;
    private Vector3 lastPosition;
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

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
        lastPosition = movementTransform.position;

        // If this script is on a camera/child and parent already has one, disable duplicate writer.
        FirstPersonMovement parentMovement = cachedRigidbody.GetComponent<FirstPersonMovement>();
        if (parentMovement != null && parentMovement != this)
        {
            Debug.LogWarning("[FirstPersonMovement] Duplicate movement script detected. Disable the one on camera/child.", this);
            enabled = false;
        }
    }

    void FixedUpdate()
    {
        string blockReason = GetBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            IsRunning = false;
            cachedRigidbody.linearVelocity = new Vector3(0f, cachedRigidbody.linearVelocity.y, 0f);
            LogBlockState(blockReason);
            lastPosition = movementTransform.position;
            return;
        }

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
        Vector3 targetPosition = cachedRigidbody.position + new Vector3(planarDelta.x, 0f, planarDelta.z);
        cachedRigidbody.MovePosition(targetPosition);

        float movedDistance = Vector3.Distance(movementTransform.position, lastPosition);
        bool hasInput = Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f;
        if (hasInput && movedDistance < 0.0002f && Time.unscaledTime >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.unscaledTime + 1f;
            Debug.Log($"[FirstPersonMovement] input but no displacement. rb={cachedRigidbody.name} constraints={cachedRigidbody.constraints} isKinematic={cachedRigidbody.isKinematic} pos={movementTransform.position}", this);
        }

        lastPosition = movementTransform.position;
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
