using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ArchiveNull.Evidence;

public class FirstPersonMovement : MonoBehaviour
{
    public const string PrefControlScheme = "global.pause.control.scheme";

    public float speed = 5;

    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    public float runSpeed = 9;
    public KeyCode runningKey = KeyCode.LeftShift;
    public bool useArrowMovement;

    private Rigidbody cachedRigidbody;
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        useArrowMovement = PlayerPrefs.GetInt(PrefControlScheme, useArrowMovement ? 1 : 0) == 1;
    }

    void FixedUpdate()
    {
        if (EvidenceNotebookUI.IsAnyNotebookOpen)
        {
            IsRunning = false;
            cachedRigidbody.linearVelocity = new Vector3(0f, cachedRigidbody.linearVelocity.y, 0f);
            return;
        }

        IsRunning = canRun && Keyboard.current != null && (runningKey == KeyCode.LeftShift ? Keyboard.current.leftShiftKey.isPressed : Keyboard.current.rightShiftKey.isPressed);

        float targetMovingSpeed = IsRunning ? runSpeed : speed;
        if (speedOverrides.Count > 0)
        {
            targetMovingSpeed = speedOverrides[speedOverrides.Count - 1]();
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (Keyboard.current != null)
        {
            if (useArrowMovement)
            {
                if (Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
                if (Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
                if (Keyboard.current.upArrowKey.isPressed) vertical += 1f;
            }
            else
            {
                if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed) horizontal += 1f;
                if (Keyboard.current.sKey.isPressed) vertical -= 1f;
                if (Keyboard.current.wKey.isPressed) vertical += 1f;
            }
        }

        Vector2 targetVelocity = new Vector2(horizontal * targetMovingSpeed, vertical * targetMovingSpeed);
        cachedRigidbody.linearVelocity = transform.rotation * new Vector3(targetVelocity.x, cachedRigidbody.linearVelocity.y, targetVelocity.y);
    }
}
