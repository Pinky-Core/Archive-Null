using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InspectObject : MonoBehaviour
{
    public float interactionDistance = 1f;
    public Transform inspectPosition;
    public Transform player;
    public FirstPersonMovement movementScript;
    public FirstPersonLook lookScript;
    public Text inspectText;
    public Key inspectKey = Key.E;
    public Key releaseKey = Key.E;
    public float rotationSpeed = 300f;
    public float zoomSpeed = 0.35f;
    public float minInspectDistance = 0.35f;
    public float maxInspectDistance = 1.25f;
    public float holdRadius = 0.18f;
    public LayerMask obstructionLayers = ~0;
    public bool disableObjectCollisionWhileInspecting = true;

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
    }

    void Update()
    {
        if (isInspecting)
        {
            UpdateHeldObjectPosition();

            if (WasKeyPressed(releaseKey))
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
            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = playerCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                if (hit.collider.gameObject.CompareTag("Inspectable"))
                {
                    if (inspectText != null)
                    {
                        inspectText.gameObject.SetActive(true);
                        inspectText.text = "(" + inspectKey.ToString().ToUpperInvariant() + ") Inspeccionar";
                    }

                    if (WasKeyPressed(inspectKey))
                    {
                        TryPickObject(hit.collider.gameObject);
                    }
                }
                else
                {
                    if (inspectText != null)
                    {
                        inspectText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
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
        currentObject.transform.position = GetSafeInspectPosition();
        currentObject.transform.rotation = inspectPosition.rotation;
        isInspecting = true;
        IsAnyInspecting = true;
        movementScript.enabled = false;
        lookScript.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
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

        inspectDistance = Mathf.Clamp(inspectDistance + scroll * zoomSpeed * 0.01f, minInspectDistance, maxInspectDistance);
        UpdateHeldObjectPosition();
    }

    void UpdateHeldObjectPosition()
    {
        if (currentObject == null)
        {
            return;
        }

        currentObject.transform.position = GetSafeInspectPosition();
    }

    Vector3 GetSafeInspectPosition()
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        float distance = Mathf.Clamp(inspectDistance, minInspectDistance, maxInspectDistance);

        if (Physics.SphereCast(origin, holdRadius, direction, out RaycastHit hit, distance, obstructionLayers, QueryTriggerInteraction.Ignore))
        {
            return origin + direction * Mathf.Max(minInspectDistance, hit.distance - holdRadius);
        }

        return origin + direction * distance;
    }

    void CacheAndDisableInspectedColliders()
    {
        inspectedColliders = currentObject.GetComponentsInChildren<Collider>(true);
        inspectedColliderStates = new bool[inspectedColliders.Length];
        for (int i = 0; i < inspectedColliders.Length; i++)
        {
            inspectedColliderStates[i] = inspectedColliders[i] != null && inspectedColliders[i].enabled;
            if (disableObjectCollisionWhileInspecting && inspectedColliders[i] != null)
            {
                inspectedColliders[i].enabled = false;
            }
        }
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
    }

    static bool WasKeyPressed(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }
}
