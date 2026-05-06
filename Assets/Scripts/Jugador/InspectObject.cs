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
    public float rotationSpeed = 300f;

    private Camera playerCamera;
    private GameObject currentObject = null;
    private bool isInspecting = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody playerRigidbody;
    private RigidbodyConstraints originalConstraints;

    void Start()
    {
        playerCamera = Camera.main;
        playerRigidbody = player.GetComponent<Rigidbody>();
        originalConstraints = playerRigidbody.constraints;
        inspectText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isInspecting)
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                ReleaseObject();
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                RotateObject();
            }
        }
        else
        {
            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = playerCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
            {
                if (hit.collider.gameObject.CompareTag("Inspectable"))
                {
                    inspectText.gameObject.SetActive(true);
                    inspectText.text = "(F) Inspeccionar";
                    if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                    {
                        TryPickObject(hit.collider.gameObject);
                    }
                }
                else
                {
                    inspectText.gameObject.SetActive(false);
                }
            }
            else
            {
                inspectText.gameObject.SetActive(false);
            }
        }
    }

    void TryPickObject(GameObject obj)
    {
        currentObject = obj;
        originalPosition = currentObject.transform.position;
        originalRotation = currentObject.transform.rotation;
        currentObject.GetComponent<Rigidbody>().isKinematic = true;
        currentObject.transform.position = inspectPosition.position;
        currentObject.transform.rotation = inspectPosition.rotation;
        isInspecting = true;
        movementScript.enabled = false;
        lookScript.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        inspectText.gameObject.SetActive(false);
    }

    void ReleaseObject()
    {
        if (currentObject != null)
        {
            currentObject.GetComponent<Rigidbody>().isKinematic = false;
            currentObject.transform.position = originalPosition;
            currentObject.transform.rotation = originalRotation;
            currentObject = null;
            isInspecting = false;
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
}
