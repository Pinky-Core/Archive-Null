using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ArchiveNull.Evidence;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 2.5f;
    public Text interactionText;
    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;
        if (interactionText != null)
        {
            interactionText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                return;
            }
        }

        if (Keypad.IsAnyOpen)
        {
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(false);
            }

            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Collide))
        {
            Keypad keypad = hit.collider.GetComponent<Keypad>();
            if (keypad == null)
            {
                keypad = hit.collider.GetComponentInParent<Keypad>();
            }

            PhoneEvidenceReader phone = hit.collider.GetComponent<PhoneEvidenceReader>();
            if (phone == null)
            {
                phone = hit.collider.GetComponentInParent<PhoneEvidenceReader>();
            }

            if (keypad != null)
            {
                if (interactionText != null)
                {
                    interactionText.gameObject.SetActive(true);
                    interactionText.text = "(" + GlobalInputBindings.GetDisplayName(GameInputAction.Interact) + ") Usar";
                }

                if (GlobalInputBindings.WasPressed(GameInputAction.Interact))
                {
                    keypad.ShowKeypad();
                    if (interactionText != null)
                    {
                        interactionText.gameObject.SetActive(false);
                    }
                }
            }
            else if (phone != null && !phone.IsOpen)
            {
                if (interactionText != null)
                {
                    interactionText.gameObject.SetActive(true);
                    interactionText.text = "(" + GlobalInputBindings.GetDisplayName(GameInputAction.Interact) + ") Recoger";
                }

                if (GlobalInputBindings.WasPressed(GameInputAction.Interact))
                {
                    phone.Collect();
                    if (interactionText != null)
                    {
                        interactionText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                if (interactionText != null)
                {
                    interactionText.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(false);
            }
        }
    }
}
