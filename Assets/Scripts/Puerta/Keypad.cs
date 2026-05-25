using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Keypad : MonoBehaviour
{
    [SerializeField] private Text Ans;
    [SerializeField] private Animator Door;
    [SerializeField] private GameObject keypadCanvas;
    [SerializeField] private FirstPersonMovement movementScript;
    [SerializeField] private FirstPersonLook lookScript;
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private Transform player;

    public Text interactionText;

    private RigidbodyConstraints originalConstraints;
    private string Answer = "229571";
    private Rigidbody playerRigidbody;
    private bool isOpen;
    private GraphicRaycaster keypadRaycaster;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);

    public static bool IsAnyOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        IsAnyOpen = false;
    }

    void Start()
    {
        IsAnyOpen = false;
        if (keypadCanvas != null)
        {
            keypadCanvas.SetActive(false);
            keypadRaycaster = keypadCanvas.GetComponentInChildren<GraphicRaycaster>(true);
        }

        if (player != null)
        {
            playerRigidbody = player.GetComponent<Rigidbody>();
            if (playerRigidbody != null)
            {
                originalConstraints = playerRigidbody.constraints;
            }
        }
    }

    void Update()
    {
        if (!isOpen || Keyboard.current == null)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryHandleUiClick();
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HideKeypad();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            Execute();
            return;
        }

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            if (Ans != null && Ans.text.Length > 0)
            {
                Ans.text = Ans.text.Substring(0, Ans.text.Length - 1);
            }

            return;
        }

        for (int i = 0; i <= 9; i++)
        {
            if (WasDigitPressed(i))
            {
                Number(i);
                return;
            }
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            isOpen = false;
            IsAnyOpen = false;
        }
    }

    public void Number(int number)
    {
        if (!isOpen || Ans == null)
        {
            return;
        }

        Ans.text += number.ToString();
    }


    public void Execute()
    {
        if (!isOpen || Ans == null)
        {
            return;
        }

        if (Ans.text == Answer)
        {
            Ans.text = "Correct";
            if (Door != null) Door.SetBool("Open", true);
            if (doorAudioSource != null && doorOpenSound != null) doorAudioSource.PlayOneShot(doorOpenSound);
            StartCoroutine(OpenDoorAndHideKeypad());
        }
        else
        {
            Ans.text = "Invalid";
            StartCoroutine(ClearText());
        }
    }

    IEnumerator ClearText()
    {
        yield return new WaitForSeconds(1f);
        Ans.text = "";
    }

    IEnumerator OpenDoorAndHideKeypad()
    {
        yield return new WaitForSeconds(0.5f);
        HideKeypad();
    }

    public void ShowKeypad()
    {
        isOpen = true;
        IsAnyOpen = true;
        EnsureEventSystem();
        if (keypadCanvas != null) keypadCanvas.SetActive(true);
        if (keypadRaycaster == null && keypadCanvas != null)
        {
            keypadRaycaster = keypadCanvas.GetComponentInChildren<GraphicRaycaster>(true);
        }
        if (Ans != null) Ans.text = "";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (movementScript != null) movementScript.enabled = false;
        if (lookScript != null) lookScript.enabled = false;
        if (interactionText != null) interactionText.gameObject.SetActive(false);
        if (playerRigidbody != null) playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    public void HideKeypad()
    {
        isOpen = false;
        IsAnyOpen = false;
        if (keypadCanvas != null) keypadCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (movementScript != null) movementScript.enabled = true;
        if (lookScript != null) lookScript.enabled = true;
        if (playerRigidbody != null) playerRigidbody.constraints = originalConstraints;
    }

    public void Cancel()
    {
        HideKeypad();
    }

    private static bool WasDigitPressed(int digit)
    {
        return digit switch
        {
            0 => Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame,
            1 => Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame,
            2 => Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame,
            3 => Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame,
            4 => Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame,
            5 => Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame,
            6 => Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame,
            7 => Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame,
            8 => Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame,
            9 => Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame,
            _ => false
        };
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    private void TryHandleUiClick()
    {
        if (EventSystem.current == null || keypadRaycaster == null || Mouse.current == null)
        {
            return;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        uiHits.Clear();
        keypadRaycaster.Raycast(pointer, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject go = uiHits[i].gameObject;
            if (go == null)
            {
                continue;
            }

            Button button = go.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }
        }
    }
}
