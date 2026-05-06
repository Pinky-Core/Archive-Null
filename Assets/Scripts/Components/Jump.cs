using UnityEngine;
using UnityEngine.InputSystem;

public class Jump : MonoBehaviour
{
    private Rigidbody cachedRigidbody;
    public float jumpStrength = 2;
    public event System.Action Jumped;

    [SerializeField, Tooltip("Prevents jumping when the transform is in mid-air.")]
    GroundCheck groundCheck;

    void Reset()
    {
        groundCheck = GetComponentInChildren<GroundCheck>();
    }

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        bool jumpPressed = Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
        if (jumpPressed && (!groundCheck || groundCheck.isGrounded))
        {
            cachedRigidbody.AddForce(Vector3.up * 100 * jumpStrength);
            Jumped?.Invoke();
        }
    }
}
