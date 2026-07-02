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
    private GameObject caseSummaryRoot;
    private TMP_Text caseSummaryText;
    private PauseOptionsCategory currentOptionsCategory;
    private Image[] glitchBars;
    private Image[] glitchBlocks;
    private TMP_Text[] glitchTextLines;
    private float glitchBurstTimer;
    private float glitchTextTimer;
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
    public static bool IsPaused => instance != null && instance.isPaused;

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
        if (RuntimeConfirmationDialog.IsOpen)
        {
            return;
        }

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
            if (ArchiveNull.Evidence.EvidenceNotebookUI.IsAnyNotebookOpen ||
                ArchiveNull.Evidence.PhoneEvidenceReader.IsAnyOpen ||
                Keypad.IsAnyOpen)
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
        bool inspecting = InspectObject.IsAnyInspecting;
        SetPlayerInputEnabled(!inspecting);
        Cursor.lockState = inspecting ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = inspecting;
    }

    public void Pause()
    {
        CachePlayerReferences();
        SetPlayerInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        optionsPanel.SetActive(false);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(true);
        RefreshPauseCaseSummary();
        awaitingRebind = false;
        SetVisible(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ShowOptions()
    {
        optionsPanel.SetActive(true);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(false);
        BuildOptionsCategory(currentOptionsCategory);
    }

    public void HideOptions()
    {
        optionsPanel.SetActive(false);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(true);
    }

    public void ExitToMainMenu()
    {
        if (isBusy)
        {
            return;
        }

        RuntimeConfirmationDialog.Show(
            L("VOLVER AL MENU", "RETURN TO MENU"),
            L("Vas a volver a la oficina. Se conservaran evidencias, notas y pizarra guardadas.", "You are returning to the office. Saved evidence, notes and board data will be kept."),
            L("IR A LA OFICINA", "GO TO OFFICE"),
            L("CANCELAR", "CANCEL"),
            () => StartCoroutine(ExitRoutine()));
    }

    private IEnumerator ExitRoutine()
    {
        isBusy = true;
        Time.timeScale = 1f;
        SetPlayerInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameSaveSystem.SaveNow();
        PlayerPrefs.SetInt(OfficeDissolveTransition.PendingOfficeRebuildPref, 1);
        GameSaveSystem.MarkOfficeContext();
        PlayerPrefs.Save();
        SetVisible(false);

        exitFadeOverlay = CreateFadeOverlay();
        AsyncOperation officeLoad = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
        if (officeLoad != null)
        {
            officeLoad.allowSceneActivation = false;
        }

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

        if (officeLoad == null)
        {
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            yield break;
        }

        while (officeLoad.progress < 0.9f)
        {
            yield return null;
        }

        officeLoad.allowSceneActivation = true;
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
        Image backdrop = CreateImage("Backdrop", canvasRect, new Color(0.018f, 0.021f, 0.019f, 0.91f));
        Stretch(backdrop.rectTransform);
        BuildGlitchBackdrop(canvasRect);

        Image topRule = CreateImage("TopRule", canvasRect, new Color(0.66f, 0.62f, 0.48f, 0.5f));
        topRule.rectTransform.anchorMin = new Vector2(0f, 1f);
        topRule.rectTransform.anchorMax = new Vector2(1f, 1f);
        topRule.rectTransform.pivot = new Vector2(0.5f, 1f);
        topRule.rectTransform.anchoredPosition = new Vector2(0f, -102f);
        topRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

        RectTransform leftRail = CreatePanel("PauseLeftRail", canvasRect, new Vector2(600f, 690f), new Vector2(0f, 0.5f), new Color(0f, 0f, 0f, 0f));
        leftRail.anchoredPosition = new Vector2(116f, 6f);

        RectTransform titleBar = CreatePanel("PauseTitleBar", leftRail, new Vector2(560f, 92f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0f));
        titleBar.anchoredPosition = new Vector2(0f, -4f);
        TMP_Text title = CreateText("Title", titleBar, L("SISTEMA", "SYSTEM"), 42, FontStyles.Normal, TextAlignmentOptions.Left);
        Stretch(title.rectTransform);
        title.rectTransform.offsetMin = new Vector2(0f, 8f);
        title.color = new Color(0.9f, 0.87f, 0.75f, 1f);

        TMP_Text section = CreateText("Section", leftRail, L("ARCHIVE: NULL  /  SESION EN PAUSA", "ARCHIVE: NULL  /  SESSION PAUSED"), 16, FontStyles.Normal, TextAlignmentOptions.Left);
        SetPoint(section.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -112f), new Vector2(560f, 30f));
        section.color = new Color(0.58f, 0.61f, 0.55f, 1f);

        CreateLeftButton(leftRail, L("CONTINUAR INVESTIGACION", "CONTINUE INVESTIGATION"), new Vector2(0f, -190f), Resume);
        CreateLeftButton(leftRail, L("CONFIGURACION", "SETTINGS"), new Vector2(0f, -274f), ShowOptions);
        CreateLeftButton(leftRail, L("VOLVER A LA OFICINA", "RETURN TO OFFICE"), new Vector2(0f, -358f), ExitToMainMenu);

        caseSummaryRoot = CreatePanel("CaseSummary", canvasRect, new Vector2(680f, 430f), new Vector2(1f, 0.5f), new Color(0.025f, 0.028f, 0.025f, 0.58f)).gameObject;
        RectTransform caseRect = caseSummaryRoot.GetComponent<RectTransform>();
        caseRect.anchoredPosition = new Vector2(-128f, 8f);
        AddOutline(caseSummaryRoot, new Color(0.58f, 0.55f, 0.42f, 0.3f));
        TMP_Text caseHeader = CreateText("CaseHeader", caseRect, L("EXPEDIENTE ACTIVO", "ACTIVE CASE"), 17, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(caseHeader.rectTransform, new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(500f, 28f));
        caseHeader.color = new Color(0.65f, 0.61f, 0.46f, 1f);
        caseSummaryText = CreateText("CaseSummaryText", caseRect, string.Empty, 23, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Stretch(caseSummaryText.rectTransform);
        caseSummaryText.rectTransform.offsetMin = new Vector2(34f, 34f);
        caseSummaryText.rectTransform.offsetMax = new Vector2(-34f, -82f);
        caseSummaryText.color = new Color(0.86f, 0.86f, 0.79f, 1f);

        TMP_Text footer = CreateText("Footer", canvasRect, L("ESC  CONTINUAR     CLICK  SELECCIONAR", "ESC  CONTINUE     CLICK  SELECT"), 15, FontStyles.Normal, TextAlignmentOptions.Left);
        SetPoint(footer.rectTransform, new Vector2(0f, 0f), new Vector2(116f, 42f), new Vector2(760f, 30f));
        footer.color = new Color(0.52f, 0.54f, 0.49f, 1f);

        optionsPanel = CreatePanel("OptionsPanel", canvasRect, new Vector2(930f, 660f), new Vector2(1f, 0.5f), new Color(0.022f, 0.026f, 0.023f, 0.97f)).gameObject;
        RectTransform optionsRect = optionsPanel.GetComponent<RectTransform>();
        optionsRect.anchoredPosition = new Vector2(-96f, 0f);
        AddOutline(optionsPanel, new Color(0.62f, 0.58f, 0.43f, 0.32f));

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

    private void RefreshPauseCaseSummary()
    {
        if (caseSummaryText == null)
        {
            return;
        }

        int evidenceCount = ArchiveNull.Evidence.EvidenceInventory.Instance.GetAllEvidence().Count;
        string sceneName = SceneManager.GetActiveScene().name;
        caseSummaryText.text = L(
            $"LA LLAVE POR DENTRO\n\nUBICACION  {sceneName.ToUpperInvariant()}\nEVIDENCIAS REGISTRADAS  {evidenceCount:00}\n\nOBJETIVO ACTUAL\nDocumentar la escena, contrastar los registros y volver a la oficina cuando exista una hipotesis sostenible.",
            $"THE KEY FROM INSIDE\n\nLOCATION  {sceneName.ToUpperInvariant()}\nREGISTERED EVIDENCE  {evidenceCount:00}\n\nCURRENT OBJECTIVE\nDocument the scene, compare records, and return to the office once a defensible hypothesis exists.");
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

        GraphicsSettingsManager.ApplySaved();

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
        RectTransform rect = CreatePanel(label + "Button", parent, new Vector2(560f, 62f), new Vector2(0f, 1f), new Color(0.055f, 0.057f, 0.049f, 0.72f));
        rect.anchoredPosition = anchoredPosition;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text bullet = CreateText("Bullet", rect, "|", 22, FontStyles.Bold, TextAlignmentOptions.Left);
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
                CreateValueButton(optionsContentRoot, L("AYUDAS CONTEXTUALES", "CONTEXT HELP"), GetHelpLabel(), new Vector2(0f, -136f), ToggleContextHelp);
                CreateValueButton(optionsContentRoot, L("TEXTOS DE ACCIONES", "ACTION MESSAGES"), GetActionFeedbackLabel(), new Vector2(0f, -214f), ToggleActionFeedback);
                CreateValueButton(optionsContentRoot, L("REINICIAR AYUDAS", "RESET HELP"), L("REINICIAR", "RESET"), new Vector2(0f, -292f), ResetContextHelp);
                CreateInfoLine(optionsContentRoot, L("Los subtitulos narrativos permanecen activos aunque ocultes los textos de acciones.", "Narrative subtitles remain active when action messages are hidden."), new Vector2(0f, -372f));
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
                CreateCompactValueButton(optionsContentRoot, L("PRESET", "PRESET"), GetQualityLabel(), new Vector2(-205f, -54f), CycleQuality);
                CreateCompactValueButton(optionsContentRoot, L("SOMBRAS", "SHADOWS"), GraphicsSettingsManager.ShadowLabel(IsSpanish()), new Vector2(205f, -54f), ChangeShadows);
                CreateCompactValueButton(optionsContentRoot, L("TEXTURAS", "TEXTURES"), GraphicsSettingsManager.TextureLabel(IsSpanish()), new Vector2(-205f, -146f), ChangeTextures);
                CreateCompactValueButton(optionsContentRoot, L("ANTIALIASING", "ANTI-ALIASING"), GraphicsSettingsManager.AntiAliasingLabel(), new Vector2(205f, -146f), ChangeAntiAliasing);
                CreateCompactValueButton(optionsContentRoot, L("DISTANCIA SOMBRAS", "SHADOW DISTANCE"), GraphicsSettingsManager.ShadowDistanceLabel(), new Vector2(-205f, -238f), ChangeShadowDistance);
                CreateCompactValueButton(optionsContentRoot, L("ESCALA DE RENDER", "RENDER SCALE"), GraphicsSettingsManager.RenderScaleLabel(), new Vector2(205f, -238f), ChangeRenderScale);
                CreateCompactValueButton(optionsContentRoot, "VSYNC", GraphicsSettingsManager.VSyncLabel(), new Vector2(-205f, -330f), ChangeVSync);
                CreateCompactValueButton(optionsContentRoot, L("LIMITE FPS", "FPS LIMIT"), GraphicsSettingsManager.FpsLabel(), new Vector2(205f, -330f), ChangeFpsLimit);
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

    private void CreateCompactValueButton(RectTransform parent, string label, string value, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreatePanel(label + "CompactValue", parent, new Vector2(380f, 72f), new Vector2(0.5f, 1f), new Color(0.045f, 0.085f, 0.078f, 1f));
        rect.anchoredPosition = anchoredPosition;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text labelText = CreateText("Label", rect, label, 15, FontStyles.Bold, TextAlignmentOptions.Left);
        Stretch(labelText.rectTransform);
        labelText.rectTransform.offsetMin = new Vector2(16f, 36f);
        labelText.rectTransform.offsetMax = new Vector2(-16f, -8f);
        labelText.color = new Color(0.55f, 0.78f, 0.73f, 1f);

        TMP_Text valueText = CreateText("Value", rect, value, 20, FontStyles.Bold, TextAlignmentOptions.Left);
        Stretch(valueText.rectTransform);
        valueText.rectTransform.offsetMin = new Vector2(16f, 8f);
        valueText.rectTransform.offsetMax = new Vector2(-16f, -32f);
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

    private static string GetHelpLabel()
    {
        return PlayerAssistanceSettings.HelpEnabled ? L("ACTIVADAS", "ENABLED") : L("DESACTIVADAS", "DISABLED");
    }

    private void ToggleContextHelp()
    {
        PlayerAssistanceSettings.HelpEnabled = !PlayerAssistanceSettings.HelpEnabled;
        if (PlayerAssistanceSettings.HelpEnabled)
        {
            CrimeSceneTutorial.EnsureForCurrentScene();
        }
        BuildOptionsCategory(PauseOptionsCategory.General);
    }

    private static string GetActionFeedbackLabel()
    {
        return PlayerAssistanceSettings.ActionFeedbackEnabled ? L("ACTIVADOS", "ENABLED") : L("DESACTIVADOS", "DISABLED");
    }

    private void ToggleActionFeedback()
    {
        PlayerAssistanceSettings.ActionFeedbackEnabled = !PlayerAssistanceSettings.ActionFeedbackEnabled;
        BuildOptionsCategory(PauseOptionsCategory.General);
    }

    private void ResetContextHelp()
    {
        PlayerAssistanceSettings.ResetHelpProgress();
        PlayerAssistanceSettings.HelpEnabled = true;
        CrimeSceneTutorial.EnsureForCurrentScene();
        BuildOptionsCategory(PauseOptionsCategory.General);
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
        GraphicsSettingsManager.CyclePreset();
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

        qualityValueText.text = GetQualityLabel();
    }

    private static string GetQualityLabel()
    {
        return GraphicsSettingsManager.PresetLabel(IsSpanish());
    }

    private void ChangeShadows() { GraphicsSettingsManager.CycleShadows(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeTextures() { GraphicsSettingsManager.CycleTextures(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeAntiAliasing() { GraphicsSettingsManager.CycleAntiAliasing(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeShadowDistance() { GraphicsSettingsManager.CycleShadowDistance(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeRenderScale() { GraphicsSettingsManager.CycleRenderScale(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeVSync() { GraphicsSettingsManager.ToggleVSync(); BuildOptionsCategory(PauseOptionsCategory.Video); }
    private void ChangeFpsLimit() { GraphicsSettingsManager.CycleFps(); BuildOptionsCategory(PauseOptionsCategory.Video); }

    private void BuildGlitchBackdrop(RectTransform parent)
    {
        glitchBars = new Image[12];
        for (int i = 0; i < glitchBars.Length; i++)
        {
            Image bar = CreateImage("GlitchBar" + i, parent, new Color(0.68f, 0.63f, 0.46f, 0.025f));
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Random.Range(-220f, 220f), Random.Range(1f, 16f));
            rect.anchoredPosition = new Vector2(Random.Range(-180f, 180f), Random.Range(0f, 1080f));
            glitchBars[i] = bar;
        }

        glitchBlocks = new Image[8];
        for (int i = 0; i < glitchBlocks.Length; i++)
        {
            Image block = CreateImage("GlitchBlock" + i, parent, new Color(0.1f, 0.95f, 0.84f, 0f));
            RectTransform rect = block.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(Random.Range(48f, 260f), Random.Range(8f, 42f));
            rect.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
            glitchBlocks[i] = block;
        }

        glitchTextLines = new TMP_Text[5];
        for (int i = 0; i < glitchTextLines.Length; i++)
        {
            TMP_Text line = CreateText("GlitchText" + i, parent, string.Empty, Random.Range(13f, 22f), FontStyles.Bold, TextAlignmentOptions.Left);
            line.color = new Color(0.48f, 1f, 0.9f, 0f);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(Random.Range(80f, 1500f), Random.Range(80f, 1000f));
            rect.sizeDelta = new Vector2(420f, 30f);
            glitchTextLines[i] = line;
        }
    }

    private void UpdateGlitchBackdrop()
    {
        if (glitchBars == null)
        {
            return;
        }

        glitchBurstTimer -= Time.unscaledDeltaTime;
        bool burst = glitchBurstTimer <= 0f;
        if (burst)
        {
            glitchBurstTimer = Random.Range(0.035f, 0.16f);
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
            position.y += (36f + i * 5f) * Time.unscaledDeltaTime;
            if (position.y > 1120f)
            {
                position.y = -40f;
                position.x = Random.Range(-260f, 260f);
            }

            if (burst || Random.value < 0.035f)
            {
                position.x = Random.Range(-360f, 360f);
                rect.sizeDelta = new Vector2(Random.Range(-280f, 320f), Random.Range(1f, 22f));
            }

            rect.anchoredPosition = position;
            Color color = bar.color;
            color.r = Random.value < 0.18f ? 0.9f : 0.2f;
            color.g = Random.value < 0.12f ? 0.25f : 0.95f;
            color.b = Random.value < 0.2f ? 1f : 0.84f;
            color.a = burst || Random.value < 0.22f ? Random.Range(0.035f, 0.22f) : Mathf.Lerp(color.a, 0.035f, Time.unscaledDeltaTime * 10f);
            bar.color = color;
        }

        UpdateGlitchBlocks(burst);
        UpdateGlitchText(burst);
    }

    private void UpdateGlitchBlocks(bool burst)
    {
        if (glitchBlocks == null)
        {
            return;
        }

        for (int i = 0; i < glitchBlocks.Length; i++)
        {
            Image block = glitchBlocks[i];
            if (block == null)
            {
                continue;
            }

            RectTransform rect = block.rectTransform;
            if (burst || Random.value < 0.08f)
            {
                rect.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
                rect.sizeDelta = new Vector2(Random.Range(34f, 360f), Random.Range(6f, 54f));
            }

            Color color = block.color;
            color.r = Random.value < 0.5f ? 0.1f : 0.95f;
            color.g = Random.value < 0.35f ? 0.18f : 0.9f;
            color.b = Random.value < 0.5f ? 0.95f : 0.18f;
            color.a = burst && Random.value < 0.55f ? Random.Range(0.035f, 0.16f) : Mathf.Lerp(color.a, 0f, Time.unscaledDeltaTime * 18f);
            block.color = color;
        }
    }

    private void UpdateGlitchText(bool burst)
    {
        if (glitchTextLines == null)
        {
            return;
        }

        glitchTextTimer -= Time.unscaledDeltaTime;
        if (!burst && glitchTextTimer > 0f)
        {
            FadeGlitchText();
            return;
        }

        glitchTextTimer = Random.Range(0.05f, 0.22f);
        const string chars = "01#%/[]{}ARCHIVE_NULL_MEM";
        for (int i = 0; i < glitchTextLines.Length; i++)
        {
            TMP_Text line = glitchTextLines[i];
            if (line == null)
            {
                continue;
            }

            int length = Random.Range(12, 34);
            System.Text.StringBuilder builder = new(length);
            for (int c = 0; c < length; c++)
            {
                builder.Append(chars[Random.Range(0, chars.Length)]);
            }

            line.text = builder.ToString();
            line.rectTransform.anchoredPosition = new Vector2(Random.Range(80f, 1500f), Random.Range(80f, 1000f));
            Color color = line.color;
            color.a = Random.Range(0.04f, 0.18f);
            line.color = color;
        }
    }

    private void FadeGlitchText()
    {
        for (int i = 0; i < glitchTextLines.Length; i++)
        {
            TMP_Text line = glitchTextLines[i];
            if (line == null)
            {
                continue;
            }

            Color color = line.color;
            color.a = Mathf.Lerp(color.a, 0f, Time.unscaledDeltaTime * 10f);
            line.color = color;
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
