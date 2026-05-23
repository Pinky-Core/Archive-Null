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
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (EvidenceNotebookUI.IsAnyNotebookOpen || Keypad.IsAnyOpen)
        {
            IsRunning = false;
            cachedRigidbody.linearVelocity = new Vector3(0f, cachedRigidbody.linearVelocity.y, 0f);
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

        Vector2 targetVelocity = new Vector2(horizontal * targetMovingSpeed, vertical * targetMovingSpeed);
        cachedRigidbody.linearVelocity = transform.rotation * new Vector3(targetVelocity.x, cachedRigidbody.linearVelocity.y, targetVelocity.y);
    }
}
