using UnityEngine;
using UnityEngine.InputSystem;
using ArchiveNull.Evidence;

public class FirstPersonLook : MonoBehaviour
{
    [SerializeField] private Transform character;
    public float sensitivity = 2;
    public float smoothing = 1.5f;

    private FirstPersonMovement controller;

    void Reset()
    {
        character = GetComponentInParent<FirstPersonMovement>().transform;
    }

    void Awake()
    {
        controller = character != null
            ? character.GetComponent<FirstPersonMovement>()
            : GetComponentInParent<FirstPersonMovement>();
        if (controller != null)
        {
            controller.ConfigureLook(transform, sensitivity, smoothing);
        }
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (controller == null)
        {
            controller = GetComponentInParent<FirstPersonMovement>();
        }

        if (controller != null)
        {
            controller.ConfigureLook(transform, sensitivity, smoothing);
        }
    }

    void Update()
    {
        if (controller != null)
        {
            controller.SetLookSensitivity(sensitivity);
        }
    }
}
