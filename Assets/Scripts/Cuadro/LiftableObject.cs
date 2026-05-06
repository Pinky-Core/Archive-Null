using UnityEngine;
using UnityEngine.InputSystem;

public class LiftableObject : Interactable
{
    public Transform player;
    public Transform carryPosition;
    private bool isBeingCarried = false;

    void Start()
    {
        interactionText = "Agarrar";
    }

    void Update()
    {
        if (isBeingCarried && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isBeingCarried = false;
            transform.parent = null;
        }

        if (isBeingCarried)
        {
            transform.position = carryPosition.position;
        }
    }

    public override void Interact()
    {
        if (!isBeingCarried)
        {
            isBeingCarried = true;
            transform.parent = carryPosition;
        }
    }
}
