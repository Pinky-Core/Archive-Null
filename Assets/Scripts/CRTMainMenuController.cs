using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

namespace ArchiveNull.UI
{
    public sealed class CRTMainMenuController : MonoBehaviour
    {
        private const string IdleTitle = "Archive: {null}";
        private const string BootPrompt = "PRESS ENTER OR CLICK TO INITIALIZE";
        private const string PrefAudioEnabled = "crt.menu.audio.enabled";
        private const string PrefMasterVolume = "crt.menu.audio.master";
        private const string PrefInterfaceVolume = "crt.menu.audio.interface";
        private const string PrefSystemVolume = "crt.menu.audio.system";
        private const string PrefEffectsVolume = "crt.menu.audio.effects";
        private const string PrefSubtitlesEnabled = "crt.menu.subtitles.enabled";
        private const string PrefLanguageIndex = "crt.menu.language.index";
        private const string PrefQualityIndex = "crt.menu.quality.index";
        private const string PrefScanlinesEnabled = "crt.menu.scanlines.enabled";
        private const string PrefChromaticEnabled = "crt.menu.chromatic.enabled";
        private const string PrefBindUp = "crt.menu.bind.up";
        private const string PrefBindDown = "crt.menu.bind.down";
        private const string PrefBindLeft = "crt.menu.bind.left";
        private const string PrefBindRight = "crt.menu.bind.right";
        private const string PrefBindSubmit = "crt.menu.bind.submit";
        private const string PrefBindBack = "crt.menu.bind.back";
        private const string PrefUnlockedArchive = "crt.archive.unlocked";
        private const string PrefMountedArchive = "crt.archive.mounted";

        private static readonly string[] BootMessages =
        {
            "it remembers",
            "you opened it",
            "not empty"
        };

        private enum MenuState
        {
            PoweredOff,
            Booting,
            AwaitingAccess,
            MainMenu,
            Settings,
            LevelSelect,
            LockedSequence
        }

        private enum SettingsPage
        {
            Categories,
            General,
            Audio,
            Video,
            Controls
        }

        private enum RebindTarget
        {
            None,
            Up,
            Down,
            Left,
            Right,
            Submit,
            Back,
            MoveForward,
            MoveBackward,
            MoveLeft,
            MoveRight,
            Run,
            Interact,
            Inspect,
            Camera,
            Notebook,
            Pause
        }

        private sealed class MenuItem
        {
            public string Label;
            public bool Enabled = true;
            public bool Hidden;
            public System.Action Action;
            public System.Action<int> AdjustAction;
            public bool PreferAdjustWithHorizontal;
        }

        [Header("Content")]
        [Tooltip("Titulo idle principal que aparece en la pantalla antes y durante el menu.")]
        [SerializeField] private string _idleTitle = "Archive: {null}";
        [Tooltip("Prompt mostrado cuando la pantalla ya encendio pero el menu aun no se abrio.")]
        [SerializeField] private string _bootPrompt = "PRESS ENTER OR CLICK TO INITIALIZE";
        [Tooltip("Mensajes de la secuencia inquietante al activar la pantalla.")]
        [SerializeField] private string[] _bootMessages = { "it remembers", "you opened it", "not empty" };
        [Tooltip("Subtitulo por defecto del menu principal.")]
        [SerializeField] private string _mainSubtitle = "RECOVERED TERMINAL // RESTRICTED ACCESS NODE";

        [Header("Visual Style")]
        [SerializeField] private Color _idleScreenColor = new(0.03f, 0.06f, 0.055f, 1f);
        [SerializeField] private Color _screenOnColor = new(0.06f, 0.15f, 0.14f, 1f);
        [SerializeField] private Color _accentColor = new(0.78f, 0.96f, 0.92f, 1f);
        [SerializeField] private Color _mutedColor = new(0.47f, 0.66f, 0.63f, 1f);
        [SerializeField] private Color _dangerColor = new(1f, 0.2f, 0.18f, 1f);
        [Range(0.05f, 1f)]
        [SerializeField] private float _flickerStrength = 0.45f;

        [Header("Timing")]
        [SerializeField] private float _bootInitialDelay = 0.1f;
        [SerializeField] private float _bootLineDuration = 0.16f;
        [SerializeField] private float _bootBloomDuration = 0.14f;
        [SerializeField] private float _contentFadeDuration = 0.18f;
        [SerializeField] private float _messageGlitchDuration = 0.28f;
        [SerializeField] private float _messagePause = 0.12f;
        [SerializeField] private float _fatalPause = 0.32f;
        [SerializeField] private float _finalMessagePause = 0.22f;
        [Tooltip("Acelera especificamente la secuencia de power cycle / error.")]
        [SerializeField] private float _powerCycleSpeedMultiplier = 2.5f;

        [Header("Menu Behaviour")]
        [Tooltip("Muestra una barra visual de seleccion. Desactivalo si queres controlar toda la presentacion a mano.")]
        [SerializeField] private bool _showSelectionBar = false;
        [Tooltip("Si esta activo, el menu y submenus se escriben al abrirse. Si esta desactivado, aparecen ya escritos.")]
        [SerializeField] private bool _typeMenusOnOpen = false;
        [Tooltip("Recorta todo el contenido visual al rectangulo de la pantalla para que nada se salga del monitor.")]
        [SerializeField] private bool _clipContentToScreen = true;
        [Tooltip("Si esta asignado, al activar CLICK TO START primero se mueve la camara al monitor y luego se abre el menu.")]
        [SerializeField] private CRTMenuCameraFocus _cameraFocus;
        [Tooltip("Loader usado para entrar a la memoria montada directamente desde el terminal.")]
        [SerializeField] private MemorySceneLoader _memorySceneLoader;

        [Header("Monitor Light")]
        [SerializeField] private Light _monitorPowerLight;
        [SerializeField] private float _monitorLightDelay = 0.04f;
        [SerializeField] private float _monitorLightFadeDuration = 0.18f;
        [SerializeField] private float _monitorLightIntensity = 1.2f;

        [Header("Audio")]
        [Tooltip("AudioSource usado para reproducir sonidos del menu. Si esta vacio, el script intenta usar uno del mismo GameObject o crear uno.")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _bootStartClip;
        [SerializeField] private AudioClip _menuOpenClip;
        [SerializeField] private AudioClip _moveClip;
        [SerializeField] private AudioClip _confirmClip;
        [SerializeField] private AudioClip _backClip;
        [SerializeField] private AudioClip _glitchClip;
        [SerializeField] private AudioClip _shutdownClip;
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 0.9f;
        [Range(0f, 1f)]
        [SerializeField] private float _interfaceVolume = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float _systemVolume = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float _effectsVolume = 0.85f;
        [Tooltip("Activa o desactiva todos los sonidos del menu.")]
        [SerializeField] private bool _audioEnabled = true;
        [Tooltip("Si esta activo y faltan clips manuales, el script genera un banco retro procedural automaticamente.")]
        [SerializeField] private bool _useProceduralRetroSounds = true;

        [Header("Functional Settings")]
        [Tooltip("Idiomas disponibles para el submenu de opciones. El primero se usa como fallback.")]
        [SerializeField] private string[] _languageOptions = { "ESPANOL", "ENGLISH" };
        [Tooltip("Cantidad de filas visibles simultaneamente en el submenu de opciones generado por script.")]
        [SerializeField] private int _settingsVisibleRows = 6;
        [Tooltip("Paso de ajuste para sliders de audio.")]
        [SerializeField] private float _audioSliderStep = 0.05f;
        [Tooltip("Cantidad de segmentos usados para dibujar barras de audio en texto.")]
        [SerializeField] private int _sliderSegments = 10;

        [Header("Archive Flow")]
        [Tooltip("Cantidad total de archivos/niveles que aparecen en la computadora.")]
        [SerializeField] private int _archiveCount = 4;
        [Tooltip("Nombres visibles de los archivos. Si faltan nombres, el script usa ARCHIVE_XX.NULL.")]
        [SerializeField] private string[] _archiveNames = { "ARCHIVE_01.NULL", "ARCHIVE_02.NULL", "ARCHIVE_03.NULL", "ARCHIVE_04.NULL" };
        [Tooltip("Expediente al que pertenece cada memoria. Si queda vacio, todas las memorias aparecen dentro de CASE_01.")]
        [SerializeField] private string[] _archiveCaseNames = { "CASE_01" };
        [Tooltip("Build index de cada archivo. Si queda vacio o incompleto, usa escenas 1..4.")]
        [SerializeField] private int[] _archiveSceneBuildIndices = { 1, 2, 3, 4 };
        [Tooltip("Si esta activo, al abrir la escena se borra cualquier archivo montado previamente y obliga a seleccionar una memoria de nuevo.")]
        [SerializeField] private bool _clearMountedArchiveOnStartup = true;

        [Header("Scene Layout")]
        [Tooltip("Activa esto si vas a colocar toda la UI manualmente en el Canvas y queres que el script solo controle el comportamiento.")]
        [SerializeField] private bool _useSceneLayout;
        [Tooltip("Canvas que contiene toda la UI del menu.")]
        [SerializeField] private Canvas _sceneCanvas;
        [Tooltip("RectTransform del marco/monitor completo.")]
        [SerializeField] private RectTransform _sceneMonitor;
        [Tooltip("RectTransform de la pantalla CRT. Debe tener un Image.")]
        [SerializeField] private RectTransform _sceneScreen;
        [Tooltip("Grupo principal del contenido visible.")]
        [SerializeField] private CanvasGroup _sceneContentGroup;
        [Tooltip("Grupo overlay CRT. Si usas layout manual, sus hijos de overlay deben ser RawImage, no Image.")]
        [SerializeField] private CanvasGroup _sceneOverlayGroup;
        [Tooltip("Root del menu principal.")]
        [SerializeField] private RectTransform _sceneMainMenuRoot;
        [Tooltip("Root del submenu de settings.")]
        [SerializeField] private RectTransform _sceneSettingsRoot;
        [Tooltip("Barra visual de seleccion opcional.")]
        [SerializeField] private RectTransform _sceneSelectionBar;
        [Tooltip("Glow general de la pantalla.")]
        [SerializeField] private Image _sceneScreenGlow;
        [Tooltip("Linea brillante del encendido CRT.")]
        [SerializeField] private Image _sceneBootLine;
        [Tooltip("Bloom del encendido CRT.")]
        [SerializeField] private Image _sceneBootBloom;

        [Header("Scene Text References")]
        [SerializeField] private TMP_Text _sceneTitleText;
        [SerializeField] private TMP_Text _sceneGhostTitleText;
        [SerializeField] private TMP_Text _sceneSubtitleText;
        [SerializeField] private TMP_Text _scenePromptText;
        [SerializeField] private TMP_Text _sceneStatusText;
        [SerializeField] private TMP_Text _sceneFooterText;
        [SerializeField] private TMP_Text _sceneDiagnosticText;
        [SerializeField] private TMP_Text[] _sceneMainMenuOptionTexts;
        [SerializeField] private TMP_Text[] _sceneSettingsOptionTexts;

        [Header("Scene Overlay RawImages")]
        [Tooltip("RawImage para scanlines. Si lo asignas, el script le carga la textura automaticamente.")]
        [SerializeField] private RawImage _sceneScanlines;
        [Tooltip("RawImage para mascara RGB.")]
        [SerializeField] private RawImage _sceneRgbMask;
        [Tooltip("RawImage para ruido CRT.")]
        [SerializeField] private RawImage _sceneNoise;

        [Header("Camera")]
        [Tooltip("Si esta activo, el script fuerza fondo negro en la Main Camera. Si esta desactivado, respeta totalmente la camara de la escena.")]
        [SerializeField] private bool _configureMainCamera = false;
        [Tooltip("Si se configura la camara, solo se ajusta el background y clear flags. La posicion y rotacion quedan intactas.")]
        [SerializeField] private Color _cameraBackgroundColor = Color.black;

        private readonly List<MenuItem> _mainMenuItems = new();
        private readonly List<MenuItem> _settingsItems = new();
        private readonly List<TMP_Text> _mainMenuTexts = new();
        private readonly List<TMP_Text> _settingsTexts = new();

        private Canvas _canvas;
        private RectTransform _monitor;
        private RectTransform _screen;
        private Image _screenImage;
        private Image _screenGlow;
        private Image _bootLine;
        private Image _bootBloom;
        private CanvasGroup _contentGroup;
        private CanvasGroup _overlayGroup;
        private CanvasGroup _mainMenuGroup;
        private CanvasGroup _settingsGroup;
        private RectTransform _mainMenuRoot;
        private RectTransform _settingsRoot;
        private RectTransform _selectionBar;

        private TMP_Text _titleText;
        private TMP_Text _ghostTitleText;
        private TMP_Text _subtitleText;
        private TMP_Text _promptText;
        private TMP_Text _statusText;
        private TMP_Text _footerText;
        private TMP_Text _diagnosticText;
        private Vector2 _monitorBasePosition;
        private CRTRetroSoundBank _retroSoundBank;

        private bool _sequenceRunning;
        private bool _poweredOn;
        private bool _subtitlesEnabled = true;
        private bool _scanlinesEnabled = true;
        private bool _chromaticEnabled = true;
        private float _flickerTimer;
        private float _idleGlitchTimer;
        private float _promptBlinkTimer;
        private float _glitchOverlayTimer;
        private int _mainIndex;
        private int _settingsIndex;
        private int _settingsScrollOffset;
        private int _levelIndex;
        private int _levelScrollOffset;
        private int _selectedCaseIndex = -1;
        private bool _levelBrowserShowingMemories;
        private int _languageIndex;
        private int _unlockedArchive = 1;
        private int _mountedArchive = -1;
        private SettingsPage _settingsPage = SettingsPage.Categories;
        private Key _navUpKey = Key.W;
        private Key _navDownKey = Key.S;
        private Key _navLeftKey = Key.A;
        private Key _navRightKey = Key.D;
        private Key _submitKey = Key.Enter;
        private Key _backKey = Key.Escape;
        private bool _openMainMenuAfterBoot;
        private bool _awaitingRebind;
        private RebindTarget _pendingRebind;
        private MenuState _state;
        private Coroutine _menuOpenRoutine;
        private Coroutine _monitorLightRoutine;

        public event System.Action<int, string> ArchiveMounted;

        private void Awake()
        {
            EnsureAudioSource();
            NormalizeSettingsData();
            LoadPreferences();

            if (_useSceneLayout && TryBindSceneLayout())
            {
                ApplySceneTextTheme();
            }
            else
            {
                BuildInterface();
            }

            ConfigureCamera();
            if (_memorySceneLoader == null)
            {
                _memorySceneLoader = FindObjectOfType<MemorySceneLoader>();
            }

            BuildMenus();
            ApplyRuntimeSettings();
            InitializePoweredOffState();
        }

        private void Start()
        {
        }

        private void OnApplicationQuit()
        {
            GameSaveSystem.MarkOfficeContext();
        }

        private void Update()
        {
            if (_canvas == null)
            {
                return;
            }

            UpdateVisualNoise();
            HandleInput();
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            CreateImage("Backdrop", canvasRect, new Color(0.005f, 0.005f, 0.005f, 1f), true);
            CreateBackdropGlow(canvasRect);
            CreateVignette(canvasRect);

            _monitor = CreatePanel("MonitorFrame", canvasRect, new Vector2(1320f, 880f), new Color(0.075f, 0.075f, 0.075f, 1f));
            _monitorBasePosition = _monitor.anchoredPosition;
            AddShadow(_monitor.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(0f, -30f));

            RectTransform outerBezel = CreatePanel("OuterBezel", _monitor, new Vector2(1230f, 780f), new Color(0.12f, 0.12f, 0.12f, 1f));
            AddOutline(outerBezel.gameObject, new Color(0.025f, 0.025f, 0.025f, 1f), new Vector2(8f, 8f));

            _screen = CreatePanel("Screen", outerBezel, new Vector2(1110f, 640f), _idleScreenColor);
            _screenImage = _screen.GetComponent<Image>();
            AddShadow(_screen.gameObject, new Color(0f, 0.7f, 0.65f, 0.18f), Vector2.zero);
            EnsureScreenMask();

            _screenGlow = CreateImage("ScreenGlow", _screen, new Color(0.42f, 0.95f, 0.9f, 0.08f), true);
            _screenGlow.sprite = CreateRadialSprite(256, 0.85f);

            _contentGroup = CreateCanvasGroup("ContentGroup", _screen);
            _contentGroup.alpha = 0f;

            RectTransform contentRoot = _contentGroup.transform as RectTransform;

            CreateHeaderBand(contentRoot);
            CreateFooterBand(contentRoot);

            _titleText = CreateText("Title", contentRoot, _idleTitle, 64, TextAlignmentOptions.TopLeft, _accentColor, FontStyles.Bold);
            SetRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(84f, -150f), new Vector2(-360f, -56f));
            AddShadow(_titleText.gameObject, new Color(0.45f, 0.95f, 0.9f, 0.25f), Vector2.zero);

            _ghostTitleText = CreateText("GhostTitle", contentRoot, _idleTitle, 64, TextAlignmentOptions.TopLeft, new Color(1f, 0.08f, 0.08f, 0.08f), FontStyles.Bold);
            SetRect(_ghostTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(90f, -154f), new Vector2(-354f, -60f));

            _subtitleText = CreateText("Subtitle", contentRoot, _mainSubtitle, 22, TextAlignmentOptions.TopLeft, _mutedColor, FontStyles.Normal);
            SetRect(_subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(88f, -212f), new Vector2(-360f, -152f));

            _mainMenuRoot = CreateLayoutRoot("MainMenuRoot", contentRoot, new Vector2(88f, -286f), new Vector2(500f, 280f));
            CreateMenuPanel(_mainMenuRoot, new Vector2(530f, 304f));
            _selectionBar = CreateImage("SelectionBar", _mainMenuRoot, new Color(0.75f, 0.96f, 0.92f, 0.12f), false).rectTransform;
            _selectionBar.SetSiblingIndex(1);

            _settingsRoot = CreateLayoutRoot("SettingsRoot", contentRoot, new Vector2(88f, -286f), new Vector2(760f, 320f));
            CreateMenuPanel(_settingsRoot, new Vector2(790f, 344f));

            _diagnosticText = CreateText("DiagnosticText", contentRoot, "STATUS: STANDBY", 19, TextAlignmentOptions.TopLeft, _mutedColor, FontStyles.Normal);
            SetPointRect(_diagnosticText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-88f, -146f), new Vector2(320f, 150f));
            _diagnosticText.textWrappingMode = TextWrappingModes.Normal;
            _diagnosticText.overflowMode = TextOverflowModes.Overflow;

            _promptText = CreateText("Prompt", contentRoot, _bootPrompt, 24, TextAlignmentOptions.MidlineLeft, _accentColor, FontStyles.Normal);
            SetPointRect(_promptText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 118f), new Vector2(440f, 32f));

            _statusText = CreateText("Status", contentRoot, "READY", 18, TextAlignmentOptions.MidlineLeft, _mutedColor, FontStyles.Normal);
            SetPointRect(_statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 70f), new Vector2(520f, 26f));
            _statusText.textWrappingMode = TextWrappingModes.Normal;

            _footerText = CreateText("Footer", contentRoot, "NAV: W/S OR ARROWS  //  EXECUTE: ENTER  //  BACK: ESC", 17, TextAlignmentOptions.MidlineRight, _mutedColor, FontStyles.Normal);
            SetPointRect(_footerText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-88f, 36f), new Vector2(480f, 22f));
            _footerText.textWrappingMode = TextWrappingModes.Normal;

            _overlayGroup = CreateCanvasGroup("OverlayGroup", _screen);
            CreateScanlines(_overlayGroup.transform as RectTransform);
            CreateRgbMask(_overlayGroup.transform as RectTransform);
            CreateNoiseSpecks(_overlayGroup.transform as RectTransform);

            _bootBloom = CreateImage("BootBloom", _screen, new Color(0.87f, 1f, 0.98f, 0f), false);
            _bootBloom.rectTransform.sizeDelta = new Vector2(980f, 42f);
            AddShadow(_bootBloom.gameObject, new Color(0.9f, 1f, 0.98f, 0.7f), Vector2.zero);

            _bootLine = CreateImage("BootLine", _screen, new Color(1f, 1f, 1f, 0f), false);
            _bootLine.rectTransform.sizeDelta = new Vector2(0f, 8f);

            CreateCornerDetails(_monitor);
        }

        private bool TryBindSceneLayout()
        {
            if (_sceneScreen == null ||
                _sceneTitleText == null ||
                _sceneGhostTitleText == null ||
                _scenePromptText == null ||
                _sceneStatusText == null ||
                _sceneMainMenuRoot == null ||
                _sceneSettingsRoot == null)
            {
                return false;
            }

            _canvas = _sceneCanvas != null ? _sceneCanvas : GetComponentInParent<Canvas>();
            _monitor = _sceneMonitor != null ? _sceneMonitor : _sceneScreen;
            _screen = _sceneScreen;
            _screenImage = _screen.GetComponent<Image>();

            if (_canvas == null || _screenImage == null)
            {
                return false;
            }

            EnsureScreenMask();

            _contentGroup = _sceneContentGroup != null ? _sceneContentGroup : EnsureCanvasGroup(_screen.gameObject);
            _overlayGroup = _sceneOverlayGroup != null ? _sceneOverlayGroup : CreateCanvasGroup("OverlayGroup", _screen);
            _mainMenuRoot = _sceneMainMenuRoot;
            _settingsRoot = _sceneSettingsRoot;
            ApplySceneOverlayTextures();

            _selectionBar = _sceneSelectionBar != null
                ? _sceneSelectionBar
                : CreateImage("SelectionBar", _mainMenuRoot, new Color(0.75f, 0.96f, 0.92f, 0.12f), false).rectTransform;
            _selectionBar.SetSiblingIndex(Mathf.Min(1, _selectionBar.parent.childCount - 1));

            _screenGlow = _sceneScreenGlow != null
                ? _sceneScreenGlow
                : CreateImage("ScreenGlow", _screen, new Color(0.42f, 0.95f, 0.9f, 0.08f), true);
            if (_screenGlow.sprite == null)
            {
                _screenGlow.sprite = CreateRadialSprite(256, 0.85f);
            }

            _bootLine = _sceneBootLine != null
                ? _sceneBootLine
                : CreateImage("BootLine", _screen, new Color(1f, 1f, 1f, 0f), false);
            _bootBloom = _sceneBootBloom != null
                ? _sceneBootBloom
                : CreateImage("BootBloom", _screen, new Color(0.87f, 1f, 0.98f, 0f), false);

            _titleText = _sceneTitleText;
            _ghostTitleText = _sceneGhostTitleText;
            _subtitleText = _sceneSubtitleText;
            _promptText = _scenePromptText;
            _statusText = _sceneStatusText;
            _footerText = _sceneFooterText;
            _diagnosticText = _sceneDiagnosticText;

            _monitorBasePosition = _monitor.anchoredPosition;
            return true;
        }

        private void ApplySceneOverlayTextures()
        {
            if (_sceneScanlines != null)
            {
                _sceneScanlines.texture = GenerateScanlineTexture();
                _sceneScanlines.color = new Color(1f, 1f, 1f, 0.46f);
                _sceneScanlines.uvRect = new Rect(0f, 0f, 190f, 90f);
            }

            if (_sceneRgbMask != null)
            {
                _sceneRgbMask.texture = GenerateRgbMaskTexture();
                _sceneRgbMask.color = Color.white;
                _sceneRgbMask.uvRect = new Rect(0f, 0f, 370f, 1f);
            }

            if (_sceneNoise != null)
            {
                _sceneNoise.texture = GenerateNoiseTexture();
                _sceneNoise.color = Color.white;
                _sceneNoise.uvRect = new Rect(0f, 0f, 16f, 9f);
            }
        }

        private void EnsureScreenMask()
        {
            if (_screen == null)
            {
                return;
            }

            RectMask2D rectMask = _screen.GetComponent<RectMask2D>();
            if (_clipContentToScreen)
            {
                if (rectMask == null)
                {
                    _screen.gameObject.AddComponent<RectMask2D>();
                }
            }
            else if (rectMask != null)
            {
                Destroy(rectMask);
            }
        }

        private void ApplySceneTextTheme()
        {
            ApplyTextTheme(_titleText, 64, FontStyles.Bold, _accentColor);
            ApplyTextTheme(_ghostTitleText, 64, FontStyles.Bold, new Color(1f, 0.08f, 0.08f, 0.08f));
            ApplyTextTheme(_subtitleText, 22, FontStyles.Normal, _mutedColor);
            ApplyTextTheme(_promptText, 24, FontStyles.Normal, _accentColor);
            ApplyTextTheme(_statusText, 18, FontStyles.Normal, _mutedColor);
            ApplyTextTheme(_footerText, 17, FontStyles.Normal, _mutedColor);
            ApplyTextTheme(_diagnosticText, 19, FontStyles.Normal, _mutedColor);
        }

        private void ApplyTextTheme(TMP_Text text, float fontSize, FontStyles style, Color color)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.richText = false;
        }

        private void BuildMenus()
        {
            _mainMenuItems.Clear();
            _settingsItems.Clear();
            _mainMenuTexts.Clear();
            _settingsTexts.Clear();

            _mainMenuItems.Add(new MenuItem
            {
                Label = GetLocalizedMainMenuLabel(0),
                Action = OpenLevelBrowser
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = GetLocalizedMainMenuLabel(1),
                Action = OpenSettings
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = GetLocalizedMainMenuLabel(2),
                Action = ConfirmDeleteAllData
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = GetLocalizedMainMenuLabel(3),
                Action = QuitGame
            });

            BuildMainMenuTextPool();
            BuildSettingsTextPool();
            RebuildSettingsPage(SettingsPage.Categories, true, false);

            _mainMenuGroup = EnsureCanvasGroup(_mainMenuRoot.gameObject);
            _settingsGroup = EnsureCanvasGroup(_settingsRoot.gameObject);
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 0f;
            _settingsRoot.gameObject.SetActive(false);
            RefreshMainMenuAvailability();
        }

        private void BuildMainMenuTextPool()
        {
            if (_useSceneLayout && _sceneMainMenuOptionTexts != null)
            {
                for (int i = 0; i < _mainMenuItems.Count; i++)
                {
                    TMP_Text option = i < _sceneMainMenuOptionTexts.Length ? _sceneMainMenuOptionTexts[i] : null;
                    if (option == null)
                    {
                        option = CreateMenuText(_mainMenuRoot, _mainMenuItems[i].Label);
                    }

                    if (!option.gameObject.activeSelf)
                    {
                        option.gameObject.SetActive(true);
                    }

                    option.transform.SetParent(_mainMenuRoot, false);
                    ApplyTextTheme(option, 30, FontStyles.Bold, _accentColor);
                    option.rectTransform.anchoredPosition = new Vector2(0f, -i * 54f);
                    option.rectTransform.sizeDelta = new Vector2(470f, 42f);
                    _mainMenuTexts.Add(option);
                }

                EnsureSelectableIndex(_mainMenuItems, ref _mainIndex);

                if (_sceneMainMenuOptionTexts.Length > _mainMenuItems.Count)
                {
                    for (int i = _mainMenuItems.Count; i < _sceneMainMenuOptionTexts.Length; i++)
                    {
                        if (_sceneMainMenuOptionTexts[i] != null)
                        {
                            _sceneMainMenuOptionTexts[i].gameObject.SetActive(false);
                        }
                    }
                }
                return;
            }

            for (int i = 0; i < _mainMenuItems.Count; i++)
            {
                TMP_Text option = CreateMenuText(_mainMenuRoot, _mainMenuItems[i].Label);
                option.rectTransform.anchoredPosition = new Vector2(0f, -i * 54f);
                _mainMenuTexts.Add(option);
            }
        }

        private void BuildSettingsTextPool()
        {
            int activeProvidedCount = 0;
            if (_useSceneLayout && _sceneSettingsOptionTexts != null)
            {
                for (int i = 0; i < _sceneSettingsOptionTexts.Length; i++)
                {
                    if (_sceneSettingsOptionTexts[i] != null && _sceneSettingsOptionTexts[i].gameObject.activeSelf)
                    {
                        activeProvidedCount++;
                    }
                }
            }

            int desiredSlotCount = activeProvidedCount > 0 ? activeProvidedCount : _settingsVisibleRows;

            desiredSlotCount = Mathf.Max(1, desiredSlotCount);

            if (_useSceneLayout && _sceneSettingsOptionTexts != null)
            {
                for (int i = 0; i < _sceneSettingsOptionTexts.Length; i++)
                {
                    TMP_Text option = _sceneSettingsOptionTexts[i];
                    if (option == null || !option.gameObject.activeSelf)
                    {
                        continue;
                    }

                    ApplyTextTheme(option, 30, FontStyles.Bold, _accentColor);
                    _settingsTexts.Add(option);
                }
            }

            while (_settingsTexts.Count < desiredSlotCount)
            {
                TMP_Text option = CreateMenuText(_settingsRoot, string.Empty);
                _settingsTexts.Add(option);
            }

            LayoutSettingsTextSlots();
        }

        private void LayoutSettingsTextSlots()
        {
            float spacing = 48f;
            for (int i = 0; i < _settingsTexts.Count; i++)
            {
                TMP_Text text = _settingsTexts[i];
                if (text == null)
                {
                    continue;
                }

                text.rectTransform.anchoredPosition = new Vector2(0f, -i * spacing);
                text.rectTransform.sizeDelta = new Vector2(720f, 40f);
            }
        }

        private void InitializePoweredOffState()
        {
            _state = MenuState.PoweredOff;
            _poweredOn = false;
            _sequenceRunning = false;
            _openMainMenuAfterBoot = false;
            SetMonitorLightImmediate(0f);

            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 0f;
            _mainMenuRoot.gameObject.SetActive(false);
            _settingsRoot.gameObject.SetActive(false);
            _contentGroup.alpha = 0f;
            _overlayGroup.alpha = 0f;
            _screenImage.color = Color.black;
            _screenGlow.color = Color.black;
            _bootLine.color = new Color(1f, 1f, 1f, 0f);
            _bootBloom.color = new Color(0.87f, 1f, 0.98f, 0f);
            _titleText.text = string.Empty;
            _ghostTitleText.text = string.Empty;
            _subtitleText.text = string.Empty;
            _promptText.text = string.Empty;
            _statusText.text = string.Empty;
            _diagnosticText.text = string.Empty;
            _footerText.text = string.Empty;
        }

        private IEnumerator BootSequence()
        {
            _sequenceRunning = true;
            _poweredOn = false;
            _state = MenuState.Booting;
            SetMonitorLightImmediate(0f);

            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 0f;
            _mainMenuRoot.gameObject.SetActive(true);
            _settingsRoot.gameObject.SetActive(false);
            _contentGroup.alpha = 0f;
            _titleText.text = string.Empty;
            _ghostTitleText.text = string.Empty;
            _subtitleText.text = string.Empty;
            _promptText.text = string.Empty;
            _statusText.text = string.Empty;
            _diagnosticText.text = string.Empty;
            _footerText.text = string.Empty;
            _overlayGroup.alpha = 0.72f;
            _screenImage.color = Color.black;
            _screenGlow.color = Color.black;
            _bootLine.color = new Color(1f, 1f, 1f, 0f);
            _bootBloom.color = new Color(0.87f, 1f, 0.98f, 0f);

            if (_monitorLightRoutine != null)
            {
                StopCoroutine(_monitorLightRoutine);
            }
            _monitorLightRoutine = StartCoroutine(AnimateMonitorLight(_monitorLightDelay, _monitorLightFadeDuration, _monitorLightIntensity));

            if (_monitorLightDelay > 0f)
            {
                yield return new WaitForSeconds(_monitorLightDelay);
            }

            PlayUiSound(_bootStartClip, _systemVolume);

            float remainingBootDelay = Mathf.Max(0f, _bootInitialDelay - _monitorLightDelay);
            if (remainingBootDelay > 0f)
            {
                yield return new WaitForSeconds(remainingBootDelay);
            }

            float timer = 0f;
            while (timer < _bootLineDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = EaseOutExpo(Mathf.Clamp01(timer / _bootLineDuration));
                _screenImage.color = Color.Lerp(new Color(0.002f, 0.005f, 0.005f, 1f), new Color(0.05f, 0.12f, 0.11f, 1f), t * 0.65f);
                _bootLine.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, t));
                _bootBloom.color = new Color(0.85f, 1f, 0.97f, Mathf.Lerp(0.1f, 0.9f, t));
                _bootLine.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(12f, 1110f, t), Mathf.Lerp(2f, 10f, t));
                _bootBloom.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(120f, 1020f, t), Mathf.Lerp(14f, 54f, t));
                yield return null;
            }

            timer = 0f;
            while (timer < _bootBloomDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(timer / _bootBloomDuration));
                _bootLine.rectTransform.sizeDelta = new Vector2(1110f, Mathf.Lerp(10f, 640f, t));
                _bootBloom.rectTransform.sizeDelta = new Vector2(1110f, Mathf.Lerp(54f, 720f, t));
                _bootLine.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.12f, t));
                _bootBloom.color = new Color(0.88f, 1f, 0.98f, Mathf.Lerp(0.95f, 0.08f, t));
                _screenGlow.color = new Color(0.45f, 0.98f, 0.92f, Mathf.Lerp(0.05f, 0.15f, t));
                yield return null;
            }

            _bootLine.color = new Color(1f, 1f, 1f, 0f);
            _bootBloom.color = new Color(0.88f, 1f, 0.98f, 0f);
            yield return StartCoroutine(FlickerSettling());

            _screenImage.color = _idleScreenColor;
            _screenGlow.color = new Color(0.42f, 0.95f, 0.9f, 0.08f);
            _titleText.text = _idleTitle;
            _ghostTitleText.text = _idleTitle;
            _subtitleText.text = _mainSubtitle;
            _promptText.text = _bootPrompt;
            _statusText.text = "SYSTEM READY // ACCESS GATE LOCKED";
            _diagnosticText.text = "CRT SIGNAL STABLE // ARCHIVE INDEX ONLINE";
            _footerText.text = "EXECUTE: ENTER OR CLICK";
            yield return StartCoroutine(FadeCanvasGroup(_contentGroup, 0f, 1f, _contentFadeDuration));

            _poweredOn = true;
            _sequenceRunning = false;

            if (_openMainMenuAfterBoot)
            {
                _openMainMenuAfterBoot = false;
                OpenMainMenu();
                yield break;
            }

            _state = MenuState.AwaitingAccess;
        }

        private IEnumerator FlickerSettling()
        {
            float[] flashes = { 0.18f, 0.62f, 0.24f, 0.88f, 0.52f, 0.7f };
            for (int i = 0; i < flashes.Length; i++)
            {
                float sample = flashes[i];
                _overlayGroup.alpha = Mathf.Lerp(0.42f, 0.92f, sample);
                _screenImage.color = Color.Lerp(new Color(0.02f, 0.035f, 0.034f, 1f), _screenOnColor, sample);
                yield return new WaitForSeconds(0.015f + i * 0.004f);
            }
        }

        private void HandleInput()
        {
            if (_awaitingRebind)
            {
                CaptureRebindInput();
                return;
            }

            if (_sequenceRunning)
            {
                return;
            }

            if (_cameraFocus != null && !_cameraFocus.IsFocused)
            {
                return;
            }

            bool submit = WasSubmitPressedCustom();
            bool pointerClick = WasPointerClicked();

            if (_state == MenuState.PoweredOff)
            {
                return;
            }

            if (!_poweredOn)
            {
                return;
            }

            if (_state == MenuState.AwaitingAccess)
            {
                if (submit || pointerClick)
                {
                    OpenMainMenu();
                }

                return;
            }

            if (_state == MenuState.MainMenu)
            {
                if (WasBackPressed())
                {
                    _cameraFocus?.MoveToStandPose();
                    return;
                }

                int direction = ReadVerticalNavigation();
                if (direction != 0)
                {
                    MoveSelection(_mainMenuItems, ref _mainIndex, direction);
                    PlayUiSound(_moveClip, _interfaceVolume);
                    RefreshMenuVisuals();
                }

                bool pointerOverMainOption = UpdateSelectionFromPointer(_mainMenuTexts, _mainMenuItems, ref _mainIndex);

                if (submit || (pointerClick && pointerOverMainOption))
                {
                    PlayUiSound(_confirmClip, _interfaceVolume);
                    ExecuteItem(_mainMenuItems[_mainIndex]);
                }

                return;
            }

            if (_state == MenuState.Settings)
            {
                int direction = ReadVerticalNavigation();
                if (direction != 0)
                {
                    MoveSelection(_settingsItems, ref _settingsIndex, direction);
                    ClampSettingsScrollToSelection();
                    PlayUiSound(_moveClip, _interfaceVolume);
                    RefreshMenuVisuals();
                }

                int horizontal = ReadHorizontalNavigation();
                if (horizontal != 0 && TryAdjustSelectedSetting(horizontal))
                {
                    PlayUiSound(_moveClip, _interfaceVolume);
                    return;
                }

                bool pointerOverSettingsOption = UpdateSettingsSelectionFromPointer(ref _settingsIndex);

                if (WasBackPressed())
                {
                    PlayUiSound(_backClip, _interfaceVolume);
                    if (_settingsPage == SettingsPage.Categories)
                    {
                        CloseSettings();
                    }
                    else
                    {
                        OpenSettingsCategoriesFromSubPage();
                    }
                    return;
                }

                if (submit || (pointerClick && pointerOverSettingsOption))
                {
                    if (pointerClick)
                    {
                        int pointerAdjustDirection = GetPointerAdjustDirection();
                        if (pointerAdjustDirection != 0 && TryAdjustSelectedSetting(pointerAdjustDirection))
                        {
                            PlayUiSound(_moveClip, _interfaceVolume);
                            return;
                        }
                    }

                    PlayUiSound(_confirmClip, _interfaceVolume);
                    ExecuteItem(_settingsItems[_settingsIndex]);
                }
            }

            if (_state == MenuState.LevelSelect)
            {
                int direction = ReadVerticalNavigation();
                if (direction != 0)
                {
                    MoveSelection(_settingsItems, ref _levelIndex, direction);
                    ClampLevelScrollToSelection();
                    PlayUiSound(_moveClip, _interfaceVolume);
                    RefreshLevelBrowserVisuals();
                }

                bool pointerOverLevel = UpdateLevelSelectionFromPointer(ref _levelIndex);
                if (WasBackPressed())
                {
                    PlayUiSound(_backClip, _interfaceVolume);
                    if (_levelBrowserShowingMemories)
                    {
                        OpenLevelBrowser();
                    }
                    else
                    {
                        CloseLevelBrowser();
                    }
                    return;
                }

                if (submit || (pointerClick && pointerOverLevel))
                {
                    PlayUiSound(_confirmClip, _interfaceVolume);
                    ExecuteItem(_settingsItems[_levelIndex]);
                }
            }
        }

        private void OpenMainMenu()
        {
            _state = MenuState.MainMenu;
            PlayUiSound(_menuOpenClip, _interfaceVolume);
            _mainMenuGroup.alpha = 1f;
            _settingsGroup.alpha = 0f;
            _mainMenuRoot.gameObject.SetActive(true);
            _settingsRoot.gameObject.SetActive(false);
            RefreshMainMenuAvailability();
            EnsureSelectableIndex(_mainMenuItems, ref _mainIndex);
            _promptText.text = string.Empty;
            _footerText.text = GetMainMenuFooter();
            SetStatus(GetLocalizedStatusUnlocked());
            ShowMenuState(_mainMenuTexts, _mainMenuItems, _mainIndex);
        }

        private void OpenSettings()
        {
            OpenSettingsCategories();
        }

        private void CloseSettings()
        {
            _state = MenuState.MainMenu;
            PlayUiSound(_backClip, _interfaceVolume);
            _mainMenuRoot.gameObject.SetActive(true);
            _settingsGroup.alpha = 0f;
            _mainMenuGroup.alpha = 1f;
            _subtitleText.text = _mainSubtitle;
            _promptText.text = string.Empty;
            _footerText.text = GetMainMenuFooter();
            SetStatus(GetLocalizedStatusReturnMain());
            ShowMenuState(_mainMenuTexts, _mainMenuItems, _mainIndex, hideRoot: _settingsRoot, showRoot: _mainMenuRoot);
        }

        private IEnumerator ActivationSequence()
        {
            _state = MenuState.LockedSequence;
            _sequenceRunning = true;
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 0f;
            _mainMenuRoot.gameObject.SetActive(false);
            _settingsRoot.gameObject.SetActive(false);
            _promptText.text = string.Empty;
            _footerText.text = string.Empty;
            float speed = Mathf.Max(0.01f, _powerCycleSpeedMultiplier);
            float glitchDuration = _messageGlitchDuration / speed;
            float messagePause = _messagePause / speed;
            float fatalPause = _fatalPause / speed;
            float finalPause = _finalMessagePause / speed;

            for (int i = 0; i < _bootMessages.Length; i++)
            {
                yield return StartCoroutine(GlitchTo(_bootMessages[i], glitchDuration));

                if (i < _bootMessages.Length - 1)
                {
                    yield return new WaitForSeconds(messagePause);
                }
            }

            _titleText.color = _dangerColor;
            _ghostTitleText.color = new Color(1f, 0.12f, 0.12f, 0.14f);
            _titleText.text = "FATAL_SYSTEM_ERROR";
            _ghostTitleText.text = _titleText.text;
            _subtitleText.text = string.Empty;
            _diagnosticText.text = "MEMORY GATE DESYNC // ORIGIN UNKNOWN";
            SetStatus("KERNEL PANIC.");
            _glitchOverlayTimer = 0.9f;
            PlayUiSound(_glitchClip, _effectsVolume);

            yield return StartCoroutine(ShakeMonitor(0.32f, 16f));

            yield return new WaitForSeconds(fatalPause);
            _titleText.text = "you shouldn't be here";
            _ghostTitleText.text = _titleText.text;
            yield return new WaitForSeconds(finalPause);
            yield return StartCoroutine(WhiteoutAndShutdown());
        }

        private IEnumerator WhiteoutAndShutdown()
        {
            float timer = 0f;
            const float duration = 0.05f;
            Color start = _screenImage.color;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                _screenImage.color = Color.Lerp(start, Color.white, EaseOutExpo(t));
                _screenGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.12f, 0.3f, t));
                _overlayGroup.alpha = Mathf.Lerp(0.88f, 0f, t);
                _contentGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            yield return new WaitForSeconds(0.015f);

            timer = 0f;
            while (timer < 0.045f)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / 0.045f);
                _screenImage.color = Color.Lerp(Color.white, Color.black, t);
                yield return null;
            }

            _contentGroup.alpha = 0f;
            _overlayGroup.alpha = 0f;
            _monitor.anchoredPosition = _monitorBasePosition;
            _titleText.color = _accentColor;
            _ghostTitleText.color = new Color(1f, 0.08f, 0.08f, 0.08f);
            _sequenceRunning = false;
            _poweredOn = false;
            SetMonitorLightImmediate(0f);
            PlayUiSound(_shutdownClip, _systemVolume);
            yield return new WaitForSeconds(0.08f);
            StartCoroutine(BootSequence());
        }

        private IEnumerator GlitchTo(string targetText, float duration)
        {
            const string glitchChars = "X$#!?{}_0123456789ABCDEF/[]";
            float timer = 0f;
            float iteration = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                if (Random.value > 0.85f)
                {
                    iteration -= 1f;
                }

                iteration += Random.Range(0.65f, 1.9f);
                int revealed = Mathf.Clamp(Mathf.FloorToInt(iteration), 0, targetText.Length);
                char[] chars = targetText.ToCharArray();
                for (int i = revealed; i < chars.Length; i++)
                {
                    chars[i] = glitchChars[Random.Range(0, glitchChars.Length)];
                }

                string current = new(chars);
                _titleText.text = current;
                _ghostTitleText.text = current;
                yield return new WaitForSeconds(0.03f);
            }

            _titleText.text = targetText;
            _ghostTitleText.text = targetText;
        }

        private IEnumerator ShakeMonitor(float duration, float strength)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(timer / duration);
                _monitor.anchoredPosition = _monitorBasePosition + Random.insideUnitCircle * (strength * fade);
                _overlayGroup.alpha = Random.Range(0.72f, 0.98f);
                _screenImage.color = Color.Lerp(new Color(0.18f, 0.03f, 0.03f, 1f), new Color(0.32f, 0.04f, 0.04f, 1f), Random.value);
                yield return null;
            }

            _monitor.anchoredPosition = _monitorBasePosition;
            _screenImage.color = _idleScreenColor;
        }

        private IEnumerator PowerCycleFromSettings()
        {
            CloseSettings();
            yield return StartCoroutine(ActivationSequence());
        }

        private void UpdateVisualNoise()
        {
            if (_state == MenuState.PoweredOff)
            {
                _screenImage.color = Color.black;
                _overlayGroup.alpha = 0f;
                _screenGlow.color = Color.black;
                _screen.anchoredPosition = Vector2.zero;
                _screen.localScale = Vector3.one;
                return;
            }

            _flickerTimer -= Time.unscaledDeltaTime;
            if (_flickerTimer <= 0f)
            {
                _flickerTimer = Random.Range(0.05f, 0.18f);
                float opacityRoll = Random.value;
                float intensity = opacityRoll < 0.7f ? 1f : opacityRoll < 0.9f ? 0.6f : 0.2f;
                intensity = Mathf.Lerp(intensity, 1f, 1f - _flickerStrength);
                Color flickered = _idleScreenColor * intensity;
                flickered.a = 1f;
                _screenImage.color = Color.Lerp(_idleScreenColor, flickered, 0.9f);
                _overlayGroup.alpha = Mathf.Lerp(0.42f, 0.82f, Random.value);
            }

            if (_state == MenuState.AwaitingAccess || _state == MenuState.MainMenu || _state == MenuState.Settings)
            {
                _idleGlitchTimer -= Time.unscaledDeltaTime;
                if (_idleGlitchTimer <= 0f)
                {
                    _idleGlitchTimer = 0.08f;
                    string title = ScrambleText(_idleTitle, 0.04f);
                    _titleText.text = title;
                    _ghostTitleText.text = title;
                }

                _promptBlinkTimer -= Time.unscaledDeltaTime;
                if (_promptBlinkTimer <= 0f)
                {
                    _promptBlinkTimer = 0.45f;
                    if (_state == MenuState.AwaitingAccess)
                    {
                        _promptText.enabled = !_promptText.enabled;
                    }
                }
                else if (_state != MenuState.AwaitingAccess)
                {
                    _promptText.enabled = true;
                }
            }

            if (_glitchOverlayTimer > 0f)
            {
                _glitchOverlayTimer -= Time.unscaledDeltaTime;
                float phase = Mathf.Repeat(Time.unscaledTime * 18f, 1f);
                float shift = Mathf.Lerp(-18f, 12f, phase);
                _screen.anchoredPosition = new Vector2(shift, Random.Range(-4f, 4f));
                _screen.localScale = new Vector3(1f, Random.Range(0.985f, 1.025f), 1f);
            }
            else
            {
                _screen.anchoredPosition = Vector2.zero;
                _screen.localScale = Vector3.one;
            }

            _screenGlow.color = Color.Lerp(_screenGlow.color, new Color(0.42f, 0.95f, 0.9f, 0.08f + Random.Range(0f, 0.025f)), 0.08f);
        }

        private void RefreshMenuVisuals()
        {
            for (int i = 0; i < _mainMenuTexts.Count; i++)
            {
                if (i >= _mainMenuItems.Count)
                {
                    continue;
                }

                TMP_Text text = _mainMenuTexts[i];
                MenuItem item = _mainMenuItems[i];
                if (text == null)
                {
                    continue;
                }

                if (item.Hidden)
                {
                    text.gameObject.SetActive(false);
                    continue;
                }

                if (!text.gameObject.activeSelf)
                {
                    text.gameObject.SetActive(true);
                }

                bool selected = i == _mainIndex && _state == MenuState.MainMenu;
                text.text = BuildMenuLine(item.Label, selected, item.Enabled);
                ApplyMenuItemStyle(text, item, selected);
            }

            if (_state == MenuState.LevelSelect)
            {
                RefreshLevelBrowserVisuals();
            }
            else
            {
                RefreshSettingsTextSlots();
            }

            if (_state == MenuState.MainMenu && _mainMenuTexts.Count > 0)
            {
                PositionSelectionBar(_mainMenuTexts[_mainIndex].rectTransform);
            }
            else if (_state == MenuState.Settings && _settingsTexts.Count > 0)
            {
                TMP_Text selectedText = GetVisibleSettingsTextForIndex(_settingsIndex);
                if (selectedText != null)
                {
                    PositionSelectionBar(selectedText.rectTransform);
                }
                else if (_selectionBar != null)
                {
                    _selectionBar.gameObject.SetActive(false);
                }
            }
            else if (_state == MenuState.LevelSelect && _settingsTexts.Count > 0)
            {
                TMP_Text selectedText = GetVisibleLevelTextForIndex(_levelIndex);
                if (selectedText != null)
                {
                    PositionSelectionBar(selectedText.rectTransform);
                }
            }
            else if (_selectionBar != null)
            {
                _selectionBar.gameObject.SetActive(false);
            }
        }

        private void ApplyMenuItemStyle(TMP_Text text, MenuItem item, bool selected)
        {
            if (!item.Enabled)
            {
                text.color = new Color(0.3f, 0.4f, 0.38f, 1f);
                return;
            }

            if (item.PreferAdjustWithHorizontal)
            {
                text.color = selected ? new Color(0.98f, 1f, 0.98f, 1f) : new Color(0.76f, 0.93f, 0.9f, 1f);
                return;
            }

            text.color = selected ? new Color(0.94f, 1f, 0.97f, 1f) : _accentColor;
        }

        private void PositionSelectionBar(RectTransform target)
        {
            if (_selectionBar == null)
            {
                return;
            }

            if (!_showSelectionBar)
            {
                _selectionBar.gameObject.SetActive(false);
                return;
            }

            _selectionBar.gameObject.SetActive(true);
            _selectionBar.SetParent(target.parent, false);
            _selectionBar.anchorMin = target.anchorMin;
            _selectionBar.anchorMax = target.anchorMax;
            _selectionBar.pivot = target.pivot;
            _selectionBar.anchoredPosition = target.anchoredPosition + new Vector2(-18f, -2f);
            _selectionBar.sizeDelta = new Vector2(target.sizeDelta.x + 36f, target.sizeDelta.y + 12f);
        }

        private void ExecuteItem(MenuItem item)
        {
            if (!item.Enabled || item.Hidden || item.Action == null)
            {
                return;
            }

            item.Action.Invoke();

            if (_menuOpenRoutine == null && !_sequenceRunning)
            {
                RefreshMenuVisuals();
            }
        }

        private string BuildMenuLine(string label, bool selected, bool enabled)
        {
            string prefix = selected && enabled ? "> " : "  ";
            return prefix + label;
        }

        private void RefreshSettingsTextSlots()
        {
            for (int slot = 0; slot < _settingsTexts.Count; slot++)
            {
                TMP_Text text = _settingsTexts[slot];
                if (text == null)
                {
                    continue;
                }

                int itemIndex = _settingsScrollOffset + slot;
                bool isVisible = itemIndex >= 0 && itemIndex < _settingsItems.Count;
                text.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                MenuItem item = _settingsItems[itemIndex];
                bool selected = itemIndex == _settingsIndex && _state == MenuState.Settings;
                text.text = BuildMenuLine(item.Label, selected, item.Enabled);
                ApplyMenuItemStyle(text, item, selected);
                text.maxVisibleCharacters = 9999;
            }

            UpdateSettingsDiagnostic();
        }

        private void RefreshLevelBrowserVisuals()
        {
            for (int slot = 0; slot < _settingsTexts.Count; slot++)
            {
                TMP_Text text = _settingsTexts[slot];
                if (text == null)
                {
                    continue;
                }

                int itemIndex = _levelScrollOffset + slot;
                bool isVisible = itemIndex >= 0 && itemIndex < _settingsItems.Count;
                text.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                MenuItem item = _settingsItems[itemIndex];
                bool selected = itemIndex == _levelIndex && _state == MenuState.LevelSelect;
                text.text = BuildMenuLine(item.Label, selected, item.Enabled);
                ApplyMenuItemStyle(text, item, selected);
                text.maxVisibleCharacters = 9999;
            }
        }

        private TMP_Text GetVisibleSettingsTextForIndex(int itemIndex)
        {
            int slot = itemIndex - _settingsScrollOffset;
            if (slot < 0 || slot >= _settingsTexts.Count)
            {
                return null;
            }

            return _settingsTexts[slot];
        }

        private TMP_Text GetVisibleLevelTextForIndex(int itemIndex)
        {
            int slot = itemIndex - _levelScrollOffset;
            if (slot < 0 || slot >= _settingsTexts.Count)
            {
                return null;
            }

            return _settingsTexts[slot];
        }

        private void ShowMenuState(List<TMP_Text> texts, List<MenuItem> items, int selectedIndex, RectTransform hideRoot = null, RectTransform showRoot = null)
        {
            if (_typeMenusOnOpen)
            {
                StartMenuOpenAnimation(texts, items, selectedIndex, hideRoot, showRoot);
                return;
            }

            if (_menuOpenRoutine != null)
            {
                StopCoroutine(_menuOpenRoutine);
                _menuOpenRoutine = null;
            }

            if (hideRoot != null)
            {
                hideRoot.gameObject.SetActive(false);
            }

            if (showRoot != null)
            {
                showRoot.gameObject.SetActive(true);
            }

            _sequenceRunning = false;
            RefreshMenuVisuals();
        }

        private void StartMenuOpenAnimation(List<TMP_Text> texts, List<MenuItem> items, int selectedIndex, RectTransform hideRoot = null, RectTransform showRoot = null)
        {
            if (_menuOpenRoutine != null)
            {
                StopCoroutine(_menuOpenRoutine);
            }

            _sequenceRunning = true;

            if (hideRoot != null)
            {
                hideRoot.gameObject.SetActive(false);
            }

            if (showRoot != null)
            {
                showRoot.gameObject.SetActive(true);
            }

            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i] == null)
                {
                    continue;
                }

                texts[i].text = string.Empty;
                texts[i].maxVisibleCharacters = 0;
            }

            _menuOpenRoutine = StartCoroutine(AnimateMenuOpen(texts, items, selectedIndex, hideRoot, showRoot));
        }

        private IEnumerator AnimateMenuOpen(List<TMP_Text> texts, List<MenuItem> items, int selectedIndex, RectTransform hideRoot, RectTransform showRoot)
        {
            _sequenceRunning = true;

            if (hideRoot != null)
            {
                hideRoot.gameObject.SetActive(false);
            }

            if (showRoot != null)
            {
                showRoot.gameObject.SetActive(true);
            }

            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i] == null)
                {
                    continue;
                }

                texts[i].text = string.Empty;
                texts[i].maxVisibleCharacters = 0;
            }

            _promptText.text = string.Empty;
            yield return new WaitForSeconds(0.04f);

            for (int i = 0; i < texts.Count && i < items.Count; i++)
            {
                if (texts[i] == null || items[i].Hidden || !texts[i].gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool selected = i == selectedIndex;
                string line = BuildMenuLine(items[i].Label, selected, items[i].Enabled);
                yield return StartCoroutine(TypeLine(texts[i], line, 0.009f));
                ApplyMenuItemStyle(texts[i], items[i], selected);
                yield return new WaitForSeconds(0.02f);
            }

            _promptText.text = string.Empty;
            RefreshMenuVisuals();
            _sequenceRunning = false;
            _menuOpenRoutine = null;
        }

        private IEnumerator TypeLine(TMP_Text text, string content, float charDelay)
        {
            text.text = content;
            text.ForceMeshUpdate();
            int totalCharacters = text.textInfo.characterCount;
            text.maxVisibleCharacters = 0;

            for (int i = 0; i <= totalCharacters; i++)
            {
                text.maxVisibleCharacters = i;
                yield return new WaitForSeconds(charDelay);
            }

            text.maxVisibleCharacters = 9999;
        }

        private void RebuildSettingsPage(SettingsPage page, bool resetSelection, bool refreshImmediately = true)
        {
            _settingsPage = page;
            _settingsItems.Clear();

            switch (page)
            {
                case SettingsPage.Categories:
                    _settingsItems.Add(new MenuItem { Label = GetSettingsCategoryLabel(SettingsPage.General), Action = () => OpenSettingsSubPage(SettingsPage.General) });
                    _settingsItems.Add(new MenuItem { Label = GetSettingsCategoryLabel(SettingsPage.Audio), Action = () => OpenSettingsSubPage(SettingsPage.Audio) });
                    _settingsItems.Add(new MenuItem { Label = GetSettingsCategoryLabel(SettingsPage.Video), Action = () => OpenSettingsSubPage(SettingsPage.Video) });
                    _settingsItems.Add(new MenuItem { Label = GetSettingsCategoryLabel(SettingsPage.Controls), Action = () => OpenSettingsSubPage(SettingsPage.Controls) });
                    _settingsItems.Add(new MenuItem { Label = GetLocalizedReturnLabel(), Action = CloseSettings });
                    break;

                case SettingsPage.General:
                    _settingsItems.Add(new MenuItem { Label = BuildLanguageLabel(), Action = CycleLanguage, AdjustAction = AdjustLanguage, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildSubtitlesLabel(), Action = ToggleSubtitles });
                    _settingsItems.Add(new MenuItem { Label = BuildAudioEnabledLabel(), Action = ToggleAudioEnabled });
                    _settingsItems.Add(new MenuItem { Label = Localize("VOLVER A CATEGORIAS", "BACK TO CATEGORIES"), Action = OpenSettingsCategoriesFromSubPage });
                    break;

                case SettingsPage.Audio:
                    _settingsItems.Add(new MenuItem { Label = BuildSliderLabel(Localize("VOLUMEN MAESTRO", "MASTER VOLUME"), _masterVolume), Action = () => AdjustMasterVolume(1), AdjustAction = AdjustMasterVolume, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildSliderLabel(Localize("SONIDOS MENU", "MENU SOUNDS"), _interfaceVolume), Action = () => AdjustInterfaceVolume(1), AdjustAction = AdjustInterfaceVolume, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildSliderLabel(Localize("SISTEMA CRT", "CRT SYSTEM"), _systemVolume), Action = () => AdjustSystemVolume(1), AdjustAction = AdjustSystemVolume, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildSliderLabel(Localize("EFECTOS GLITCH", "GLITCH FX"), _effectsVolume), Action = () => AdjustEffectsVolume(1), AdjustAction = AdjustEffectsVolume, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildAudioEnabledLabel(), Action = ToggleAudioEnabled });
                    _settingsItems.Add(new MenuItem { Label = Localize("VOLVER A CATEGORIAS", "BACK TO CATEGORIES"), Action = OpenSettingsCategoriesFromSubPage });
                    break;

                case SettingsPage.Video:
                    _settingsItems.Add(new MenuItem { Label = BuildQualityLabel(), Action = CycleQuality, AdjustAction = AdjustQuality, PreferAdjustWithHorizontal = true });
                    _settingsItems.Add(new MenuItem { Label = BuildScanlinesLabel(), Action = ToggleScanlines });
                    _settingsItems.Add(new MenuItem { Label = BuildChromaticLabel(), Action = ToggleChromatic });
                    _settingsItems.Add(new MenuItem { Label = BuildFlickerLabel(), Action = CycleFlicker });
                    _settingsItems.Add(new MenuItem { Label = Localize("VOLVER A CATEGORIAS", "BACK TO CATEGORIES"), Action = OpenSettingsCategoriesFromSubPage });
                    break;

                case SettingsPage.Controls:
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("MOVER ARRIBA", "MOVE UP"), _navUpKey), Action = () => BeginRebind(RebindTarget.Up) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("MOVER ABAJO", "MOVE DOWN"), _navDownKey), Action = () => BeginRebind(RebindTarget.Down) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("AJUSTAR IZQ", "ADJUST LEFT"), _navLeftKey), Action = () => BeginRebind(RebindTarget.Left) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("AJUSTAR DER", "ADJUST RIGHT"), _navRightKey), Action = () => BeginRebind(RebindTarget.Right) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("ACEPTAR", "ACCEPT"), _submitKey), Action = () => BeginRebind(RebindTarget.Submit) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("ATRAS", "BACK"), _backKey), Action = () => BeginRebind(RebindTarget.Back) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("AVANZAR", "MOVE FORWARD"), GlobalInputBindings.GetKey(GameInputAction.MoveForward)), Action = () => BeginRebind(RebindTarget.MoveForward) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("RETROCEDER", "MOVE BACKWARD"), GlobalInputBindings.GetKey(GameInputAction.MoveBackward)), Action = () => BeginRebind(RebindTarget.MoveBackward) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("MOVER IZQ", "MOVE LEFT"), GlobalInputBindings.GetKey(GameInputAction.MoveLeft)), Action = () => BeginRebind(RebindTarget.MoveLeft) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("MOVER DER", "MOVE RIGHT"), GlobalInputBindings.GetKey(GameInputAction.MoveRight)), Action = () => BeginRebind(RebindTarget.MoveRight) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("CORRER", "RUN"), GlobalInputBindings.GetKey(GameInputAction.Run)), Action = () => BeginRebind(RebindTarget.Run) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("INTERACTUAR", "INTERACT"), GlobalInputBindings.GetKey(GameInputAction.Interact)), Action = () => BeginRebind(RebindTarget.Interact) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("INSPECCIONAR", "INSPECT"), GlobalInputBindings.GetKey(GameInputAction.Inspect)), Action = () => BeginRebind(RebindTarget.Inspect) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("CAMARA", "CAMERA"), GlobalInputBindings.GetKey(GameInputAction.Camera)), Action = () => BeginRebind(RebindTarget.Camera) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("LIBRETA", "NOTEBOOK"), GlobalInputBindings.GetKey(GameInputAction.Notebook)), Action = () => BeginRebind(RebindTarget.Notebook) });
                    _settingsItems.Add(new MenuItem { Label = BuildControlLabel(Localize("PAUSA", "PAUSE"), GlobalInputBindings.GetKey(GameInputAction.Pause)), Action = () => BeginRebind(RebindTarget.Pause) });
                    _settingsItems.Add(new MenuItem { Label = Localize("VOLVER A CATEGORIAS", "BACK TO CATEGORIES"), Action = OpenSettingsCategoriesFromSubPage });
                    break;
            }

            if (resetSelection)
            {
                _settingsIndex = 0;
                _settingsScrollOffset = 0;
            }
            else
            {
                _settingsIndex = Mathf.Clamp(_settingsIndex, 0, Mathf.Max(0, _settingsItems.Count - 1));
                ClampSettingsScrollToSelection();
            }

            UpdateSettingsContextText();
            if (refreshImmediately)
            {
                RefreshMenuVisuals();
            }
        }

        private void OpenSettingsCategories()
        {
            _state = MenuState.Settings;
            PlayUiSound(_menuOpenClip, _interfaceVolume);
            _settingsRoot.gameObject.SetActive(true);
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 1f;
            RebuildSettingsPage(SettingsPage.Categories, true, false);
            ShowMenuState(_settingsTexts, _settingsItems, _settingsIndex, hideRoot: _mainMenuRoot, showRoot: _settingsRoot);
        }

        private void OpenSettingsSubPage(SettingsPage page)
        {
            RebuildSettingsPage(page, true, false);
            ShowMenuState(_settingsTexts, _settingsItems, _settingsIndex, showRoot: _settingsRoot);
        }

        private void OpenSettingsCategoriesFromSubPage()
        {
            RebuildSettingsPage(SettingsPage.Categories, true, false);
            ShowMenuState(_settingsTexts, _settingsItems, _settingsIndex, showRoot: _settingsRoot);
        }

        private void UpdateSettingsContextText()
        {
            _subtitleText.text = _settingsPage switch
            {
                SettingsPage.Categories => GetSettingsSubtitle(),
                SettingsPage.General => Localize("GENERAL // AJUSTES DEL TERMINAL", "GENERAL // TERMINAL SETTINGS"),
                SettingsPage.Audio => Localize("AUDIO // MEZCLA Y VOLUMENES", "AUDIO // MIX AND LEVELS"),
                SettingsPage.Video => Localize("VIDEO // CRT Y RENDER", "VIDEO // CRT AND RENDER"),
                SettingsPage.Controls => Localize("CONTROLES // REASIGNACION", "CONTROLS // REBINDING"),
                _ => GetSettingsSubtitle()
            };

            _promptText.text = string.Empty;
            _footerText.text = GetSettingsFooter();
            SetStatus(GetLocalizedStatusSettings());
        }

        private void MoveSelection(List<MenuItem> items, ref int index, int direction)
        {
            if (items.Count == 0)
            {
                return;
            }

            int safety = 0;
            do
            {
                index = (index + direction + items.Count) % items.Count;
                safety++;
            }
            while ((!items[index].Enabled || items[index].Hidden) && safety < items.Count + 1);
        }

        private void EnsureSelectableIndex(List<MenuItem> items, ref int index)
        {
            if (items.Count == 0)
            {
                index = 0;
                return;
            }

            index = Mathf.Clamp(index, 0, items.Count - 1);
            if (!items[index].Enabled || items[index].Hidden)
            {
                MoveSelection(items, ref index, 1);
            }
        }

        private void ClampSettingsScrollToSelection()
        {
            int visibleCount = Mathf.Max(1, _settingsTexts.Count);
            if (_settingsIndex < _settingsScrollOffset)
            {
                _settingsScrollOffset = _settingsIndex;
            }
            else if (_settingsIndex >= _settingsScrollOffset + visibleCount)
            {
                _settingsScrollOffset = _settingsIndex - visibleCount + 1;
            }

            _settingsScrollOffset = Mathf.Clamp(_settingsScrollOffset, 0, Mathf.Max(0, _settingsItems.Count - visibleCount));
        }

        private void ClampLevelScrollToSelection()
        {
            int visibleCount = Mathf.Max(1, _settingsTexts.Count);
            if (_levelIndex < _levelScrollOffset)
            {
                _levelScrollOffset = _levelIndex;
            }
            else if (_levelIndex >= _levelScrollOffset + visibleCount)
            {
                _levelScrollOffset = _levelIndex - visibleCount + 1;
            }

            _levelScrollOffset = Mathf.Clamp(_levelScrollOffset, 0, Mathf.Max(0, _settingsItems.Count - visibleCount));
        }

        private bool UpdateSettingsSelectionFromPointer(ref int index)
        {
            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            for (int slot = 0; slot < _settingsTexts.Count; slot++)
            {
                TMP_Text text = _settingsTexts[slot];
                int itemIndex = _settingsScrollOffset + slot;
                if (text == null || !text.gameObject.activeSelf || itemIndex >= _settingsItems.Count)
                {
                    continue;
                }

                if (!_settingsItems[itemIndex].Enabled)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, mousePosition, eventCamera))
                {
                    index = itemIndex;
                    ClampSettingsScrollToSelection();
                    RefreshMenuVisuals();
                    return true;
                }
            }

            return false;
        }

        private bool UpdateLevelSelectionFromPointer(ref int index)
        {
            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            for (int slot = 0; slot < _settingsTexts.Count; slot++)
            {
                TMP_Text text = _settingsTexts[slot];
                int itemIndex = _levelScrollOffset + slot;
                if (text == null || !text.gameObject.activeSelf || itemIndex >= _settingsItems.Count)
                {
                    continue;
                }

                if (!_settingsItems[itemIndex].Enabled)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(text.rectTransform, mousePosition, eventCamera))
                {
                    index = itemIndex;
                    ClampLevelScrollToSelection();
                    RefreshLevelBrowserVisuals();
                    return true;
                }
            }

            return false;
        }

        private int GetPointerAdjustDirection()
        {
            TMP_Text selectedText = GetVisibleSettingsTextForIndex(_settingsIndex);
            if (selectedText == null || Mouse.current == null)
            {
                return 0;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(selectedText.rectTransform, mousePosition, eventCamera))
            {
                return 0;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(selectedText.rectTransform, mousePosition, eventCamera, out Vector2 localPoint);
            return localPoint.x >= 0f ? 1 : -1;
        }

        private bool TryAdjustSelectedSetting(int direction)
        {
            if (_settingsIndex < 0 || _settingsIndex >= _settingsItems.Count)
            {
                return false;
            }

            MenuItem item = _settingsItems[_settingsIndex];
            if (item.AdjustAction == null)
            {
                return false;
            }

            item.AdjustAction.Invoke(direction);
            return true;
        }

        private void BeginRebind(RebindTarget target)
        {
            _awaitingRebind = true;
            _pendingRebind = target;
            _promptText.text = Localize("PRESIONA UNA TECLA...", "PRESS A KEY...");
            SetStatus(Localize("ESPERANDO NUEVO BIND.", "WAITING FOR NEW BIND."));
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

                Key pressedKey = keyControl.keyCode;
                ApplyRebind(pressedKey);
                return;
            }
        }

        private void ApplyRebind(Key key)
        {
            switch (_pendingRebind)
            {
                case RebindTarget.Up: _navUpKey = key; PlayerPrefs.SetInt(PrefBindUp, (int)key); break;
                case RebindTarget.Down: _navDownKey = key; PlayerPrefs.SetInt(PrefBindDown, (int)key); break;
                case RebindTarget.Left: _navLeftKey = key; PlayerPrefs.SetInt(PrefBindLeft, (int)key); break;
                case RebindTarget.Right: _navRightKey = key; PlayerPrefs.SetInt(PrefBindRight, (int)key); break;
                case RebindTarget.Submit: _submitKey = key; PlayerPrefs.SetInt(PrefBindSubmit, (int)key); break;
                case RebindTarget.Back: _backKey = key; PlayerPrefs.SetInt(PrefBindBack, (int)key); break;
                case RebindTarget.MoveForward: GlobalInputBindings.SetKey(GameInputAction.MoveForward, key); break;
                case RebindTarget.MoveBackward: GlobalInputBindings.SetKey(GameInputAction.MoveBackward, key); break;
                case RebindTarget.MoveLeft: GlobalInputBindings.SetKey(GameInputAction.MoveLeft, key); break;
                case RebindTarget.MoveRight: GlobalInputBindings.SetKey(GameInputAction.MoveRight, key); break;
                case RebindTarget.Run: GlobalInputBindings.SetKey(GameInputAction.Run, key); break;
                case RebindTarget.Interact: GlobalInputBindings.SetKey(GameInputAction.Interact, key); break;
                case RebindTarget.Inspect:
                    GlobalInputBindings.SetKey(GameInputAction.Inspect, key);
                    GlobalInputBindings.SetKey(GameInputAction.ReleaseInspect, key);
                    break;
                case RebindTarget.Camera: GlobalInputBindings.SetKey(GameInputAction.Camera, key); break;
                case RebindTarget.Notebook: GlobalInputBindings.SetKey(GameInputAction.Notebook, key); break;
                case RebindTarget.Pause: GlobalInputBindings.SetKey(GameInputAction.Pause, key); break;
            }

            PlayerPrefs.Save();
            _awaitingRebind = false;
            _pendingRebind = RebindTarget.None;
            _promptText.text = string.Empty;
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"CONTROL CAMBIADO A {key}.", $"CONTROL SET TO {key}."));
        }

        private bool UpdateSelectionFromPointer(List<TMP_Text> texts, List<MenuItem> items, ref int index)
        {
            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            for (int i = 0; i < texts.Count; i++)
            {
                if (i >= items.Count || texts[i] == null || !texts[i].gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!items[i].Enabled || items[i].Hidden)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(texts[i].rectTransform, mousePosition, eventCamera))
                {
                    index = i;
                    RefreshMenuVisuals();
                    return true;
                }
            }

            return false;
        }

        private int ReadVerticalNavigation()
        {
            if (WasKeyPressed(_navUpKey) || (Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame))
            {
                return -1;
            }

            if (WasKeyPressed(_navDownKey) || (Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame))
            {
                return 1;
            }

            return 0;
        }

        private static bool WasPointerClicked()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private bool WasSubmitPressedCustom()
        {
            return WasKeyPressed(_submitKey) ||
                   (Keyboard.current != null &&
                    (Keyboard.current.enterKey.wasPressedThisFrame ||
                     Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                     Keyboard.current.spaceKey.wasPressedThisFrame));
        }

        private bool WasBackPressed()
        {
            return WasKeyPressed(_backKey) ||
                   (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        }

        private int ReadHorizontalNavigation()
        {
            if (WasKeyPressed(_navLeftKey) || (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame))
            {
                return -1;
            }

            if (WasKeyPressed(_navRightKey) || (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame))
            {
                return 1;
            }

            return 0;
        }

        private bool WasKeyPressed(Key key)
        {
            if (Keyboard.current == null || key == Key.None)
            {
                return false;
            }

            KeyControl control = Keyboard.current[key];
            return control != null && control.wasPressedThisFrame;
        }

        private void NormalizeSettingsData()
        {
            if (_languageOptions == null || _languageOptions.Length == 0)
            {
                _languageOptions = new[] { "ESPANOL", "ENGLISH" };
            }

            _settingsVisibleRows = Mathf.Max(3, _settingsVisibleRows);
            _audioSliderStep = Mathf.Clamp(_audioSliderStep, 0.01f, 0.25f);
            _sliderSegments = Mathf.Clamp(_sliderSegments, 6, 20);
            _archiveCount = Mathf.Clamp(_archiveCount, 1, 16);
        }

        private void LoadPreferences()
        {
            _audioEnabled = PlayerPrefs.GetInt(PrefAudioEnabled, _audioEnabled ? 1 : 0) == 1;
            _navUpKey = (Key)PlayerPrefs.GetInt(PrefBindUp, (int)_navUpKey);
            _navDownKey = (Key)PlayerPrefs.GetInt(PrefBindDown, (int)_navDownKey);
            _navLeftKey = (Key)PlayerPrefs.GetInt(PrefBindLeft, (int)_navLeftKey);
            _navRightKey = (Key)PlayerPrefs.GetInt(PrefBindRight, (int)_navRightKey);
            _submitKey = (Key)PlayerPrefs.GetInt(PrefBindSubmit, (int)_submitKey);
            _backKey = (Key)PlayerPrefs.GetInt(PrefBindBack, (int)_backKey);
            _masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume, _masterVolume);
            _interfaceVolume = PlayerPrefs.GetFloat(PrefInterfaceVolume, _interfaceVolume);
            _systemVolume = PlayerPrefs.GetFloat(PrefSystemVolume, _systemVolume);
            _effectsVolume = PlayerPrefs.GetFloat(PrefEffectsVolume, _effectsVolume);
            _subtitlesEnabled = PlayerPrefs.GetInt(PrefSubtitlesEnabled, _subtitlesEnabled ? 1 : 0) == 1;
            _scanlinesEnabled = PlayerPrefs.GetInt(PrefScanlinesEnabled, _scanlinesEnabled ? 1 : 0) == 1;
            _chromaticEnabled = PlayerPrefs.GetInt(PrefChromaticEnabled, _chromaticEnabled ? 1 : 0) == 1;
            _languageIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefLanguageIndex, _languageIndex), 0, _languageOptions.Length - 1);
            int availableArchiveCount = GetEffectiveArchiveCount();
            _unlockedArchive = Mathf.Clamp(PlayerPrefs.GetInt(PrefUnlockedArchive, 1), 1, Mathf.Max(1, availableArchiveCount));
            _mountedArchive = Mathf.Clamp(PlayerPrefs.GetInt(PrefMountedArchive, -1), -1, availableArchiveCount - 1);

            if (_clearMountedArchiveOnStartup)
            {
                _mountedArchive = -1;
                PlayerPrefs.SetInt(PrefMountedArchive, _mountedArchive);
                PlayerPrefs.Save();
            }

            string[] qualityNames = QualitySettings.names;
            if (qualityNames != null && qualityNames.Length > 0)
            {
                int qualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefQualityIndex, QualitySettings.GetQualityLevel()), 0, qualityNames.Length - 1);
                QualitySettings.SetQualityLevel(qualityIndex, true);
            }

            ApplyRuntimeSettings();
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetInt(PrefAudioEnabled, _audioEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PrefMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(PrefInterfaceVolume, _interfaceVolume);
            PlayerPrefs.SetFloat(PrefSystemVolume, _systemVolume);
            PlayerPrefs.SetFloat(PrefEffectsVolume, _effectsVolume);
            PlayerPrefs.SetInt(PrefSubtitlesEnabled, _subtitlesEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefLanguageIndex, _languageIndex);
            PlayerPrefs.SetInt(PrefQualityIndex, QualitySettings.GetQualityLevel());
            PlayerPrefs.SetInt(PrefScanlinesEnabled, _scanlinesEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefChromaticEnabled, _chromaticEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyRuntimeSettings()
        {
            _masterVolume = Mathf.Clamp01(_masterVolume);
            _interfaceVolume = Mathf.Clamp01(_interfaceVolume);
            _systemVolume = Mathf.Clamp01(_systemVolume);
            _effectsVolume = Mathf.Clamp01(_effectsVolume);
            ToggleOverlayGraphic("Scanlines", _scanlinesEnabled);
            ToggleOverlayGraphic("RgbMask", _chromaticEnabled);
        }

        private void QuitGame()
        {
            RuntimeConfirmationDialog.Show(
                Localize("CONFIRMAR SALIDA", "CONFIRM QUIT"),
                Localize("Vas a cerrar el juego. Los datos de gameplay guardados se conservaran.", "You are about to close the game. Saved gameplay data will be kept."),
                Localize("SALIR", "QUIT"),
                Localize("CANCELAR", "CANCEL"),
                QuitGameConfirmed);
        }

        private void QuitGameConfirmed()
        {
            SetStatus("TERMINATING SESSION.");
            GameSaveSystem.MarkOfficeContext();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ConfirmDeleteAllData()
        {
            RuntimeConfirmationDialog.Show(
                Localize("ELIMINAR DATOS", "DELETE DATA"),
                Localize("Esto borra evidencias, notas, pizarra, conexiones, conclusiones y progreso de gameplay. Las opciones no se borran.", "This deletes evidence, notes, board, connections, conclusions and gameplay progress. Settings are not deleted."),
                Localize("ELIMINAR", "DELETE"),
                Localize("CANCELAR", "CANCEL"),
                DeleteAllDataConfirmed);
        }

        private void DeleteAllDataConfirmed()
        {
            GameSaveSystem.DeleteAllGameplayData();
            SetStatus(Localize("DATOS DE GAMEPLAY ELIMINADOS.", "GAMEPLAY DATA DELETED."));
            RefreshMainMenuAvailability();
        }

        private void ToggleAudioEnabled()
        {
            _audioEnabled = !_audioEnabled;
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(_audioEnabled ? Localize("SALIDA DE AUDIO ACTIVADA.", "AUDIO OUTPUT ENABLED.") : Localize("SALIDA DE AUDIO DESACTIVADA.", "AUDIO OUTPUT DISABLED."));
        }

        private void AdjustMasterVolume(int direction)
        {
            _masterVolume = StepSlider(_masterVolume, direction);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"VOLUMEN MAESTRO {GetPercentLabel(_masterVolume)}.", $"MASTER VOLUME {GetPercentLabel(_masterVolume)}."));
        }

        private void AdjustInterfaceVolume(int direction)
        {
            _interfaceVolume = StepSlider(_interfaceVolume, direction);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"SONIDOS DE MENU {GetPercentLabel(_interfaceVolume)}.", $"MENU SOUND VOLUME {GetPercentLabel(_interfaceVolume)}."));
        }

        private void AdjustSystemVolume(int direction)
        {
            _systemVolume = StepSlider(_systemVolume, direction);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"SISTEMA CRT {GetPercentLabel(_systemVolume)}.", $"CRT SYSTEM VOLUME {GetPercentLabel(_systemVolume)}."));
        }

        private void AdjustEffectsVolume(int direction)
        {
            _effectsVolume = StepSlider(_effectsVolume, direction);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"EFECTOS GLITCH {GetPercentLabel(_effectsVolume)}.", $"GLITCH FX VOLUME {GetPercentLabel(_effectsVolume)}."));
        }

        private void CycleQuality()
        {
            AdjustQuality(1);
        }

        private void AdjustQuality(int direction)
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                SetStatus(Localize("NO HAY PRESETS DE CALIDAD DISPONIBLES.", "NO QUALITY PRESETS AVAILABLE."));
                return;
            }

            int nextIndex = QualitySettings.GetQualityLevel() + direction;
            if (nextIndex < 0)
            {
                nextIndex = qualityNames.Length - 1;
            }
            else if (nextIndex >= qualityNames.Length)
            {
                nextIndex = 0;
            }

            QualitySettings.SetQualityLevel(nextIndex, true);
            PlayerPrefs.SetInt("global.pause.quality", nextIndex);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"CALIDAD CAMBIADA A {GetQualityLabel()}.", $"QUALITY SET TO {GetQualityLabel()}."));
        }

        private void ToggleSubtitles()
        {
            _subtitlesEnabled = !_subtitlesEnabled;
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(_subtitlesEnabled ? Localize("SUBTITULOS ACTIVADOS.", "SUBTITLES ENABLED.") : Localize("SUBTITULOS DESACTIVADOS.", "SUBTITLES DISABLED."));
        }

        private void CycleLanguage()
        {
            AdjustLanguage(1);
        }

        private void AdjustLanguage(int direction)
        {
            int count = Mathf.Max(1, _languageOptions.Length);
            _languageIndex = (_languageIndex + direction + count) % count;
            RefreshLocalizedLabels();
            SavePreferences();
            SetStatus(Localize("IDIOMA DEL TERMINAL ACTUALIZADO.", "TERMINAL LANGUAGE UPDATED."));
        }

        private void CycleFlicker()
        {
            if (_flickerStrength < 0.2f)
            {
                _flickerStrength = 0.45f;
            }
            else if (_flickerStrength < 0.5f)
            {
                _flickerStrength = 0.75f;
            }
            else
            {
                _flickerStrength = 0.12f;
            }

            RebuildSettingsPage(_settingsPage, false);
            SetStatus(Localize($"PERFIL DE FLICKER {GetFlickerLabel()}.", $"FLICKER PROFILE {GetFlickerLabel()}."));
        }

        private void ToggleScanlines()
        {
            _scanlinesEnabled = !_scanlinesEnabled;
            ToggleOverlayGraphic("Scanlines", _scanlinesEnabled);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(_scanlinesEnabled ? Localize("SCANLINES ACTIVADAS.", "SCANLINES ENABLED.") : Localize("SCANLINES DESACTIVADAS.", "SCANLINES DISABLED."));
        }

        private void ToggleChromatic()
        {
            _chromaticEnabled = !_chromaticEnabled;
            ToggleOverlayGraphic("RgbMask", _chromaticEnabled);
            SavePreferences();
            RebuildSettingsPage(_settingsPage, false);
            SetStatus(_chromaticEnabled ? Localize("ABERRACION RGB ACTIVADA.", "RGB BLEED ENABLED.") : Localize("RGB BLEED DESACTIVADO.", "RGB BLEED DISABLED."));
        }

        private void ToggleOverlayGraphic(string childName, bool active)
        {
            if (_overlayGroup == null)
            {
                return;
            }

            Transform child = _overlayGroup.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private void RefreshLocalizedLabels()
        {
            if (_mainMenuItems.Count >= 4)
            {
                _mainMenuItems[0].Label = GetLocalizedMainMenuLabel(0);
                _mainMenuItems[1].Label = GetLocalizedMainMenuLabel(1);
                _mainMenuItems[2].Label = GetLocalizedMainMenuLabel(2);
                _mainMenuItems[3].Label = GetLocalizedMainMenuLabel(3);
            }

            RebuildSettingsPage(_settingsPage, false);

            if (_state == MenuState.MainMenu)
            {
                _footerText.text = GetMainMenuFooter();
            }
            else if (_state == MenuState.Settings)
            {
                UpdateSettingsContextText();
            }

            RefreshMenuVisuals();
        }

        private void OpenLevelBrowser()
        {
            _state = MenuState.LevelSelect;
            _settingsItems.Clear();
            _levelBrowserShowingMemories = false;
            _selectedCaseIndex = -1;
            int availableArchiveCount = GetEffectiveArchiveCount();
            List<string> caseNames = GetAvailableCaseNames(availableArchiveCount);

            for (int i = 0; i < caseNames.Count; i++)
            {
                int caseIndex = i;
                string caseName = caseNames[i];
                int memoryCount = GetMemoryCountForCase(caseName, availableArchiveCount);
                int unlockedCount = GetUnlockedMemoryCountForCase(caseName, availableArchiveCount);
                bool unlocked = unlockedCount > 0;
                string prefix = unlocked ? "[CASE]" : "[LOCK]";
                _settingsItems.Add(new MenuItem
                {
                    Label = $"{prefix} {caseName} ({unlockedCount:00}/{memoryCount:00})",
                    Enabled = unlocked,
                    Action = () => OpenMemoryBrowser(caseIndex)
                });
            }

            if (_settingsItems.Count == 0)
            {
                _settingsItems.Add(new MenuItem
                {
                    Label = Localize("[VACIO] NO HAY EXPEDIENTES EN BUILD SETTINGS", "[EMPTY] NO CASES IN BUILD SETTINGS"),
                    Enabled = false
                });
            }

            _levelIndex = 0;
            EnsureSelectableIndex(_settingsItems, ref _levelIndex);
            _levelScrollOffset = 0;
            _settingsRoot.gameObject.SetActive(true);
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 1f;
            _subtitleText.text = Localize("EXPEDIENTES // HISTORIAS ABIERTAS", "CASE FILES // OPEN STORIES");
            _footerText.text = Localize("NAV: ARRIBA/ABAJO  //  ABRIR: ENTER  //  VOLVER: ATRAS", "NAV: UP/DOWN  //  OPEN: ENTER  //  BACK");
            _diagnosticText.text = Localize($"EXPEDIENTES: {caseNames.Count:00}  //  MEMORIAS: {Mathf.Min(_unlockedArchive, availableArchiveCount):00}/{availableArchiveCount:00}", $"CASES: {caseNames.Count:00}  //  MEMORIES: {Mathf.Min(_unlockedArchive, availableArchiveCount):00}/{availableArchiveCount:00}");
            SetStatus(Localize("SELECCIONA UN EXPEDIENTE.", "SELECT A CASE FILE."));
            ShowMenuState(_settingsTexts, _settingsItems, _levelIndex, hideRoot: _mainMenuRoot, showRoot: _settingsRoot);
        }

        private void RefreshMainMenuAvailability()
        {
        }

        private void OpenMemoryBrowser(int caseIndex)
        {
            int availableArchiveCount = GetEffectiveArchiveCount();
            List<string> caseNames = GetAvailableCaseNames(availableArchiveCount);
            if (caseIndex < 0 || caseIndex >= caseNames.Count)
            {
                SetStatus(Localize("EL EXPEDIENTE SELECCIONADO NO EXISTE.", "THE SELECTED CASE DOES NOT EXIST."));
                return;
            }

            _state = MenuState.LevelSelect;
            _settingsItems.Clear();
            _levelBrowserShowingMemories = true;
            _selectedCaseIndex = caseIndex;

            string selectedCaseName = caseNames[caseIndex];
            for (int i = 0; i < availableArchiveCount; i++)
            {
                int archiveIndex = i;
                if (!string.Equals(GetArchiveCaseName(archiveIndex), selectedCaseName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool unlocked = archiveIndex < _unlockedArchive;
                string archiveName = GetArchiveName(archiveIndex);
                string prefix = unlocked ? "[MEM]" : "[LOCK]";
                _settingsItems.Add(new MenuItem
                {
                    Label = $"{prefix} {archiveName}",
                    Enabled = unlocked,
                    Action = () => MountArchive(archiveIndex)
                });
            }

            if (_settingsItems.Count == 0)
            {
                _settingsItems.Add(new MenuItem
                {
                    Label = Localize("[VACIO] ESTE EXPEDIENTE NO TIENE MEMORIAS", "[EMPTY] THIS CASE HAS NO MEMORIES"),
                    Enabled = false
                });
            }

            _levelIndex = 0;
            EnsureSelectableIndex(_settingsItems, ref _levelIndex);
            _levelScrollOffset = 0;
            _subtitleText.text = Localize($"{selectedCaseName} // MEMORIAS", $"{selectedCaseName} // MEMORIES");
            _footerText.text = Localize("NAV: ARRIBA/ABAJO  //  MONTAR: ENTER  //  EXPEDIENTES: ATRAS", "NAV: UP/DOWN  //  MOUNT: ENTER  //  CASES: BACK");
            _diagnosticText.text = Localize($"MEMORIAS DESBLOQUEADAS: {GetUnlockedMemoryCountForCase(selectedCaseName, availableArchiveCount):00}/{GetMemoryCountForCase(selectedCaseName, availableArchiveCount):00}", $"UNLOCKED MEMORIES: {GetUnlockedMemoryCountForCase(selectedCaseName, availableArchiveCount):00}/{GetMemoryCountForCase(selectedCaseName, availableArchiveCount):00}");
            SetStatus(Localize("SELECCIONA UNA MEMORIA PARA MONTAR.", "SELECT A MEMORY TO MOUNT."));
            ShowMenuState(_settingsTexts, _settingsItems, _levelIndex, hideRoot: _mainMenuRoot, showRoot: _settingsRoot);
        }

        private void MountArchive(int archiveIndex)
        {
            int maxArchiveIndex = GetEffectiveArchiveCount() - 1;
            if (maxArchiveIndex < 0)
            {
                SetStatus(Localize("NO HAY ESCENAS VALIDAS PARA MONTAR.", "THERE ARE NO VALID SCENES TO MOUNT."));
                return;
            }

            int sceneBuildIndex = GetArchiveSceneBuildIndex(archiveIndex);
            if (sceneBuildIndex < 0)
            {
                SetStatus(Localize("EL ARCHIVO SELECCIONADO NO TIENE ESCENA VALIDA.", "THE SELECTED ARCHIVE HAS NO VALID SCENE."));
                return;
            }

            _mountedArchive = Mathf.Clamp(archiveIndex, 0, maxArchiveIndex);
            PlayerPrefs.SetInt(PrefMountedArchive, _mountedArchive);
            PlayerPrefs.Save();
            RefreshMainMenuAvailability();
            ArchiveMounted?.Invoke(_mountedArchive, MountedArchiveName);
            CloseLevelBrowser();
        }

        private void CloseLevelBrowser()
        {
            _state = MenuState.MainMenu;
            _levelBrowserShowingMemories = false;
            _selectedCaseIndex = -1;
            RefreshMainMenuAvailability();
            EnsureSelectableIndex(_mainMenuItems, ref _mainIndex);
            RebuildSettingsPage(SettingsPage.Categories, true, false);
            _mainMenuGroup.alpha = 1f;
            _settingsGroup.alpha = 0f;
            _subtitleText.text = _mainSubtitle;
            _footerText.text = GetMainMenuFooter();
            _diagnosticText.text = "CRT SIGNAL STABLE // ARCHIVE INDEX ONLINE";
            SetStatus(HasMountedArchive ? GetMountedArchiveStatus() : GetLocalizedStatusReturnMain());
            ShowMenuState(_mainMenuTexts, _mainMenuItems, _mainIndex, hideRoot: _settingsRoot, showRoot: _mainMenuRoot);
        }

        private void UpdateSettingsDiagnostic()
        {
            if (_diagnosticText == null || _state != MenuState.Settings)
            {
                return;
            }

            int visibleCount = Mathf.Max(1, _settingsTexts.Count);
            int total = _settingsItems.Count;
            int start = total == 0 ? 0 : _settingsScrollOffset + 1;
            int end = Mathf.Min(total, _settingsScrollOffset + visibleCount);

            _diagnosticText.text = _settingsPage switch
            {
                SettingsPage.Categories => Localize("SELECCIONA UNA CATEGORIA DEL TERMINAL", "SELECT A TERMINAL CATEGORY"),
                SettingsPage.General => $"{Localize("GENERAL", "GENERAL")} // {start:00}-{end:00} / {total:00}",
                SettingsPage.Audio => $"{Localize("AUDIO", "AUDIO")} // {start:00}-{end:00} / {total:00}",
                SettingsPage.Video => $"{Localize("VIDEO", "VIDEO")} // {start:00}-{end:00} / {total:00}",
                SettingsPage.Controls => $"{Localize("CONTROLES", "CONTROLS")} // {start:00}-{end:00} / {total:00}",
                _ => _diagnosticText.text
            };

            if (end < total)
            {
                _diagnosticText.text += $"  {Localize("v MAS", "v MORE")}";
            }
        }

        private string GetSettingsCategoryLabel(SettingsPage page)
        {
            return page switch
            {
                SettingsPage.General => Localize("GENERAL", "GENERAL"),
                SettingsPage.Audio => Localize("AUDIO", "AUDIO"),
                SettingsPage.Video => Localize("VIDEO", "VIDEO"),
                SettingsPage.Controls => Localize("CONTROLES", "CONTROLS"),
                _ => string.Empty
            };
        }

        private string BuildControlLabel(string title, Key key)
        {
            return $"{title} .............. [{GetBindingLabel(key)}]";
        }

        private string GetBindingLabel(Key key)
        {
            return key.ToString().ToUpperInvariant();
        }

        private string BuildAudioEnabledLabel()
        {
            return $"{Localize("SALIDA DE AUDIO", "AUDIO OUTPUT")} ......... {(_audioEnabled ? "ON" : "OFF")}";
        }

        private string BuildLanguageLabel()
        {
            return $"{Localize("IDIOMA", "LANGUAGE")} ................. {GetLanguageLabel()}";
        }

        private string BuildSubtitlesLabel()
        {
            return $"{Localize("SUBTITULOS", "SUBTITLES")} ............. {(_subtitlesEnabled ? "ON" : "OFF")}";
        }

        private string BuildQualityLabel()
        {
            return $"{Localize("CALIDAD", "QUALITY")} ................ {GetQualityLabel()}";
        }

        private string BuildScanlinesLabel()
        {
            return $"{Localize("SCANLINES CRT", "CRT SCANLINES")} ...... {(_scanlinesEnabled ? "ON" : "OFF")}";
        }

        private string BuildChromaticLabel()
        {
            return $"{Localize("SANGRADO RGB", "RGB BLEED")} ........... {(_chromaticEnabled ? "ON" : "OFF")}";
        }

        private string BuildFlickerLabel()
        {
            return $"{Localize("FLICKER", "FLICKER")} ................ {GetFlickerLabel()}";
        }

        private string BuildSliderLabel(string title, float value)
        {
            return $"{title} {BuildSlider(value)} {GetPercentLabel(value)}";
        }

        private string BuildSlider(float value)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value) * _sliderSegments);
            filled = Mathf.Clamp(filled, 0, _sliderSegments);
            return "[" + new string('|', filled) + new string('.', _sliderSegments - filled) + "]";
        }

        private float StepSlider(float value, int direction)
        {
            return Mathf.Clamp01(value + direction * _audioSliderStep);
        }

        private string GetPercentLabel(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private string GetLocalizedMainMenuLabel(int index)
        {
            return index switch
            {
                0 => Localize("EXPEDIENTES", "CASE FILES"),
                1 => Localize("OPCIONES", "SETTINGS"),
                2 => Localize("ELIMINAR TODOS LOS DATOS", "DELETE ALL DATA"),
                3 => Localize("SALIR", "QUIT"),
                _ => string.Empty
            };
        }

        private List<string> GetAvailableCaseNames(int availableArchiveCount)
        {
            List<string> caseNames = new();
            for (int i = 0; i < availableArchiveCount; i++)
            {
                string caseName = GetArchiveCaseName(i);
                bool alreadyAdded = false;
                for (int existing = 0; existing < caseNames.Count; existing++)
                {
                    if (string.Equals(caseNames[existing], caseName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    caseNames.Add(caseName);
                }
            }

            return caseNames;
        }

        private string GetArchiveCaseName(int archiveIndex)
        {
            if (_archiveCaseNames != null && archiveIndex >= 0 && archiveIndex < _archiveCaseNames.Length && !string.IsNullOrWhiteSpace(_archiveCaseNames[archiveIndex]))
            {
                return _archiveCaseNames[archiveIndex].ToUpperInvariant();
            }

            if (_archiveCaseNames != null && _archiveCaseNames.Length > 0 && !string.IsNullOrWhiteSpace(_archiveCaseNames[0]))
            {
                return _archiveCaseNames[0].ToUpperInvariant();
            }

            return "CASE_01";
        }

        private int GetMemoryCountForCase(string caseName, int availableArchiveCount)
        {
            int count = 0;
            for (int i = 0; i < availableArchiveCount; i++)
            {
                if (string.Equals(GetArchiveCaseName(i), caseName, System.StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private int GetUnlockedMemoryCountForCase(string caseName, int availableArchiveCount)
        {
            int count = 0;
            for (int i = 0; i < availableArchiveCount; i++)
            {
                if (i < _unlockedArchive && string.Equals(GetArchiveCaseName(i), caseName, System.StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private string GetArchiveName(int archiveIndex)
        {
            if (_archiveNames != null && archiveIndex >= 0 && archiveIndex < _archiveNames.Length && !string.IsNullOrWhiteSpace(_archiveNames[archiveIndex]))
            {
                return _archiveNames[archiveIndex].ToUpperInvariant();
            }

            return $"ARCHIVE_{archiveIndex + 1:00}.NULL";
        }

        private int GetArchiveSceneBuildIndex(int archiveIndex)
        {
            int buildSceneCount = SceneManager.sceneCountInBuildSettings;

            if (_archiveSceneBuildIndices != null && archiveIndex >= 0 && archiveIndex < _archiveSceneBuildIndices.Length)
            {
                int configuredIndex = _archiveSceneBuildIndices[archiveIndex];
                if (configuredIndex > 0 && configuredIndex < buildSceneCount)
                {
                    return configuredIndex;
                }
            }

            int fallbackIndex = archiveIndex + 1;
            return fallbackIndex > 0 && fallbackIndex < buildSceneCount ? fallbackIndex : -1;
        }

        public bool HasMountedArchive => _mountedArchive >= 0 && _mountedArchive < GetEffectiveArchiveCount() && GetArchiveSceneBuildIndex(_mountedArchive) >= 0;
        public int MountedArchiveIndex => _mountedArchive;
        public int MountedArchiveSceneBuildIndex => HasMountedArchive ? GetArchiveSceneBuildIndex(_mountedArchive) : -1;
        public string MountedArchiveSceneName => HasMountedArchive ? GetArchiveSceneName(_mountedArchive) : string.Empty;
        public string MountedArchiveName => HasMountedArchive ? GetArchiveName(_mountedArchive) : string.Empty;

        public void MarkArchiveCompleted(int archiveIndex)
        {
            int availableArchiveCount = GetEffectiveArchiveCount();
            int completedNumber = Mathf.Clamp(archiveIndex + 1, 1, Mathf.Max(1, availableArchiveCount));
            if (completedNumber >= _unlockedArchive && _unlockedArchive < availableArchiveCount)
            {
                _unlockedArchive = completedNumber + 1;
                PlayerPrefs.SetInt(PrefUnlockedArchive, Mathf.Clamp(_unlockedArchive, 1, availableArchiveCount));
                PlayerPrefs.Save();
            }
        }

        public void ClearMountedArchive()
        {
            _mountedArchive = -1;
            PlayerPrefs.SetInt(PrefMountedArchive, _mountedArchive);
            PlayerPrefs.Save();
        }

        private int GetEffectiveArchiveCount()
        {
            int playableSceneCount = Mathf.Max(0, SceneManager.sceneCountInBuildSettings - 1);
            if (playableSceneCount <= 0)
            {
                return 0;
            }

            return Mathf.Min(_archiveCount, playableSceneCount);
        }

        private string GetArchiveSceneName(int archiveIndex)
        {
            int sceneBuildIndex = GetArchiveSceneBuildIndex(archiveIndex);
            if (sceneBuildIndex < 0)
            {
                return string.Empty;
            }

            string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
            return string.IsNullOrWhiteSpace(scenePath) ? string.Empty : Path.GetFileNameWithoutExtension(scenePath);
        }

        private string GetMountedArchiveStatus()
        {
            return Localize($"{GetArchiveName(_mountedArchive)} MONTADO. USA LOS CASCOS VR.", $"{GetArchiveName(_mountedArchive)} MOUNTED. USE THE VR HEADSET.");
        }

        private string GetLocalizedReturnLabel()
        {
            return Localize("VOLVER AL MENU", "RETURN TO MENU");
        }

        private string GetSettingsSubtitle()
        {
            return Localize("PREFERENCIAS LOCALES // CONSOLA DEL TERMINAL", "LOCAL PREFERENCES // TERMINAL CONSOLE");
        }

        private string GetMainMenuFooter()
        {
            return Localize("NAV: W/S O FLECHAS  //  ACEPTAR: ENTER O CLICK", "NAV: W/S OR ARROWS  //  EXECUTE: ENTER OR CLICK");
        }

        private string GetSettingsFooter()
        {
            return Localize("NAV: W/S  //  AJUSTAR: A/D O CLICK  //  ABRIR: ENTER  //  ATRAS: ESC", "NAV: W/S  //  ADJUST: A/D OR CLICK  //  OPEN: ENTER  //  BACK: ESC");
        }

        private string GetLocalizedStatusUnlocked()
        {
            return Localize("INTERFAZ DEL ARCHIVO DESBLOQUEADA.", "ARCHIVE INTERFACE UNLOCKED.");
        }

        private string GetLocalizedStatusSettings()
        {
            return Localize("AJUSTES LOCALES DISPONIBLES.", "LOCAL TERMINAL SETTINGS AVAILABLE.");
        }

        private string GetLocalizedStatusReturnMain()
        {
            return Localize("VOLVISTE AL DIRECTORIO PRINCIPAL.", "RETURNED TO PRIMARY DIRECTORY.");
        }

        private string GetQualityLabel()
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                return "---";
            }

            int currentIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, qualityNames.Length - 1);
            return qualityNames[currentIndex].ToUpperInvariant();
        }

        private string GetLanguageLabel()
        {
            if (_languageOptions == null || _languageOptions.Length == 0)
            {
                return "ESPANOL";
            }

            return _languageOptions[Mathf.Clamp(_languageIndex, 0, _languageOptions.Length - 1)].ToUpperInvariant();
        }

        private string Localize(string spanish, string english)
        {
            return _languageIndex == 0 ? spanish : english;
        }

        private string GetFlickerLabel()
        {
            if (_flickerStrength < 0.2f)
            {
                return "LOW";
            }

            return _flickerStrength < 0.6f ? "MEDIUM" : "HIGH";
        }

        private void SetStatus(string message)
        {
            _statusText.text = message;
        }

        private string ScrambleText(string source, float corruptionChance)
        {
            const string chars = "X$#!?{}_0123456789ABCDEF/[]";
            char[] copy = source.ToCharArray();

            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] != ' ' && Random.value < corruptionChance)
                {
                    copy[i] = chars[Random.Range(0, chars.Length)];
                }
            }

            return new string(copy);
        }

        private void ConfigureCamera()
        {
            if (!_configureMainCamera)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _cameraBackgroundColor;
        }

        private IEnumerator AnimateMonitorLight(float delay, float duration, float targetIntensity)
        {
            if (_monitorPowerLight == null)
            {
                yield break;
            }

            _monitorPowerLight.enabled = false;
            _monitorPowerLight.intensity = 0f;

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            _monitorPowerLight.enabled = true;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                _monitorPowerLight.intensity = Mathf.Lerp(0f, targetIntensity, EaseOutCubic(t));
                yield return null;
            }

            _monitorPowerLight.intensity = targetIntensity;
        }

        private void SetMonitorLightImmediate(float intensity)
        {
            if (_monitorPowerLight == null)
            {
                return;
            }

            if (_monitorLightRoutine != null)
            {
                StopCoroutine(_monitorLightRoutine);
                _monitorLightRoutine = null;
            }

            _monitorPowerLight.intensity = intensity;
            _monitorPowerLight.enabled = intensity > 0.001f;
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
            {
                AssignProceduralFallbacks();
                return;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            AssignProceduralFallbacks();
        }

        private void AssignProceduralFallbacks()
        {
            if (!_useProceduralRetroSounds)
            {
                return;
            }

            _retroSoundBank = GetComponent<CRTRetroSoundBank>();
            if (_retroSoundBank == null)
            {
                _retroSoundBank = gameObject.AddComponent<CRTRetroSoundBank>();
            }

            if (_bootStartClip == null) _bootStartClip = _retroSoundBank.BootStartClip;
            if (_menuOpenClip == null) _menuOpenClip = _retroSoundBank.MenuOpenClip;
            if (_moveClip == null) _moveClip = _retroSoundBank.MoveClip;
            if (_confirmClip == null) _confirmClip = _retroSoundBank.ConfirmClip;
            if (_backClip == null) _backClip = _retroSoundBank.BackClip;
            if (_glitchClip == null) _glitchClip = _retroSoundBank.GlitchClip;
            if (_shutdownClip == null) _shutdownClip = _retroSoundBank.ShutdownClip;
        }

        private void PlayUiSound(AudioClip clip, float channelVolume = 1f)
        {
            if (!_audioEnabled || _audioSource == null || clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip, Mathf.Clamp01(_masterVolume * channelVolume));
        }

        public void FocusOpenMenu()
        {
            if (_sequenceRunning)
            {
                return;
            }

            if (!_poweredOn)
            {
                _openMainMenuAfterBoot = false;
                StartCoroutine(BootSequence());
                return;
            }

            if (_state == MenuState.AwaitingAccess)
            {
                OpenMainMenu();
            }
        }

        public void SuspendTerminalInteraction()
        {
            if (_state == MenuState.MainMenu || _state == MenuState.Settings || _state == MenuState.LevelSelect)
            {
                _state = _poweredOn ? MenuState.AwaitingAccess : MenuState.PoweredOff;
                _mainMenuGroup.alpha = 0f;
                _settingsGroup.alpha = 0f;
                _mainMenuRoot.gameObject.SetActive(false);
                _settingsRoot.gameObject.SetActive(false);
                _promptText.text = _poweredOn ? _bootPrompt : string.Empty;
                _footerText.text = _poweredOn ? "EXECUTE: ENTER OR CLICK" : string.Empty;
                _subtitleText.text = _poweredOn ? _mainSubtitle : string.Empty;
                _diagnosticText.text = _poweredOn ? "CRT SIGNAL STABLE // ARCHIVE INDEX ONLINE" : _diagnosticText.text;
            }
        }

        public void DisableMonitorCanvasForTransition()
        {
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            if (_contentGroup != null)
            {
                _contentGroup.alpha = 0f;
            }

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
            }
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 size, Color color)
        {
            Image image = CreateImage(name, parent, color, false);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color, bool stretch)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;

            RectTransform rect = image.rectTransform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return image;
        }

        private static CanvasGroup CreateCanvasGroup(string name, RectTransform parent)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<CanvasGroup>();
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private TMP_Text CreateText(string name, RectTransform parent, string content, float fontSize, TextAlignmentOptions anchor, Color color, FontStyles style)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.text = content;
            text.color = color;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private TMP_Text CreateMenuText(RectTransform parent, string content)
        {
            TMP_Text text = CreateText("MenuOption", parent, content, 30, TextAlignmentOptions.MidlineLeft, _accentColor, FontStyles.Bold);
            SetPointRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(470f, 42f));
            return text;
        }

        private static RectTransform CreateLayoutRoot(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetPointRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void CreateMenuPanel(RectTransform parent, Vector2 size)
        {
            Image panel = CreateImage("MenuPanel", parent, new Color(0.06f, 0.14f, 0.13f, 0.18f), false);
            panel.transform.SetAsFirstSibling();
            panel.rectTransform.anchorMin = new Vector2(0f, 1f);
            panel.rectTransform.anchorMax = new Vector2(0f, 1f);
            panel.rectTransform.pivot = new Vector2(0f, 1f);
            panel.rectTransform.anchoredPosition = new Vector2(-22f, 18f);
            panel.rectTransform.sizeDelta = size;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.6f, 0.56f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private void CreateHeaderBand(RectTransform parent)
        {
            Image band = CreateImage("HeaderBand", parent, new Color(0.08f, 0.19f, 0.18f, 0.24f), false);
            SetRect(band.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(38f, -40f), new Vector2(-38f, -112f));
        }

        private void CreateFooterBand(RectTransform parent)
        {
            Image band = CreateImage("FooterBand", parent, new Color(0.08f, 0.19f, 0.18f, 0.18f), false);
            SetRect(band.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(38f, 18f), new Vector2(-38f, 78f));
        }

        private void CreateBackdropGlow(RectTransform parent)
        {
            Image glow = CreateImage("BackdropGlow", parent, new Color(0.1f, 0.42f, 0.38f, 0.2f), false);
            glow.sprite = CreateRadialSprite(256, 0.92f);
            glow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(1800f, 1100f);
        }

        private void CreateVignette(RectTransform parent)
        {
            Image vignette = CreateImage("Vignette", parent, Color.white, true);
            vignette.sprite = CreateVignetteSprite(256);
            vignette.color = new Color(0f, 0f, 0f, 0.95f);
        }

        private void CreateScanlines(RectTransform parent)
        {
            RawImage scanlines = CreateRawImage("Scanlines", parent, GenerateScanlineTexture());
            scanlines.color = new Color(1f, 1f, 1f, 0.46f);
            scanlines.uvRect = new Rect(0f, 0f, 190f, 90f);
        }

        private void CreateRgbMask(RectTransform parent)
        {
            RawImage mask = CreateRawImage("RgbMask", parent, GenerateRgbMaskTexture());
            mask.color = Color.white;
            mask.uvRect = new Rect(0f, 0f, 370f, 1f);
        }

        private void CreateNoiseSpecks(RectTransform parent)
        {
            RawImage noise = CreateRawImage("Noise", parent, GenerateNoiseTexture());
            noise.color = Color.white;
            noise.uvRect = new Rect(0f, 0f, 16f, 9f);
        }

        private static Texture2D GenerateScanlineTexture()
        {
            Texture2D texture = new(4, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                Color line = y % 2 == 0 ? new Color(0f, 0f, 0f, 0.04f) : new Color(0f, 0f, 0f, 0.22f);
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, line);
                }
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return texture;
        }

        private static Texture2D GenerateRgbMaskTexture()
        {
            Texture2D texture = new(6, 1, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                new Color(1f, 0f, 0f, 0.045f),
                new Color(1f, 0f, 0f, 0.02f),
                new Color(0f, 1f, 0f, 0.03f),
                new Color(0f, 1f, 0f, 0.02f),
                new Color(0f, 0.42f, 1f, 0.045f),
                new Color(0f, 0.42f, 1f, 0.02f)
            });
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return texture;
        }

        private static Texture2D GenerateNoiseTexture()
        {
            Texture2D texture = new(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float alpha = Random.value > 0.993f ? Random.Range(0.02f, 0.08f) : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return texture;
        }

        private static RawImage CreateRawImage(string name, RectTransform parent, Texture texture)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage raw = go.GetComponent<RawImage>();
            raw.texture = texture;

            RectTransform rect = raw.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return raw;
        }

        private static void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private void CreateCornerDetails(RectTransform monitor)
        {
            TMP_Text label = CreateText("MonitorLabel", monitor, "ARCHIVE NULL  //  CRT-77 RESTORATION UNIT", 18, TextAlignmentOptions.Center, new Color(0.58f, 0.58f, 0.58f, 1f), FontStyles.Normal);
            SetPointRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(500f, 26f));

            Image led = CreateImage("PowerLed", monitor, new Color(0.88f, 0.15f, 0.15f, 1f), false);
            led.sprite = CreateRadialSprite(64, 0.85f);
            led.rectTransform.anchorMin = new Vector2(1f, 0f);
            led.rectTransform.anchorMax = new Vector2(1f, 0f);
            led.rectTransform.pivot = new Vector2(1f, 0f);
            led.rectTransform.anchoredPosition = new Vector2(-38f, 30f);
            led.rectTransform.sizeDelta = new Vector2(20f, 20f);
            AddShadow(led.gameObject, new Color(1f, 0f, 0f, 0.7f), Vector2.zero);
        }

        private static Sprite CreateRadialSprite(int size, float softness)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new(size / 2f, size / 2f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - Mathf.Pow(distance, softness));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateVignetteSprite(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new(size / 2f, size / 2f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(Mathf.InverseLerp(0.45f, 1f, distance));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            float timer = 0f;
            group.alpha = from;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t));
                yield return null;
            }

            group.alpha = to;
        }

        private static float EaseOutExpo(float value)
        {
            return value >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * value);
        }

        private static float EaseOutCubic(float value)
        {
            return 1f - Mathf.Pow(1f - value, 3f);
        }
    }
}
