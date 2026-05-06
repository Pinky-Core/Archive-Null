using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InGameMenu : MonoBehaviour
{
    public GameObject menuCanvas;
    public TMP_Text instructionsText;
    public TMP_Text controlsText;
    public TMP_Text tipsText;
    public TMP_Text objectivesText;
    private bool isMenuActive = false;

    public FirstPersonMovement movementScript;
    public FirstPersonLook lookScript;
    public Camera playerCamera;
    public Rigidbody playerRigidbody;

    private RigidbodyConstraints originalConstraints;

    void Start()
    {
        menuCanvas.SetActive(false);
        UpdateInstructions();
        playerCamera = Camera.main;
        originalConstraints = playerRigidbody.constraints;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        isMenuActive = !isMenuActive;
        menuCanvas.SetActive(isMenuActive);
        Cursor.lockState = isMenuActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isMenuActive;

        if (isMenuActive)
        {
            movementScript.enabled = false;
            lookScript.enabled = false;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
        else
        {
            movementScript.enabled = true;
            lookScript.enabled = true;
            playerRigidbody.constraints = originalConstraints;
        }
    }

    public void UpdateInstructions()
    {
        instructionsText.text = "Presiona M para abrir el menú de ayuda.\n" +
                                "Presiona E para interactuar con objetos.\n" +
                                "Presiona F para inspeccionar objetos.\n" +
                                "Presiona Esc para pausar el juego.";

        controlsText.text = "Controles:\n\nMoverse: W, A, S, D\nAgacharse: Ctrl";
        tipsText.text = "Consejos:\n\nRevisa todos los rincones para encontrar objetos útiles.\nAlgunos objetos pueden ser inspeccionados más de cerca.\nSi te quedas atascado, revisa las pistas que has encontrado.";
        objectivesText.text = "Objetivos:\nObjetivo actual: Encuentra la tarjeta de acceso.\nObjetivos secundarios: Explora la habitación para encontrar pistas.\n";
    }

    public void CloseMenu()
    {
        isMenuActive = false;
        menuCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        movementScript.enabled = true;
        lookScript.enabled = true;
        playerRigidbody.constraints = originalConstraints;
    }
}
