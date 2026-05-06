using UnityEngine;

public class PlaceObjectTrigger : MonoBehaviour
{
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenSound;

    public Animator PaintAnimator;
    public string requiredTag = "Liftable";
    private bool isTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (!isTriggered && other.CompareTag(requiredTag))
        {
            isTriggered = true;
            doorAudioSource.PlayOneShot(doorOpenSound);
            PaintAnimator.SetBool("Open", true);
        }
    }
}
