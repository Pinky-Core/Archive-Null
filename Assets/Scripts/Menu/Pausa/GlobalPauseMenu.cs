using System.Collections;
using ArchiveNull.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GlobalPauseMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string PrefMasterVolume = "global.pause.master.volume";
    private const string PrefEffectsVolume = "global.pause.effects.volume";
    private const string PrefVoiceVolume = "global.pause.voice.volume";
    private const string PrefLookSensitivity = "global.pause.look.sensitivity";
    private const string PrefQuality = "global.pause.quality";
    private const string PrefCrtQuality = "crt.menu.quality.index";
    private const string PrefLanguageIndex = "crt.menu.language.index";

    private static GlobalPauseMenu instance;

    public static float EffectsVolume => PlayerPrefs.GetFloat(PrefEffectsVolume, 1f);
    public static float VoiceVolume => PlayerPrefs.GetFloat(PrefVoiceVolume, 1f);
    private enum PauseOptionsCategory
    {
        General,
        Language,
        Sound,
        Video,
        Controls
    }

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
    private Slider effectsSlider;
    private Slider voiceSlider;
    private Slider sensitivitySlider;
    private TMP_Text qualityValueText;
    private TMP_Text rebindHintText;
    private TMP_Text[] rebindValueTexts;
    private RectTransform optionsContentRoot;
    private TMP_Text optionsHeaderText;
    private PauseOptionsCategory currentOptionsCategory;
    private Image[] glitchBars;
    private CanvasGroup exitFadeOverlay;
    private FirstPersonMovement movement;
    private FirstPersonLook look;
    private Rigidbody playerRigidbody;
    private RigidbodyConstraints originalConstraints;
    private bool hasOriginalConstraints;
    private bool isPaused;
    private bool isBusy;
    private bool awaitingRebind;
    private GameInputAction pendingRebindAction;

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
        if (awaitingRebind)
        {
            CaptureRebindInput();
            UpdateGlitchBackdrop();
            return;
        }

        if (isBusy || string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (isPaused)
        {
            UpdateGlitchBackdrop();
        }

        if (GlobalInputBindings.WasPressed(GameInputAction.Pause))
        {
            if (InspectObject.IsAnyInspecting || ArchiveNull.Evidence.EvidenceNotebookUI.IsAnyNotebookOpen || Keypad.IsAnyOpen)
            {
                return;
            }

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
        awaitingRebind = false;
        SetVisible(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ShowOptions()
    {
        optionsPanel.SetActive(true);
        BuildOptionsCategory(currentOptionsCategory);
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
        SetVisible(false);

        exitFadeOverlay = CreateFadeOverlay();

        OfficeDissolveTransition sceneTransition = FindObjectOfType<OfficeDissolveTransition>();
        bool destroyTemporaryTransition = false;
        if (sceneTransition != null)
        {
            yield return sceneTransition.PlayDissolve();
        }
        else
        {
            GameObject transitionHost = new("RuntimeMemoryDissolveTransition");
            sceneTransition = transitionHost.AddComponent<OfficeDissolveTransition>();
            sceneTransition.DisablePendingReturnRebuildCheck();
            destroyTemporaryTransition = true;
            yield return sceneTransition.PlayDissolve();
        }

        yield return FadeCanvasGroup(exitFadeOverlay, 0f, 1f, 0.45f);

        if (destroyTemporaryTransition && sceneTransition != null)
        {
            Destroy(sceneTransition.gameObject);
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
        BuildGlitchBackdrop(canvasRect);

        RectTransform leftRail = CreatePanel("PauseLeftRail", canvasRect, new Vector2(560f, 560f), new Vector2(0f, 0.5f), new Color(0f, 0f, 0f, 0f));
        leftRail.anchoredPosition = new Vector2(96f, 0f);

        RectTransform titleBar = CreatePanel("PauseTitleBar", leftRail, new Vector2(520f, 58f), new Vector2(0f, 1f), new Color(0.035f, 0.15f, 0.135f, 0.82f));
        titleBar.anchoredPosition = new Vector2(0f, -18f);
        AddOutline(titleBar.gameObject, new Color(0.45f, 1f, 0.9f, 0.35f));
        TMP_Text title = CreateText("Title", titleBar, L("MENU DE PAUSA", "PAUSE MENU"), 31, FontStyles.Bold, TextAlignmentOptions.Left);
        Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(28f, 0f);

        CreateLeftButton(leftRail, L("REANUDAR", "RESUME"), new Vector2(0f, -120f), Resume);
        CreateLeftButton(leftRail, L("OPCIONES", "OPTIONS"), new Vector2(0f, -204f), ShowOptions);
        CreateLeftButton(leftRail, L("SALIR AL MENU", "QUIT TO MENU"), new Vector2(0f, -288f), ExitToMainMenu);

        optionsPanel = CreatePanel("OptionsPanel", canvasRect, new Vector2(930f, 660f), new Vector2(1f, 0.5f), new Color(0.018f, 0.034f, 0.032f, 0.94f)).gameObject;
        RectTransform optionsRect = optionsPanel.GetComponent<RectTransform>();
        optionsRect.anchoredPosition = new Vector2(-96f, 0f);
        AddOutline(optionsPanel, new Color(0.4f, 1f, 0.9f, 0.3f));

        optionsHeaderText = CreateText("OptionsTitle", optionsRect, L("OPCIONES", "OPTIONS"), 31, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(optionsHeaderText.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -42f), new Vector2(320f, 42f));

        RectTransform tabsRoot = CreatePanel("CategoryTabs", optionsRect, new Vector2(860f, 52f), new Vector2(0.5f, 1f), new Color(0f, 0f, 0f, 0f));
        tabsRoot.anchoredPosition = new Vector2(0f, -96f);
        CreateCategoryButton(tabsRoot, PauseOptionsCategory.General, L("GENERAL", "GENERAL"), 0);
        CreateCategoryButton(tabsRoot, PauseOptionsCategory.Language, L("IDIOMA", "LANGUAGE"), 1);
        CreateCategoryButton(tabsRoot, PauseOptionsCategory.Sound, L("SONIDO", "SOUND"), 2);
        CreateCategoryButton(tabsRoot, PauseOptionsCategory.Video, L("VIDEO", "VIDEO"), 3);
        CreateCategoryButton(tabsRoot, PauseOptionsCategory.Controls, L("CONTROLES", "CONTROLS"), 4);

        optionsContentRoot = CreatePanel("OptionsContent", optionsRect, new Vector2(840f, 450f), new Vector2(0.5f, 1f), new Color(0.01f, 0.022f, 0.021f, 0.45f));
        optionsContentRoot.anchoredPosition = new Vector2(0f, -170f);
        AddOutline(optionsContentRoot.gameObject, new Color(0.26f, 0.75f, 0.68f, 0.18f));

        CreateButton(optionsRect, L("CERRAR OPCIONES", "CLOSE OPTIONS"), new Vector2(0f, -604f), HideOptions);
        currentOptionsCategory = PauseOptionsCategory.General;
        BuildOptionsCategory(currentOptionsCategory);
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
        if ((PlayerPrefs.HasKey(PrefQuality) || PlayerPrefs.HasKey(PrefCrtQuality)) && QualitySettings.names != null && QualitySettings.names.Length > 0)
        {
            int savedQuality = PlayerPrefs.HasKey(PrefQuality) ? PlayerPrefs.GetInt(PrefQuality) : PlayerPrefs.GetInt(PrefCrtQuality);
            QualitySettings.SetQualityLevel(Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1), true);
        }

        ApplySensitivity(sensitivity);

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = Mathf.InverseLerp(0.25f, 8f, sensitivity);
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

    private void OnEffectsVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(PrefEffectsVolume, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    private void OnVoiceVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(PrefVoiceVolume, Mathf.Clamp01(value));
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

    private void OnQualityChanged(int index)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
        {
            return;
        }

        QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0, QualitySettings.names.Length - 1), true);
        PlayerPrefs.SetInt(PrefQuality, QualitySettings.GetQualityLevel());
        PlayerPrefs.SetInt(PrefCrtQuality, QualitySettings.GetQualityLevel());
        PlayerPrefs.Save();
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

    private Button CreateLeftButton(RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreatePanel(label + "Button", parent, new Vector2(520f, 58f), new Vector2(0f, 1f), new Color(0.015f, 0.026f, 0.025f, 0.9f));
        rect.anchoredPosition = anchoredPosition;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text bullet = CreateText("Bullet", rect, ">", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(bullet.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));

        TMP_Text text = CreateText("Label", rect, label, 23, FontStyles.Bold, TextAlignmentOptions.Left);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(54f, 0f);
        text.rectTransform.offsetMax = new Vector2(-18f, 0f);
        return button;
    }

    private void CreateCategoryButton(RectTransform parent, PauseOptionsCategory category, string label, int index)
    {
        RectTransform rect = CreatePanel(category + "Tab", parent, new Vector2(160f, 38f), new Vector2(0f, 0.5f), new Color(0.045f, 0.09f, 0.082f, 0.96f));
        rect.anchoredPosition = new Vector2(index * 170f, 0f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => BuildOptionsCategory(category));

        TMP_Text text = CreateText("Label", rect, label, 16, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
    }

    private void BuildOptionsCategory(PauseOptionsCategory category)
    {
        currentOptionsCategory = category;
        awaitingRebind = false;
        if (rebindHintText != null)
        {
            rebindHintText.text = string.Empty;
        }

        if (optionsHeaderText != null)
        {
            optionsHeaderText.text = GetCategoryTitle(category);
        }

        ClearChildren(optionsContentRoot);
        if (optionsContentRoot == null)
        {
            return;
        }

        switch (category)
        {
            case PauseOptionsCategory.General:
                sensitivitySlider = CreateOptionSlider(optionsContentRoot, L("SENSIBILIDAD MOUSE", "MOUSE SENSITIVITY"), new Vector2(0f, -58f), OnSensitivityChanged);
                CreateInfoLine(optionsContentRoot, L("Los cambios se guardan y se aplican al instante.", "Changes are saved and applied instantly."), new Vector2(0f, -146f));
                if (sensitivitySlider != null) sensitivitySlider.value = Mathf.InverseLerp(0.25f, 8f, PlayerPrefs.GetFloat(PrefLookSensitivity, look != null ? look.sensitivity : 2f));
                break;

            case PauseOptionsCategory.Language:
                CreateValueButton(optionsContentRoot, L("IDIOMA", "LANGUAGE"), GetLanguageLabel(), new Vector2(0f, -58f), CycleLanguage);
                CreateInfoLine(optionsContentRoot, L("El idioma se comparte con el monitor de la oficina.", "Language is shared with the office monitor."), new Vector2(0f, -146f));
                break;

            case PauseOptionsCategory.Sound:
                volumeSlider = CreateOptionSlider(optionsContentRoot, L("VOLUMEN MAESTRO", "MASTER VOLUME"), new Vector2(0f, -58f), OnVolumeChanged);
                effectsSlider = CreateOptionSlider(optionsContentRoot, L("EFECTOS", "EFFECTS"), new Vector2(0f, -136f), OnEffectsVolumeChanged);
                voiceSlider = CreateOptionSlider(optionsContentRoot, L("VOCES", "VOICES"), new Vector2(0f, -214f), OnVoiceVolumeChanged);
                if (volumeSlider != null) volumeSlider.value = AudioListener.volume;
                if (effectsSlider != null) effectsSlider.value = PlayerPrefs.GetFloat(PrefEffectsVolume, 1f);
                if (voiceSlider != null) voiceSlider.value = PlayerPrefs.GetFloat(PrefVoiceVolume, 1f);
                break;

            case PauseOptionsCategory.Video:
                CreateValueButton(optionsContentRoot, L("CALIDAD GRAFICA", "GRAPHICS QUALITY"), GetQualityLabel(), new Vector2(0f, -58f), CycleQuality);
                CreateInfoLine(optionsContentRoot, L("La calidad se guarda para el menu principal y las memorias.", "Quality is saved for the main menu and memories."), new Vector2(0f, -146f));
                break;

            case PauseOptionsCategory.Controls:
                CreateControlsSelector(optionsContentRoot, new Vector2(0f, -36f));
                break;
        }
    }

    private Slider CreateOptionSlider(RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction<float> action)
    {
        TMP_Text text = CreateText(label + "Label", parent, label, 19, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(text.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-250f, 18f), new Vector2(260f, 30f));

        RectTransform sliderRect = CreatePanel(label + "Slider", parent, new Vector2(390f, 26f), new Vector2(0.5f, 1f), new Color(0.035f, 0.065f, 0.06f, 1f));
        sliderRect.anchoredPosition = anchoredPosition + new Vector2(148f, 4f);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(action);

        RectTransform fill = CreatePanel("Fill", sliderRect, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Color(0.46f, 0.95f, 0.86f, 1f));
        Stretch(fill);
        slider.fillRect = fill;
        slider.targetGraphic = fill.GetComponent<Image>();
        return slider;
    }

    private void CreateValueButton(RectTransform parent, string label, string value, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        TMP_Text text = CreateText(label + "Label", parent, label, 19, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(text.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-250f, 18f), new Vector2(260f, 30f));

        RectTransform rect = CreatePanel(label + "Value", parent, new Vector2(390f, 38f), new Vector2(0.5f, 1f), new Color(0.045f, 0.085f, 0.078f, 1f));
        rect.anchoredPosition = anchoredPosition + new Vector2(148f, 8f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text valueText = CreateText("ValueText", rect, value, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(valueText.rectTransform);
    }

    private void CreateInfoLine(RectTransform parent, string value, Vector2 anchoredPosition)
    {
        TMP_Text text = CreateText("Info", parent, value, 17, FontStyles.Normal, TextAlignmentOptions.Left);
        text.color = new Color(0.56f, 0.78f, 0.73f, 1f);
        SetPoint(text.rectTransform, new Vector2(0.5f, 1f), anchoredPosition, new Vector2(720f, 44f));
    }

    private string GetCategoryTitle(PauseOptionsCategory category)
    {
        return category switch
        {
            PauseOptionsCategory.General => L("OPCIONES // GENERAL", "OPTIONS // GENERAL"),
            PauseOptionsCategory.Language => L("OPCIONES // IDIOMA", "OPTIONS // LANGUAGE"),
            PauseOptionsCategory.Sound => L("OPCIONES // SONIDO", "OPTIONS // SOUND"),
            PauseOptionsCategory.Video => L("OPCIONES // VIDEO", "OPTIONS // VIDEO"),
            PauseOptionsCategory.Controls => L("OPCIONES // CONTROLES", "OPTIONS // CONTROLS"),
            _ => L("OPCIONES", "OPTIONS")
        };
    }

    private string GetLanguageLabel()
    {
        return IsSpanish() ? "ESPANOL" : "ENGLISH";
    }

    private void CycleLanguage()
    {
        PlayerPrefs.SetInt(PrefLanguageIndex, IsSpanish() ? 1 : 0);
        PlayerPrefs.Save();
        BuildOptionsCategory(PauseOptionsCategory.Language);
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
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
        TMP_Text label = CreateText("QualityLabel", parent, L("GRAFICOS", "GRAPHICS"), 21, FontStyles.Bold, TextAlignmentOptions.Left);
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
        TMP_Text label = CreateText("ControlsLabel", parent, L("CONTROLES", "CONTROLS"), 21, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(label.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-240f, 20f), new Vector2(220f, 34f));

        GameInputAction[] actions =
        {
            GameInputAction.MoveForward,
            GameInputAction.MoveBackward,
            GameInputAction.MoveLeft,
            GameInputAction.MoveRight,
            GameInputAction.Run,
            GameInputAction.Interact,
            GameInputAction.Inspect,
            GameInputAction.Camera,
            GameInputAction.Notebook,
            GameInputAction.Pause
        };

        rebindValueTexts = new TMP_Text[actions.Length];
        for (int i = 0; i < actions.Length; i++)
        {
            GameInputAction action = actions[i];
            float y = -18f - i * 28f;
            TMP_Text actionLabel = CreateText(action + "Label", parent, GlobalInputBindings.GetLabel(action, IsSpanish()), 16, FontStyles.Bold, TextAlignmentOptions.Left);
            SetPoint(actionLabel.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-205f, y), new Vector2(250f, 26f));

            RectTransform buttonRect = CreatePanel(action + "Rebind", parent, new Vector2(160f, 24f), new Vector2(0.5f, 1f), new Color(0.07f, 0.12f, 0.115f, 1f));
            buttonRect.anchoredPosition = anchoredPosition + new Vector2(160f, y);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            button.onClick.AddListener(() => BeginGameRebind(action));

            TMP_Text value = CreateText("Value", buttonRect, string.Empty, 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(value.rectTransform);
            rebindValueTexts[i] = value;
        }

        rebindHintText = CreateText("RebindHint", parent, string.Empty, 15, FontStyles.Normal, TextAlignmentOptions.Center);
        SetPoint(rebindHintText.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(0f, -304f), new Vector2(540f, 26f));
        RefreshControlsLabel();
    }

    private void RefreshControlsLabel()
    {
        if (rebindValueTexts == null)
        {
            return;
        }

        GameInputAction[] actions =
        {
            GameInputAction.MoveForward,
            GameInputAction.MoveBackward,
            GameInputAction.MoveLeft,
            GameInputAction.MoveRight,
            GameInputAction.Run,
            GameInputAction.Interact,
            GameInputAction.Inspect,
            GameInputAction.Camera,
            GameInputAction.Notebook,
            GameInputAction.Pause
        };

        for (int i = 0; i < rebindValueTexts.Length && i < actions.Length; i++)
        {
            if (rebindValueTexts[i] != null)
            {
                rebindValueTexts[i].text = GlobalInputBindings.GetDisplayName(actions[i]);
            }
        }
    }

    private void BeginGameRebind(GameInputAction action)
    {
        awaitingRebind = true;
        pendingRebindAction = action;
        if (rebindHintText != null)
        {
            rebindHintText.text = L("PRESIONA UNA TECLA...", "PRESS A KEY...");
        }
    }

    private void CaptureRebindInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        foreach (KeyControl keyControl in Keyboard.current.allKeys)
        {
            if (!keyControl.wasPressedThisFrame)
            {
                continue;
            }

            Key key = keyControl.keyCode;
            if (key == Key.None)
            {
                return;
            }

            GlobalInputBindings.SetKey(pendingRebindAction, key);
            if (pendingRebindAction == GameInputAction.Inspect)
            {
                GlobalInputBindings.SetKey(GameInputAction.ReleaseInspect, key);
            }
            awaitingRebind = false;
            if (rebindHintText != null)
            {
                rebindHintText.text = string.Empty;
            }

            RefreshControlsLabel();
            return;
        }
    }

    private void CycleQuality()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
        {
            return;
        }

        OnQualityChanged((QualitySettings.GetQualityLevel() + 1) % names.Length);
        if (currentOptionsCategory == PauseOptionsCategory.Video)
        {
            BuildOptionsCategory(PauseOptionsCategory.Video);
        }
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

    private static string GetQualityLabel()
    {
        string[] names = QualitySettings.names;
        return names != null && names.Length > 0 ? names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)].ToUpperInvariant() : "N/A";
    }

    private void BuildGlitchBackdrop(RectTransform parent)
    {
        glitchBars = new Image[14];
        for (int i = 0; i < glitchBars.Length; i++)
        {
            Image bar = CreateImage("GlitchBar" + i, parent, new Color(0.3f, 0.95f, 0.85f, 0.04f));
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, Random.Range(2f, 10f));
            rect.anchoredPosition = new Vector2(Random.Range(-80f, 80f), Random.Range(0f, 1080f));
            glitchBars[i] = bar;
        }
    }

    private void UpdateGlitchBackdrop()
    {
        if (glitchBars == null)
        {
            return;
        }

        for (int i = 0; i < glitchBars.Length; i++)
        {
            Image bar = glitchBars[i];
            if (bar == null)
            {
                continue;
            }

            RectTransform rect = bar.rectTransform;
            Vector2 position = rect.anchoredPosition;
            position.y += (20f + i * 3f) * Time.unscaledDeltaTime;
            if (position.y > 1120f)
            {
                position.y = -40f;
                position.x = Random.Range(-120f, 120f);
            }

            if (Random.value < 0.04f)
            {
                position.x = Random.Range(-140f, 140f);
                rect.sizeDelta = new Vector2(0f, Random.Range(2f, 14f));
            }

            rect.anchoredPosition = position;
            Color color = bar.color;
            color.a = Random.value < 0.2f ? Random.Range(0.04f, 0.14f) : Mathf.Lerp(color.a, 0.045f, Time.unscaledDeltaTime * 8f);
            bar.color = color;
        }
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

    private static bool IsSpanish()
    {
        return PlayerPrefs.GetInt("crt.menu.language.index", 0) == 0;
    }

    private static string L(string spanish, string english)
    {
        return IsSpanish() ? spanish : english;
    }

    private static CanvasGroup CreateFadeOverlay()
    {
        GameObject canvasObject = new("PauseExitFade", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        Stretch(image.rectTransform);

        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = true;
        return group;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        group.alpha = from;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }
}
