using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string mapSceneName = "Phsiquiatra";
    public string mainMenuSceneName = "MainMenu";
    public Animator fadeAnimator;
    public float fadeDuration = 2.0f;
    public GameObject mainMenuUI;
    public GameObject optionsMenuUI;
    public GameObject quitMenuUI;
    public Camera menuCamera;

    [SerializeField] private AudioSource playAudioSource;
    [SerializeField] private AudioClip playSound;

    void Start()
    {
        menuCamera = Camera.main;
        mainMenuUI.SetActive(true);
        optionsMenuUI.SetActive(false);
        quitMenuUI.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ConfirmQuit();
        }
    }

    public void Play()
    {
        playAudioSource.PlayOneShot(playSound);
        fadeAnimator.SetBool("FadeOut", true);
        Time.timeScale = 1f;
        StartCoroutine(FadeOutAndLoadScene(mapSceneName));
    }

    IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        yield return new WaitForSeconds(fadeDuration);
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void Options()
    {
        mainMenuUI.SetActive(false);
        optionsMenuUI.SetActive(true);
    }

    public void Confirm()
    {
        optionsMenuUI.SetActive(false);
        mainMenuUI.SetActive(true);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void ConfirmQuit()
    {
        quitMenuUI.SetActive(true);
    }

    public void CancelQuit()
    {
        quitMenuUI.SetActive(false);
    }

    public void ReturnMainMenu()
    {
        Time.timeScale = 1f;
        StartCoroutine(FadeOutAndLoadScene(mainMenuSceneName));
    }
}
