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
        interactionText.gameObject.SetActive(false);
    }

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            Keypad keypad = hit.collider.GetComponent<Keypad>();
            if (keypad != null)
            {
                interactionText.gameObject.SetActive(true);
                interactionText.text = "(E) Usar";

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    keypad.ShowKeypad();
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
