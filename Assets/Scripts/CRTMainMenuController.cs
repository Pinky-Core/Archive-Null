using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed class CRTMainMenuController : MonoBehaviour
    {
        private const string IdleTitle = "Archive: {null}";
        private const string BootPrompt = "PRESS ENTER OR CLICK TO INITIALIZE";

        private static readonly string[] BootMessages =
        {
            "it remembers",
            "you opened it",
            "not empty"
        };

        private enum MenuState
        {
            Booting,
            AwaitingAccess,
            MainMenu,
            Settings,
            LockedSequence
        }

        private sealed class MenuItem
        {
            public string Label;
            public bool Enabled = true;
            public System.Action Action;
        }

        private readonly List<MenuItem> _mainMenuItems = new();
        private readonly List<MenuItem> _settingsItems = new();
        private readonly List<Text> _mainMenuTexts = new();
        private readonly List<Text> _settingsTexts = new();

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

        private Text _titleText;
        private Text _ghostTitleText;
        private Text _subtitleText;
        private Text _promptText;
        private Text _statusText;
        private Text _footerText;
        private Text _diagnosticText;

        private Font _terminalFont;
        private Vector2 _monitorBasePosition;
        private readonly Color _idleScreenColor = new(0.03f, 0.06f, 0.055f, 1f);
        private readonly Color _screenOnColor = new(0.06f, 0.15f, 0.14f, 1f);
        private readonly Color _accentColor = new(0.78f, 0.96f, 0.92f, 1f);
        private readonly Color _mutedColor = new(0.47f, 0.66f, 0.63f, 1f);
        private readonly Color _dangerColor = new(1f, 0.2f, 0.18f, 1f);

        private bool _sequenceRunning;
        private bool _poweredOn;
        private bool _scanlinesEnabled = true;
        private bool _chromaticEnabled = true;
        private float _flickerStrength = 0.45f;
        private float _flickerTimer;
        private float _idleGlitchTimer;
        private float _promptBlinkTimer;
        private float _glitchOverlayTimer;
        private int _mainIndex;
        private int _settingsIndex;
        private MenuState _state;

        private void Awake()
        {
            BuildInterface();
            ConfigureCamera();
            BuildMenus();
            SetStatus("DISPLAY COLD. WAITING FOR POWER.");
        }

        private void Start()
        {
            StartCoroutine(BootSequence());
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
            _terminalFont = LoadTerminalFont();

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

            _screenGlow = CreateImage("ScreenGlow", _screen, new Color(0.42f, 0.95f, 0.9f, 0.08f), true);
            _screenGlow.sprite = CreateRadialSprite(256, 0.85f);

            _contentGroup = CreateCanvasGroup("ContentGroup", _screen);
            _contentGroup.alpha = 0f;

            RectTransform contentRoot = _contentGroup.transform as RectTransform;

            CreateHeaderBand(contentRoot);
            CreateFooterBand(contentRoot);

            _titleText = CreateText("Title", contentRoot, IdleTitle, 64, TextAnchor.UpperLeft, _accentColor, FontStyle.Bold);
            SetRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(84f, -150f), new Vector2(-360f, -56f));
            AddShadow(_titleText.gameObject, new Color(0.45f, 0.95f, 0.9f, 0.25f), Vector2.zero);

            _ghostTitleText = CreateText("GhostTitle", contentRoot, IdleTitle, 64, TextAnchor.UpperLeft, new Color(1f, 0.08f, 0.08f, 0.08f), FontStyle.Bold);
            SetRect(_ghostTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(90f, -154f), new Vector2(-354f, -60f));

            _subtitleText = CreateText("Subtitle", contentRoot, "RECOVERED TERMINAL // RESTRICTED ACCESS NODE", 22, TextAnchor.UpperLeft, _mutedColor, FontStyle.Normal);
            SetRect(_subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(88f, -212f), new Vector2(-360f, -152f));

            _mainMenuRoot = CreateLayoutRoot("MainMenuRoot", contentRoot, new Vector2(88f, -286f), new Vector2(500f, 280f));
            CreateMenuPanel(_mainMenuRoot, new Vector2(530f, 304f));
            _selectionBar = CreateImage("SelectionBar", _mainMenuRoot, new Color(0.75f, 0.96f, 0.92f, 0.12f), false).rectTransform;
            _selectionBar.SetSiblingIndex(1);

            _settingsRoot = CreateLayoutRoot("SettingsRoot", contentRoot, new Vector2(88f, -286f), new Vector2(760f, 320f));
            CreateMenuPanel(_settingsRoot, new Vector2(790f, 344f));

            _diagnosticText = CreateText("DiagnosticText", contentRoot, "STATUS: STANDBY", 19, TextAnchor.UpperLeft, _mutedColor, FontStyle.Normal);
            SetPointRect(_diagnosticText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-88f, -146f), new Vector2(320f, 150f));
            _diagnosticText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _diagnosticText.verticalOverflow = VerticalWrapMode.Overflow;

            _promptText = CreateText("Prompt", contentRoot, BootPrompt, 24, TextAnchor.MiddleLeft, _accentColor, FontStyle.Normal);
            SetPointRect(_promptText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 118f), new Vector2(440f, 32f));

            _statusText = CreateText("Status", contentRoot, "READY", 18, TextAnchor.MiddleLeft, _mutedColor, FontStyle.Normal);
            SetPointRect(_statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(88f, 70f), new Vector2(520f, 26f));
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _footerText = CreateText("Footer", contentRoot, "NAV: W/S OR ARROWS  //  EXECUTE: ENTER  //  BACK: ESC", 17, TextAnchor.MiddleRight, _mutedColor, FontStyle.Normal);
            SetPointRect(_footerText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-88f, 36f), new Vector2(480f, 22f));
            _footerText.horizontalOverflow = HorizontalWrapMode.Wrap;

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

        private void BuildMenus()
        {
            _mainMenuItems.Clear();
            _settingsItems.Clear();
            _mainMenuTexts.Clear();
            _settingsTexts.Clear();

            _mainMenuItems.Add(new MenuItem
            {
                Label = "NEW GAME",
                Action = StartNewGame
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = "CONTINUE",
                Enabled = false,
                Action = () => SetStatus("NO RECOVERABLE SESSION FOUND.")
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = "SETTINGS",
                Action = OpenSettings
            });
            _mainMenuItems.Add(new MenuItem
            {
                Label = "QUIT",
                Action = QuitGame
            });

            for (int i = 0; i < _mainMenuItems.Count; i++)
            {
                Text option = CreateMenuText(_mainMenuRoot, _mainMenuItems[i].Label);
                option.rectTransform.anchoredPosition = new Vector2(0f, -i * 54f);
                _mainMenuTexts.Add(option);
            }

            _settingsItems.Add(new MenuItem
            {
                Label = string.Empty,
                Action = CycleFlicker
            });
            _settingsItems.Add(new MenuItem
            {
                Label = string.Empty,
                Action = ToggleScanlines
            });
            _settingsItems.Add(new MenuItem
            {
                Label = string.Empty,
                Action = ToggleChromatic
            });
            _settingsItems.Add(new MenuItem
            {
                Label = "POWER CYCLE DISPLAY",
                Action = () => StartCoroutine(PowerCycleFromSettings())
            });
            _settingsItems.Add(new MenuItem
            {
                Label = "RETURN TO MAIN MENU",
                Action = CloseSettings
            });

            RefreshSettingsLabels();

            for (int i = 0; i < _settingsItems.Count; i++)
            {
                Text option = CreateMenuText(_settingsRoot, _settingsItems[i].Label);
                option.rectTransform.anchoredPosition = new Vector2(0f, -i * 50f);
                _settingsTexts.Add(option);
            }

            _mainMenuGroup = _mainMenuRoot.gameObject.AddComponent<CanvasGroup>();
            _settingsGroup = _settingsRoot.gameObject.AddComponent<CanvasGroup>();
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 0f;
            _settingsRoot.gameObject.SetActive(false);
        }

        private IEnumerator BootSequence()
        {
            _sequenceRunning = true;
            _poweredOn = false;
            _state = MenuState.Booting;

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
            _screenImage.color = new Color(0.002f, 0.005f, 0.005f, 1f);
            _screenGlow.color = new Color(0.5f, 1f, 0.95f, 0f);
            _bootLine.color = new Color(1f, 1f, 1f, 0f);
            _bootBloom.color = new Color(0.87f, 1f, 0.98f, 0f);

            yield return new WaitForSeconds(0.1f);

            float timer = 0f;
            const float lineDuration = 0.16f;
            while (timer < lineDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = EaseOutExpo(Mathf.Clamp01(timer / lineDuration));
                _screenImage.color = Color.Lerp(new Color(0.002f, 0.005f, 0.005f, 1f), new Color(0.05f, 0.12f, 0.11f, 1f), t * 0.65f);
                _bootLine.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, t));
                _bootBloom.color = new Color(0.85f, 1f, 0.97f, Mathf.Lerp(0.1f, 0.9f, t));
                _bootLine.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(12f, 1110f, t), Mathf.Lerp(2f, 10f, t));
                _bootBloom.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(120f, 1020f, t), Mathf.Lerp(14f, 54f, t));
                yield return null;
            }

            timer = 0f;
            const float bloomDuration = 0.14f;
            while (timer < bloomDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(timer / bloomDuration));
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
            _titleText.text = IdleTitle;
            _ghostTitleText.text = IdleTitle;
            _subtitleText.text = "RECOVERED TERMINAL // RESTRICTED ACCESS NODE";
            _promptText.text = BootPrompt;
            _statusText.text = "SYSTEM READY // ACCESS GATE LOCKED";
            _diagnosticText.text = "CRT SIGNAL STABLE // ARCHIVE INDEX ONLINE";
            _footerText.text = "EXECUTE: ENTER OR CLICK";
            yield return StartCoroutine(FadeCanvasGroup(_contentGroup, 0f, 1f, 0.18f));

            _poweredOn = true;
            _state = MenuState.AwaitingAccess;
            _sequenceRunning = false;
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
            if (_sequenceRunning || !_poweredOn)
            {
                return;
            }

            bool submit = WasSubmitPressed();
            bool pointerClick = WasPointerClicked();

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
                int direction = ReadVerticalNavigation();
                if (direction != 0)
                {
                    MoveSelection(_mainMenuItems, ref _mainIndex, direction);
                    RefreshMenuVisuals();
                }

                UpdateSelectionFromPointer(_mainMenuTexts, _mainMenuItems, ref _mainIndex);

                if (submit || pointerClick)
                {
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
                    RefreshMenuVisuals();
                }

                UpdateSelectionFromPointer(_settingsTexts, _settingsItems, ref _settingsIndex);

                if (WasBackPressed())
                {
                    CloseSettings();
                    return;
                }

                if (submit || pointerClick)
                {
                    ExecuteItem(_settingsItems[_settingsIndex]);
                }
            }
        }

        private void OpenMainMenu()
        {
            _state = MenuState.MainMenu;
            _mainMenuGroup.alpha = 1f;
            _settingsGroup.alpha = 0f;
            _mainMenuRoot.gameObject.SetActive(true);
            _settingsRoot.gameObject.SetActive(false);
            _mainIndex = Mathf.Clamp(_mainIndex, 0, _mainMenuItems.Count - 1);
            _promptText.text = ">";
            _footerText.text = "NAV: W/S OR ARROWS  //  EXECUTE: ENTER  //  SETTINGS: CLICK";
            SetStatus("ARCHIVE INTERFACE UNLOCKED.");
            RefreshMenuVisuals();
        }

        private void OpenSettings()
        {
            _state = MenuState.Settings;
            _mainMenuRoot.gameObject.SetActive(false);
            _settingsRoot.gameObject.SetActive(true);
            _mainMenuGroup.alpha = 0f;
            _settingsGroup.alpha = 1f;
            _settingsIndex = Mathf.Clamp(_settingsIndex, 0, _settingsItems.Count - 1);
            _subtitleText.text = "DISPLAY CALIBRATION // LOCAL TERMINAL SETTINGS";
            _promptText.text = "SETTINGS MODE";
            _footerText.text = "NAV: W/S OR ARROWS  //  EXECUTE: ENTER  //  BACK: ESC";
            SetStatus("LOCAL DISPLAY PARAMETERS AVAILABLE.");
            RefreshSettingsLabels();
            RefreshMenuVisuals();
        }

        private void CloseSettings()
        {
            _state = MenuState.MainMenu;
            _settingsRoot.gameObject.SetActive(false);
            _mainMenuRoot.gameObject.SetActive(true);
            _settingsGroup.alpha = 0f;
            _mainMenuGroup.alpha = 1f;
            _subtitleText.text = "RECOVERED TERMINAL // RESTRICTED ACCESS NODE";
            _promptText.text = ">";
            _footerText.text = "NAV: W/S OR ARROWS  //  EXECUTE: ENTER  //  SETTINGS: CLICK";
            SetStatus("RETURNED TO PRIMARY DIRECTORY.");
            RefreshMenuVisuals();
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

            for (int i = 0; i < BootMessages.Length; i++)
            {
                yield return StartCoroutine(GlitchTo(BootMessages[i], 0.28f));

                if (i < BootMessages.Length - 1)
                {
                    yield return new WaitForSeconds(0.12f);
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

            yield return StartCoroutine(ShakeMonitor(0.32f, 16f));

            yield return new WaitForSeconds(0.32f);
            _titleText.text = "you shouldn't be here";
            _ghostTitleText.text = _titleText.text;
            yield return new WaitForSeconds(0.22f);
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
                    string title = ScrambleText(IdleTitle, 0.04f);
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
                bool selected = i == _mainIndex && _state == MenuState.MainMenu;
                ApplyMenuItemStyle(_mainMenuTexts[i], _mainMenuItems[i], selected);
            }

            for (int i = 0; i < _settingsTexts.Count; i++)
            {
                bool selected = i == _settingsIndex && _state == MenuState.Settings;
                _settingsTexts[i].text = _settingsItems[i].Label;
                ApplyMenuItemStyle(_settingsTexts[i], _settingsItems[i], selected);
            }

            if (_state == MenuState.MainMenu && _mainMenuTexts.Count > 0)
            {
                PositionSelectionBar(_mainMenuTexts[_mainIndex].rectTransform);
            }
            else if (_state == MenuState.Settings && _settingsTexts.Count > 0)
            {
                PositionSelectionBar(_settingsTexts[_settingsIndex].rectTransform);
            }
        }

        private void ApplyMenuItemStyle(Text text, MenuItem item, bool selected)
        {
            if (!item.Enabled)
            {
                text.color = new Color(0.3f, 0.4f, 0.38f, 1f);
                return;
            }

            text.color = selected ? new Color(0.94f, 1f, 0.97f, 1f) : _accentColor;
        }

        private void PositionSelectionBar(RectTransform target)
        {
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
            if (!item.Enabled || item.Action == null)
            {
                return;
            }

            item.Action.Invoke();
            RefreshMenuVisuals();
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
            while (!items[index].Enabled && safety < items.Count + 1);
        }

        private void UpdateSelectionFromPointer(List<Text> texts, List<MenuItem> items, ref int index)
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            for (int i = 0; i < texts.Count; i++)
            {
                if (!items[i].Enabled)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(texts[i].rectTransform, mousePosition, null))
                {
                    index = i;
                    RefreshMenuVisuals();
                    return;
                }
            }
        }

        private int ReadVerticalNavigation()
        {
            if (Keyboard.current == null)
            {
                return 0;
            }

            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                return -1;
            }

            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                return 1;
            }

            return 0;
        }

        private static bool WasPointerClicked()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private static bool WasSubmitPressed()
        {
            if (Keyboard.current == null)
            {
                return false;
            }

            return Keyboard.current.enterKey.wasPressedThisFrame ||
                   Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                   Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        private static bool WasBackPressed()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private void StartNewGame()
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            if (sceneCount > currentIndex + 1)
            {
                SetStatus($"LOADING SCENE {currentIndex + 1:00}...");
                SceneManager.LoadScene(currentIndex + 1);
                return;
            }

            SetStatus("NO GAMEPLAY SCENE IN BUILD SETTINGS. ADD ONE AFTER THE MENU.");
        }

        private void QuitGame()
        {
            SetStatus("TERMINATING SESSION.");
#if UNITY_EDITOR
            Debug.Log("Quit requested from main menu.");
#else
            Application.Quit();
#endif
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

            RefreshSettingsLabels();
            SetStatus($"FLICKER PROFILE SET TO {GetFlickerLabel()}.");
        }

        private void ToggleScanlines()
        {
            _scanlinesEnabled = !_scanlinesEnabled;
            ToggleOverlayGraphic("Scanlines", _scanlinesEnabled);
            RefreshSettingsLabels();
            SetStatus(_scanlinesEnabled ? "SCANLINES ENABLED." : "SCANLINES DISABLED.");
        }

        private void ToggleChromatic()
        {
            _chromaticEnabled = !_chromaticEnabled;
            ToggleOverlayGraphic("RgbMask", _chromaticEnabled);
            RefreshSettingsLabels();
            SetStatus(_chromaticEnabled ? "CHROMATIC BLEED ENABLED." : "CHROMATIC BLEED DISABLED.");
        }

        private void ToggleOverlayGraphic(string childName, bool active)
        {
            Transform child = _overlayGroup.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private void RefreshSettingsLabels()
        {
            _settingsItems[0].Label = $"FLICKER PROFILE .......... {GetFlickerLabel()}";
            _settingsItems[1].Label = $"SCANLINES ................ {(_scanlinesEnabled ? "ON" : "OFF")}";
            _settingsItems[2].Label = $"CHROMATIC BLEED .......... {(_chromaticEnabled ? "ON" : "OFF")}";

            for (int i = 0; i < _settingsTexts.Count && i < _settingsItems.Count; i++)
            {
                _settingsTexts[i].text = _settingsItems[i].Label;
            }
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
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
        }

        private Font LoadTerminalFont()
        {
            string[] candidates =
            {
                "Consolas",
                "Lucida Console",
                "Courier New"
            };

            Font font = Font.CreateDynamicFontFromOSFont(candidates, 32);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private Text CreateText(string name, RectTransform parent, string content, int fontSize, TextAnchor anchor, Color color, FontStyle style)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = _terminalFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.text = content;
            text.color = color;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Text CreateMenuText(RectTransform parent, string content)
        {
            Text text = CreateText("MenuOption", parent, content, 30, TextAnchor.MiddleLeft, _accentColor, FontStyle.Bold);
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

            RawImage scanlines = CreateRawImage("Scanlines", parent, texture);
            scanlines.color = new Color(1f, 1f, 1f, 0.46f);
            scanlines.uvRect = new Rect(0f, 0f, 190f, 90f);
        }

        private void CreateRgbMask(RectTransform parent)
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

            RawImage mask = CreateRawImage("RgbMask", parent, texture);
            mask.color = Color.white;
            mask.uvRect = new Rect(0f, 0f, 370f, 1f);
        }

        private void CreateNoiseSpecks(RectTransform parent)
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

            RawImage noise = CreateRawImage("Noise", parent, texture);
            noise.color = Color.white;
            noise.uvRect = new Rect(0f, 0f, 16f, 9f);
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
            Text label = CreateText("MonitorLabel", monitor, "ARCHIVE NULL  //  CRT-77 RESTORATION UNIT", 18, TextAnchor.MiddleCenter, new Color(0.58f, 0.58f, 0.58f, 1f), FontStyle.Normal);
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
