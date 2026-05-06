using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Interaction : MonoBehaviour
{
    public float interactionDistance = 1f;
    public Text interactionText;
    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;
        interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                interactionText.gameObject.SetActive(true);
                interactionText.text = interactable.interactionText;

                if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    interactable.Interact();
                    interactionText.gameObject.SetActive(false);
                }
            }
            else
            {
                interactionText.gameObject.SetActive(false);
            }
        }
        else
        {
            interactionText.gameObject.SetActive(false);
        }
    }
}
