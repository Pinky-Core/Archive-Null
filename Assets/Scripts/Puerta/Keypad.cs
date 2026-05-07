using System.Collections;
using UnityEngine;
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
    private string Answer = "929571";
    private Rigidbody playerRigidbody;

    void Start()
    {
        keypadCanvas.SetActive(false);
        playerRigidbody = player.GetComponent<Rigidbody>();
        originalConstraints = playerRigidbody.constraints;
    }

    public void Number(int number)
    {
        Ans.text += number.ToString();
    }


    public void Execute()
    {
        if (Ans.text == Answer)
        {
            Ans.text = "Correct";
            Door.SetBool("Open", true);
            doorAudioSource.PlayOneShot(doorOpenSound);
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
        keypadCanvas.SetActive(true);
        Ans.text = "";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        movementScript.enabled = false;
        lookScript.enabled = false;
        interactionText.gameObject.SetActive(false);
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
    }

    public void HideKeypad()
    {
        keypadCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        movementScript.enabled = true;
        lookScript.enabled = true;
        playerRigidbody.constraints = originalConstraints;
    }

    public void Cancel()
    {
        HideKeypad();
    }
}
