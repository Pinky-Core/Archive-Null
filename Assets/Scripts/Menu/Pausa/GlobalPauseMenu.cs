using System.Collections;
using ArchiveNull.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GlobalPauseMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string PrefMasterVolume = "global.pause.master.volume";
    private const string PrefLookSensitivity = "global.pause.look.sensitivity";
    private const string PrefMoveSpeed = "global.pause.move.speed";

    private static GlobalPauseMenu instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureInstanceForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstanceForScene(scene);
    }

    private static void EnsureInstanceForScene(Scene scene)
    {
        bool isMainMenu = string.Equals(scene.name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
        if (isMainMenu)
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            Time.timeScale = 1f;
            return;
        }

        if (instance != null)
        {
            return;
        }

        GameObject host = new("GlobalPauseMenu");
        instance = host.AddComponent<GlobalPauseMenu>();
    }

    private CanvasGroup rootGroup;
    private GameObject optionsPanel;
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private Slider speedSlider;
    private TMP_Text controlsValueText;
    private TMP_Text qualityValueText;
    private FirstPersonMovement movement;
    private FirstPersonLook look;
    private Rigidbody playerRigidbody;
    private RigidbodyConstraints originalConstraints;
    private bool hasOriginalConstraints;
    private bool isPaused;
    private bool isBusy;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
        ApplySavedSettings();
        SetVisible(false);
    }

    private void Update()
    {
        if (isBusy || string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        SetVisible(false);
        Time.timeScale = 1f;
        isPaused = false;
        SetPlayerInputEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Pause()
    {
        CachePlayerReferences();
        SetPlayerInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        optionsPanel.SetActive(false);
        SetVisible(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ShowOptions()
    {
        optionsPanel.SetActive(true);
    }

    public void HideOptions()
    {
        optionsPanel.SetActive(false);
    }

    public void ExitToMainMenu()
    {
        if (!isBusy)
        {
            StartCoroutine(ExitRoutine());
        }
    }

    private IEnumerator ExitRoutine()
    {
        isBusy = true;
        Time.timeScale = 1f;
        SetPlayerInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayerPrefs.SetInt(OfficeDissolveTransition.PendingOfficeRebuildPref, 1);
        PlayerPrefs.Save();

        OfficeDissolveTransition sceneTransition = FindObjectOfType<OfficeDissolveTransition>();
        if (sceneTransition != null)
        {
            yield return sceneTransition.PlayDissolve();
        }

        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new("GlobalPauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGroup = canvasObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Image backdrop = CreateImage("Backdrop", canvasRect, new Color(0f, 0f, 0f, 0.72f));
        Stretch(backdrop.rectTransform);

        RectTransform panel = CreatePanel("PausePanel", canvasRect, new Vector2(520f, 560f), new Vector2(0.5f, 0.5f), new Color(0.035f, 0.045f, 0.045f, 0.96f));
        AddOutline(panel.gameObject, new Color(0.3f, 0.8f, 0.72f, 0.35f));

        TMP_Text title = CreateText("Title", panel, "PAUSA", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        SetPoint(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(440f, 64f));

        CreateButton(panel, "REANUDAR", new Vector2(0f, -150f), Resume);
        CreateButton(panel, "OPCIONES", new Vector2(0f, -230f), ShowOptions);
        CreateButton(panel, "SALIR", new Vector2(0f, -310f), ExitToMainMenu);

        optionsPanel = CreatePanel("OptionsPanel", canvasRect, new Vector2(620f, 620f), new Vector2(1f, 0.5f), new Color(0.025f, 0.035f, 0.035f, 0.98f)).gameObject;
        RectTransform optionsRect = optionsPanel.GetComponent<RectTransform>();
        optionsRect.anchoredPosition = new Vector2(-360f, 0f);
        AddOutline(optionsPanel, new Color(0.3f, 0.8f, 0.72f, 0.28f));

        TMP_Text optionsTitle = CreateText("OptionsTitle", optionsRect, "OPCIONES", 34, FontStyles.Bold, TextAlignmentOptions.Center);
        SetPoint(optionsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(520f, 52f));

        volumeSlider = CreateLabeledSlider(optionsRect, "VOLUMEN", new Vector2(0f, -130f), OnVolumeChanged);
        sensitivitySlider = CreateLabeledSlider(optionsRect, "MOUSE", new Vector2(0f, -220f), OnSensitivityChanged);
        speedSlider = CreateLabeledSlider(optionsRect, "VELOCIDAD", new Vector2(0f, -310f), OnSpeedChanged);
        CreateControlsSelector(optionsRect, new Vector2(0f, -400f));
        CreateQualitySelector(optionsRect, new Vector2(0f, -480f));
        CreateButton(optionsRect, "VOLVER", new Vector2(0f, -525f), HideOptions);
    }

    private void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(PrefMasterVolume, AudioListener.volume);
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
        }

        CachePlayerReferences();
        float sensitivity = PlayerPrefs.GetFloat(PrefLookSensitivity, look != null ? look.sensitivity : 2f);
        float speed = PlayerPrefs.GetFloat(PrefMoveSpeed, movement != null ? movement.speed : 5f);
        ApplySensitivity(sensitivity);
        ApplyMoveSpeed(speed);

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = Mathf.InverseLerp(0.25f, 8f, sensitivity);
        }

        if (speedSlider != null)
        {
            speedSlider.value = Mathf.InverseLerp(2f, 12f, speed);
        }
    }

    private void CachePlayerReferences()
    {
        if (movement == null)
        {
            movement = FindObjectOfType<FirstPersonMovement>();
        }

        if (look == null)
        {
            look = FindObjectOfType<FirstPersonLook>();
        }

        if (playerRigidbody == null && movement != null)
        {
            playerRigidbody = movement.GetComponent<Rigidbody>();
        }

        if (playerRigidbody != null && !hasOriginalConstraints)
        {
            originalConstraints = playerRigidbody.constraints;
            hasOriginalConstraints = true;
        }
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        CachePlayerReferences();
        if (movement != null) movement.enabled = enabled;
        if (look != null) look.enabled = enabled;
        if (playerRigidbody != null)
        {
            playerRigidbody.constraints = enabled && hasOriginalConstraints ? originalConstraints : RigidbodyConstraints.FreezeAll;
        }
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefMasterVolume, AudioListener.volume);
        PlayerPrefs.Save();
    }

    private void OnSensitivityChanged(float value)
    {
        float sensitivity = Mathf.Lerp(0.25f, 8f, value);
        ApplySensitivity(sensitivity);
        PlayerPrefs.SetFloat(PrefLookSensitivity, sensitivity);
        PlayerPrefs.Save();
    }

    private void ApplySensitivity(float sensitivity)
    {
        CachePlayerReferences();
        if (look != null)
        {
            look.sensitivity = sensitivity;
        }
    }

    private void OnSpeedChanged(float value)
    {
        float speed = Mathf.Lerp(2f, 12f, value);
        ApplyMoveSpeed(speed);
        PlayerPrefs.SetFloat(PrefMoveSpeed, speed);
        PlayerPrefs.Save();
    }

    private void ApplyMoveSpeed(float speed)
    {
        CachePlayerReferences();
        if (movement != null)
        {
            movement.speed = speed;
            movement.runSpeed = Mathf.Max(movement.runSpeed, speed * 1.45f);
        }
    }

    private void ToggleControlScheme()
    {
        CachePlayerReferences();
        bool useArrowMovement = movement != null && !movement.useArrowMovement;
        if (movement != null)
        {
            movement.useArrowMovement = useArrowMovement;
        }

        PlayerPrefs.SetInt(FirstPersonMovement.PrefControlScheme, useArrowMovement ? 1 : 0);
        PlayerPrefs.Save();
        RefreshControlsLabel();
    }

    private void OnQualityChanged(int index)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
        {
            return;
        }

        QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0, QualitySettings.names.Length - 1), true);
        RefreshQualityLabel();
    }

    private void SetVisible(bool visible)
    {
        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.interactable = visible;
        rootGroup.blocksRaycasts = visible;
        rootGroup.gameObject.SetActive(visible);
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreatePanel(label + "Button", parent, new Vector2(360f, 56f), new Vector2(0.5f, 1f), new Color(0.07f, 0.12f, 0.115f, 1f));
        rect.anchoredPosition = anchoredPosition;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text text = CreateText("Label", rect, label, 24, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private Slider CreateLabeledSlider(RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction<float> action)
    {
        TMP_Text text = CreateText(label + "Label", parent, label, 21, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(text.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-210f, 28f), new Vector2(180f, 34f));

        RectTransform sliderRect = CreatePanel(label + "Slider", parent, new Vector2(360f, 24f), new Vector2(0.5f, 1f), new Color(0.08f, 0.12f, 0.12f, 1f));
        sliderRect.anchoredPosition = anchoredPosition + new Vector2(80f, 12f);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(action);

        RectTransform fill = CreatePanel("Fill", sliderRect, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Color(0.46f, 0.93f, 0.84f, 1f));
        Stretch(fill);
        slider.fillRect = fill;
        slider.targetGraphic = fill.GetComponent<Image>();
        return slider;
    }

    private void CreateQualitySelector(RectTransform parent, Vector2 anchoredPosition)
    {
        TMP_Text label = CreateText("QualityLabel", parent, "GRAFICOS", 21, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(label.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-210f, 26f), new Vector2(180f, 34f));

        RectTransform rect = CreatePanel("QualitySelector", parent, new Vector2(360f, 48f), new Vector2(0.5f, 1f), new Color(0.07f, 0.12f, 0.115f, 1f));
        rect.anchoredPosition = anchoredPosition + new Vector2(80f, 20f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(CycleQuality);

        qualityValueText = CreateText("QualityValue", rect, string.Empty, 21, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(qualityValueText.rectTransform);
        RefreshQualityLabel();
    }

    private void CreateControlsSelector(RectTransform parent, Vector2 anchoredPosition)
    {
        TMP_Text label = CreateText("ControlsLabel", parent, "CONTROLES", 21, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(label.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-210f, 26f), new Vector2(180f, 34f));

        RectTransform rect = CreatePanel("ControlsSelector", parent, new Vector2(360f, 48f), new Vector2(0.5f, 1f), new Color(0.07f, 0.12f, 0.115f, 1f));
        rect.anchoredPosition = anchoredPosition + new Vector2(80f, 20f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(ToggleControlScheme);

        controlsValueText = CreateText("ControlsValue", rect, string.Empty, 21, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(controlsValueText.rectTransform);
        RefreshControlsLabel();
    }

    private void RefreshControlsLabel()
    {
        if (controlsValueText == null)
        {
            return;
        }

        bool useArrowMovement = PlayerPrefs.GetInt(FirstPersonMovement.PrefControlScheme, 0) == 1;
        if (movement != null)
        {
            useArrowMovement = movement.useArrowMovement;
        }

        controlsValueText.text = useArrowMovement ? "FLECHAS" : "WASD";
    }

    private void CycleQuality()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
        {
            return;
        }

        OnQualityChanged((QualitySettings.GetQualityLevel() + 1) % names.Length);
    }

    private void RefreshQualityLabel()
    {
        if (qualityValueText == null)
        {
            return;
        }

        string[] names = QualitySettings.names;
        qualityValueText.text = names != null && names.Length > 0 ? names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)].ToUpperInvariant() : "N/A";
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 size, Vector2 anchor, Color color)
    {
        Image image = CreateImage(name, parent, color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.76f, 0.96f, 0.9f, 1f);
        return tmp;
    }

    private static void SetPoint(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddOutline(GameObject target, Color color)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
    }
}
