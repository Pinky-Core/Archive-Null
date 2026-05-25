using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 1f;
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
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Keypad keypad = hit.collider.GetComponent<Keypad>();
            if (keypad == null)
            {
                keypad = hit.collider.GetComponentInParent<Keypad>();
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
