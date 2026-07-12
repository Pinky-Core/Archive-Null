using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Final1 : MonoBehaviour
{
    public float interactionDistance = 1f;
    public string escapeSceneName = "EscapeScene";
    public Animator doorAnimator;
    public string doorOpenAnimationName = "ExitDoorOpen";

    private bool hasCard = false;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.CompareTag("Card"))
            {
                CollectCard(hit.collider.gameObject);
            }
            else if (hit.collider.CompareTag("ExitKeypad"))
            {
                TryOpenDoor(hit.collider.gameObject);
            }
        }
    }

    void CollectCard(GameObject card)
    {
        hasCard = true;
        Destroy(card);
    }

    void TryOpenDoor(GameObject keypad)
    {
        if (hasCard)
        {
            if (doorAnimator != null)
            {
                doorAnimator.Play(doorOpenAnimationName);
                StartCoroutine(WaitForDoorToOpen());
            }
            else
            {
                Debug.LogError("Animator de la puerta no asignado.");
            }
        }
        else
        {
            Debug.Log("Necesitas una tarjeta para abrir esta puerta.");
        }
    }

    IEnumerator WaitForDoorToOpen()
    {
        yield return new WaitForSeconds(doorAnimator.GetCurrentAnimatorStateInfo(0).length);
        SceneManager.LoadScene(escapeSceneName, LoadSceneMode.Single);
    }
}
