using System.Collections;
using System.Collections.Generic;
using ArchiveNull.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Provides the shared pause overlay and settings UI for playable scenes.
/// </summary>
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
    private readonly List<Canvas> suppressedCanvases = new();
    private GameInputAction pendingRebindAction;
    public static bool IsPaused => instance != null && instance.isPaused;

    public static void OpenSettingsRuntime()
    {
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
        {
            FindAnyObjectByType<ModernOfficeDesktop>()?.OpenSettings();
            return;
        }
        if (instance == null) instance = new GameObject("GlobalPauseMenu").AddComponent<GlobalPauseMenu>();
        if (!instance.isPaused) instance.Pause();
        instance.ShowOptions();
    }

    private void Awake()
    {
        if(SceneManager.GetActiveScene().name==MainMenuSceneName) { enabled=false; Destroy(gameObject); return; }
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
        ApplySavedSettings();
        SetVisible(false);
        RestoreGameplayCanvases();
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

        if (isBusy || (!isPaused && string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (isPaused)
        {
            UpdateGlitchBackdrop();
        }

        if (GlobalInputBindings.WasPressed(GameInputAction.Pause))
        {
            if (ArchiveNull.Evidence.TestingToolsController.CancelledThisFrame || ArchiveNull.Evidence.TestingToolsController.IsPlacing || MemoryLoadingScreen.IsBusy) return;
            if (ArchiveNull.Evidence.EvidenceNotebookUI.IsAnyNotebookOpen || ArchiveNull.Evidence.EvidenceNotebookUI.ClosedThisFrame ||
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
        RestoreGameplayCanvases();
        Time.timeScale = 1f;
        isPaused = false;
        bool inspecting = InspectObject.IsAnyInspecting || CaseFileReader.IsAnyCaseFileOpen || (FindAnyObjectByType<ModernOfficeDesktop>() is ModernOfficeDesktop desktop && desktop.IsOpen);
        SetPlayerInputEnabled(!InspectObject.IsAnyInspecting);
        Cursor.lockState = inspecting ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = inspecting;
    }

    public void Pause()
    {
        if(SceneManager.GetActiveScene().name==MainMenuSceneName) return;
        CachePlayerReferences();
        SetPlayerInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        optionsPanel.SetActive(false);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(false);
        pauseActions.SetActive(true);
        RefreshPauseCaseSummary();
        awaitingRebind = false;
        pauseBackdrop.Capture(Camera.main);
        SetVisible(true);
        if(UnityEngine.EventSystems.EventSystem.current!=null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        SuppressGameplayCanvases();
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ShowOptions()
    {
        pauseActions.SetActive(false);
        optionsPanel.SetActive(true);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(false);
        BuildOptionsCategory(currentOptionsCategory);
    }

    public void HideOptions()
    {
        optionsPanel.SetActive(false);
        if (caseSummaryRoot != null) caseSummaryRoot.SetActive(false);
        pauseActions.SetActive(true);
    }

    public void ExitToMainMenu()
    {
        if (isBusy)
        {
            return;
        }

        Time.timeScale = 1f;
        RuntimeConfirmationDialog.Show(
            L("VOLVER AL MENÚ", "RETURN TO MENU"),
            L("Va a volver a la oficina. Se conservarán las evidencias, notas y conexiones guardadas.", "You are returning to the office. Saved evidence, notes and board data will be kept."),
            L("IR A LA OFICINA", "GO TO OFFICE"),
            L("CANCELAR", "CANCEL"),
            () => StartCoroutine(ExitRoutine()),
            () => Time.timeScale = 0f);
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

        OfficeDissolveTransition transition = FindAnyObjectByType<OfficeDissolveTransition>();
        MemoryLoadingScreen.Load(MainMenuSceneName,transition);
        yield break;
    }
    private GameObject pauseActions;
    private PauseBackdrop pauseBackdrop;
    private Button resumeButton;
    private void BuildUi()
    {
        GameObject host=new("GlobalPauseCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); host.transform.SetParent(transform,false);
        Canvas canvas=host.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=25000;
        CanvasScaler scaler=host.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        rootGroup=host.AddComponent<CanvasGroup>(); RectTransform root=(RectTransform)host.transform;
        var blurred=new GameObject("Paused world",typeof(RectTransform),typeof(RawImage),typeof(PauseBackdrop)); blurred.transform.SetParent(root,false); Stretch((RectTransform)blurred.transform); blurred.GetComponent<RawImage>().raycastTarget=false; pauseBackdrop=blurred.GetComponent<PauseBackdrop>();
        Image shade=CreateImage("Soft shade",root,new Color(.014f,.011f,.006f,.68f)); Stretch(shade.rectTransform);
        RectTransform top=CreatePanel("Navigation",root,new Vector2(1040,88),new Vector2(.5f,1),Color.clear); top.anchoredPosition=new Vector2(0,-18);
        ThinButton(top,L("PAUSA","PAUSE"),new Vector2(-330,0),new Vector2(270,76),ShowPauseActions,20);
        ThinButton(top,L("EXPEDIENTE","CASE FILE"),Vector2.zero,new Vector2(270,76),ShowCaseSummary,20);
        ThinButton(top,L("OPCIONES","OPTIONS"),new Vector2(330,0),new Vector2(270,76),ShowOptions,20);
        Image rule=CreateImage("Navigation baseline",root,new Color(.72f,.62f,.37f,.15f)); rule.rectTransform.anchorMin=new Vector2(0,1); rule.rectTransform.anchorMax=Vector2.one; rule.rectTransform.sizeDelta=new Vector2(0,1); rule.rectTransform.anchoredPosition=new Vector2(0,-108);
        pauseActions=CreatePanel("Pause actions",root,new Vector2(760,430),new Vector2(.5f,.5f),Color.clear).gameObject;
        var actions=(RectTransform)pauseActions.transform; actions.anchoredPosition=new Vector2(0,-36);
        resumeButton=ThinButton(actions,L("Continuar investigación","Resume investigation"),new Vector2(0,132),new Vector2(760,104),Resume,30);
        ThinButton(actions,L("Configuración","Settings"),new Vector2(0,0),new Vector2(760,104),ShowOptions,30);
        ThinButton(actions,L("Salir de la memoria","Leave memory"),new Vector2(0,-132),new Vector2(760,104),ExitToMainMenu,30);
        caseSummaryRoot=CreatePanel("Case overview",root,new Vector2(980,510),new Vector2(.5f,.5f),Color.clear).gameObject;
        caseSummaryText=CreateText("Case details",(RectTransform)caseSummaryRoot.transform,"",27,FontStyles.Normal,TextAlignmentOptions.TopLeft); Stretch(caseSummaryText.rectTransform); caseSummaryText.color=new Color(.89f,.88f,.81f);
        TMP_Text footer=CreateText("Footer",root,L("ESC  Continuar      ·      Clic  Seleccionar","ESC  Resume      ·      Click  Select"),18,FontStyles.Normal,TextAlignmentOptions.Right);
        SetPoint(footer.rectTransform,new Vector2(1,0),new Vector2(-380,46),new Vector2(660,36)); footer.color=new Color(.77f,.76f,.68f);
        TMP_Text brand=CreateText("Archive",root,"ARCHIVE: NULL",15,FontStyles.Normal,TextAlignmentOptions.Left); SetPoint(brand.rectTransform,new Vector2(0,0),new Vector2(230,46),new Vector2(340,36)); brand.color=new Color(.55f,.52f,.41f);
        optionsPanel=CreatePanel("Options content",root,new Vector2(1020,690),new Vector2(.5f,.5f),new Color(.015f,.014f,.011f,.42f)).gameObject;
        RectTransform optionsRect=(RectTransform)optionsPanel.transform;
        optionsHeaderText=CreateText("OptionsTitle",optionsRect,L("Configuración","Settings"),31,FontStyles.Normal,TextAlignmentOptions.Left);
        SetPoint(optionsHeaderText.rectTransform,new Vector2(.5f,1),new Vector2(0,-42),new Vector2(860,48));
        RectTransform tabs=CreatePanel("Settings navigation",optionsRect,new Vector2(860,45),new Vector2(.5f,1),Color.clear); tabs.anchoredPosition=new Vector2(0,-92);
        CreateCategoryButton(tabs,PauseOptionsCategory.General,L("GENERAL","GENERAL"),0);
        CreateCategoryButton(tabs,PauseOptionsCategory.Language,L("IDIOMA","LANGUAGE"),1);
        CreateCategoryButton(tabs,PauseOptionsCategory.Sound,L("SONIDO","SOUND"),2);
        CreateCategoryButton(tabs,PauseOptionsCategory.Video,L("IMAGEN","PICTURE"),3);
        CreateCategoryButton(tabs,PauseOptionsCategory.Controls,L("CONTROLES","CONTROLS"),4);
        optionsContentRoot=CreatePanel("Settings fields",optionsRect,new Vector2(840,450),new Vector2(.5f,1),Color.clear); optionsContentRoot.anchoredPosition=new Vector2(0,-145);
        ThinButton(optionsRect,L("Volver","Back"),new Vector2(0,-290),new Vector2(360,54),HideOptions,22);
        currentOptionsCategory=PauseOptionsCategory.General; BuildOptionsCategory(currentOptionsCategory);
    }
    private Button ThinButton(RectTransform parent,string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,float fontSize)
    {
        RectTransform rect=CreatePanel(label,parent,size,new Vector2(.5f,.5f),Color.clear); rect.anchoredPosition=position;
        Button button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>(); button.transition=Selectable.Transition.None; button.onClick.AddListener(action);
        TMP_Text text=CreateText("Label",rect,label,fontSize,FontStyles.Normal,TextAlignmentOptions.Center); Stretch(text.rectTransform); text.rectTransform.offsetMin=new Vector2(12,16); text.rectTransform.offsetMax=new Vector2(-12,-8);
        Image line=CreateImage("Selection line",rect,new Color(.89f,.74f,.43f,.13f)); line.rectTransform.anchorMin=Vector2.zero; line.rectTransform.anchorMax=new Vector2(1,0); line.rectTransform.sizeDelta=new Vector2(0,1); line.rectTransform.anchoredPosition=new Vector2(0,3); line.raycastTarget=false;
        var visual=rect.gameObject.AddComponent<PauseMenuAction>(); visual.Label=text; visual.Rule=line; return button;
    }
    private void ShowPauseActions() { optionsPanel.SetActive(false); caseSummaryRoot.SetActive(false); pauseActions.SetActive(true); }
    private void ShowCaseSummary() { optionsPanel.SetActive(false); pauseActions.SetActive(false); caseSummaryRoot.SetActive(true); RefreshPauseCaseSummary(); }
    private void RefreshPauseCaseSummary()
    {
        if (caseSummaryText == null)
        {
            return;
        }

        int evidenceCount = ArchiveNull.Evidence.EvidenceInventory.Instance.GetAllEvidence().Count;
        string sceneName = SceneManager.GetActiveScene().name;
        caseSummaryText.text = L(
            $"LA LLAVE POR DENTRO\n\nUBICACIÓN  {sceneName.ToUpperInvariant()}\nEVIDENCIAS REGISTRADAS  {evidenceCount:00}\n\nOBJETIVO ACTUAL\nDocumentar la escena, contrastar los registros y volver a la oficina cuando exista una hipótesis sostenible.",
            $"THE KEY FROM INSIDE\n\nLOCATION  {sceneName.ToUpperInvariant()}\nREGISTERED EVIDENCE  {evidenceCount:00}\n\nCURRENT OBJECTIVE\nDocument the scene, compare records, and return to the office once a defensible hypothesis exists.");
        caseSummaryText.text = "LA LLAVE POR DENTRO\n\n" + sceneName + " · " + evidenceCount + " evidencias\n\n" +
            ArchiveNull.Evidence.CaseEvidenceCatalog.NextObjective() + "\n\nTAB: libreta y fotografías";
        if (sceneName == "MechanicsLab") caseSummaryText.text = "LABORATORIO DE MECÁNICAS\n\nG: rueda de herramientas\nTrípode: clic para colocar · Q/R para rotar\nE sobre trípode: montar / recuperar\nL: encender montada · P: foto montada\n\nG: herramientas · F: levantar cámara\nTAB: libreta y fotografías";
    }

    private void SuppressGameplayCanvases()
    {
        suppressedCanvases.Clear();
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.enabled || canvas.transform.IsChildOf(transform)) continue;
            suppressedCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    private void RestoreGameplayCanvases()
    {
        for (int i = 0; i < suppressedCanvases.Count; i++)
        {
            if (suppressedCanvases[i] != null) suppressedCanvases[i].enabled = true;
        }
        suppressedCanvases.Clear();
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
            movement = FindAnyObjectByType<FirstPersonMovement>();
        }

        if (look == null)
        {
            look = FindAnyObjectByType<FirstPersonLook>();
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
        RectTransform rect = CreatePanel(label + "Button", parent, new Vector2(360f, 56f), new Vector2(0.5f, 1f), new Color(.10f,.20f,.29f,1));
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
        RectTransform rect = CreatePanel(category + "Tab", parent, new Vector2(160f, 38f), new Vector2(0f, 0.5f), new Color(.08f,.14f,.20f,1));
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
                Slider headBob = CreateOptionSlider(optionsContentRoot, L("MOVIMIENTO BODYCAM", "BODYCAM MOTION"), new Vector2(0f, -372f), value =>
                {
                    PlayerPrefs.SetFloat("archive.headbob.intensity", value);
                    PlayerPrefs.Save();
                });
                headBob.SetValueWithoutNotify(PlayerPrefs.GetFloat("archive.headbob.intensity", 0.65f));
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

        RectTransform fill = CreatePanel("Fill", sliderRect, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Color(.4f,.73f,.97f,1));
        Stretch(fill);
        slider.fillRect = fill;
        slider.targetGraphic = fill.GetComponent<Image>();
        return slider;
    }

    private void CreateValueButton(RectTransform parent, string label, string value, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        TMP_Text text = CreateText(label + "Label", parent, label, 19, FontStyles.Bold, TextAlignmentOptions.Left);
        SetPoint(text.rectTransform, new Vector2(0.5f, 1f), anchoredPosition + new Vector2(-250f, 18f), new Vector2(260f, 30f));

        RectTransform rect = CreatePanel(label + "Value", parent, new Vector2(390f, 38f), new Vector2(0.5f, 1f), new Color(.08f,.14f,.20f,1));
        rect.anchoredPosition = anchoredPosition + new Vector2(148f, 8f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text valueText = CreateText("ValueText", rect, value, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(valueText.rectTransform);
    }

    private void CreateCompactValueButton(RectTransform parent, string label, string value, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreatePanel(label + "CompactValue", parent, new Vector2(380f, 72f), new Vector2(0.5f, 1f), new Color(.08f,.14f,.20f,1));
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

        RectTransform rect = CreatePanel("QualitySelector", parent, new Vector2(360f, 48f), new Vector2(0.5f, 1f), new Color(.10f,.20f,.29f,1));
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

            RectTransform buttonRect = CreatePanel(action + "Rebind", parent, new Vector2(160f, 24f), new Vector2(0.5f, 1f), new Color(.10f,.20f,.29f,1));
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

    private void ChangeShadows()
    {
        GraphicsSettingsManager.CycleShadows();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeTextures()
    {
        GraphicsSettingsManager.CycleTextures();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeAntiAliasing()
    {
        GraphicsSettingsManager.CycleAntiAliasing();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeShadowDistance()
    {
        GraphicsSettingsManager.CycleShadowDistance();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeRenderScale()
    {
        GraphicsSettingsManager.CycleRenderScale();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeVSync()
    {
        GraphicsSettingsManager.ToggleVSync();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

    private void ChangeFpsLimit()
    {
        GraphicsSettingsManager.CycleFps();
        BuildOptionsCategory(PauseOptionsCategory.Video);
    }

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

        glitchTextLines = new TMP_Text[0];
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
        tmp.color = new Color(.83f,.9f,.97f,1);
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

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
