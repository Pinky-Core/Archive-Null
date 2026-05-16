using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenu";
    public string inGameSceneName = "Phsiquiatra";

    public FirstPersonMovement movementScript;
    public FirstPersonLook lookScript;
    public GameObject pauseMenuUI;
    public Transform player;
    public Camera playerCamera;
    public Rigidbody playerRigidbody;

    private bool isPaused = false;
    public RigidbodyConstraints originalConstraints;

    void Start()
    {
        playerCamera = Camera.main;
        pauseMenuUI.SetActive(false);
        originalConstraints = playerRigidbody.constraints;
    }

    void Update()
    {
        if (FindObjectOfType<GlobalPauseMenu>() != null)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
            {
                Resume();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        movementScript.enabled = true;
        lookScript.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerRigidbody.constraints = originalConstraints;
    }

    void Pause()
    {
        movementScript.enabled = false;
        lookScript.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(ArchiveNull.UI.OfficeDissolveTransition.PendingOfficeRebuildPref, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }
}
