using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeSpeakerTutorial : MonoBehaviour
    {
        public const string CompletedPref = "archive.office.speaker_tutorial.completed";

        public enum TutorialStep
        {
            Welcome,
            Movement,
            SitPrompt,
            TerminalReady,
            TerminalFocus,
            ReturnToFar,
            MemoryMounted,
            VrEquipped,
            Completed
        }

        [System.Serializable]
        public sealed class TutorialLine
        {
            public TutorialStep step;
            [TextArea(2, 4)] public string subtitle;
            public AudioClip voiceClip;
            [Min(0.5f)] public float fallbackDuration = 3.5f;
        }

        [Header("References")]
        [SerializeField] private CRTMenuCameraFocus cameraFocus;
        [SerializeField] private CRTMainMenuController mainMenuController;
        [SerializeField] private VRHeadsetArchiveStarter vrHeadsetStarter;
        [SerializeField] private AudioSource speakerSource;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private CanvasGroup subtitleGroup;

        [Header("Behaviour")]
        [SerializeField] private bool playOnlyFirstTime = true;
        [SerializeField] private bool alwaysShowInEditor;
        [SerializeField] private bool resetTutorialOnStart;
        [SerializeField] private float lineGap = 0.35f;
        [SerializeField] private float subtitleFadeDuration = 0.2f;
        [SerializeField] private float initialDelay = 0.7f;
        [SerializeField] private float movementPromptDelay = 1.2f;
        [SerializeField] private float sitPromptDelay = 2.2f;

        [Header("Lines")]
        [SerializeField] private TutorialLine[] lines =
        {
            new()
            {
                step = TutorialStep.Welcome,
                subtitle = "Bienvenido, Operador 253. Hay un expediente preliminar sobre la mesa.",
                fallbackDuration = 3.5f
            },
            new()
            {
                step = TutorialStep.Movement,
                subtitle = "Lealo antes de montar una memoria. Use W A S D para moverse, el mouse para mirar y click para interactuar.",
                fallbackDuration = 5f
            },
            new()
            {
                step = TutorialStep.SitPrompt,
                subtitle = "Cuando termine de revisar la carpeta, tome asiento frente al terminal.",
                fallbackDuration = 3.2f
            },
            new()
            {
                step = TutorialStep.TerminalReady,
                subtitle = "Asiento confirmado. La pantalla esta lista para recibir entrada.",
                fallbackDuration = 3.2f
            },
            new()
            {
                step = TutorialStep.TerminalFocus,
                subtitle = "Acceso concedido. Use el terminal para revisar expedientes, opciones y memorias montadas.",
                fallbackDuration = 4.4f
            },
            new()
            {
                step = TutorialStep.ReturnToFar,
                subtitle = "Puede apartarse del monitor para volver a la posicion de espera.",
                fallbackDuration = 3.5f
            },
            new()
            {
                step = TutorialStep.MemoryMounted,
                subtitle = "Memoria montada. El visor esta preparado.",
                fallbackDuration = 3f
            },
            new()
            {
                step = TutorialStep.VrEquipped,
                subtitle = "Sincronizacion iniciada. Cuando este preparado, ejecute la memoria.",
                fallbackDuration = 3.6f
            }
        };

        private TutorialStep currentStep = TutorialStep.Welcome;
        private bool isPlayingLine;
        private bool hasFocusedTerminal;
        private bool hasReturnedToFarAfterFocus;
        private bool hasMountedMemory;
        private float activeTime;
        private Coroutine lineRoutine;

        private void Awake()
        {
            if (resetTutorialOnStart)
            {
                PlayerPrefs.DeleteKey(CompletedPref);
            }

            if (!PlayerAssistanceSettings.ShouldShowHelp)
            {
                enabled = false;
                return;
            }

            if (speakerSource == null)
            {
                speakerSource = GetComponent<AudioSource>();
                if (speakerSource == null)
                {
                    speakerSource = gameObject.AddComponent<AudioSource>();
                }
            }

            speakerSource.playOnAwake = false;
            speakerSource.loop = false;

            if (cameraFocus == null)
            {
                cameraFocus = FindObjectOfType<CRTMenuCameraFocus>();
            }

            if (mainMenuController == null)
            {
                mainMenuController = FindObjectOfType<CRTMainMenuController>();
            }

            if (vrHeadsetStarter == null)
            {
                vrHeadsetStarter = FindObjectOfType<VRHeadsetArchiveStarter>();
            }

            if (subtitleGroup == null && subtitleText != null)
            {
                subtitleGroup = subtitleText.GetComponentInParent<CanvasGroup>();
            }

            if (subtitleText == null)
            {
                CreateRuntimeSubtitleUi();
            }

            SubscribeToSceneEvents();
            PlayerAssistanceSettings.HelpEnabledChanged += HandleHelpEnabledChanged;
            SetSubtitleVisible(false, true);
        }

        private void OnDestroy()
        {
            PlayerAssistanceSettings.HelpEnabledChanged -= HandleHelpEnabledChanged;
            UnsubscribeFromSceneEvents();
        }

        private void HandleHelpEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                return;
            }

            if (lineRoutine != null)
            {
                StopCoroutine(lineRoutine);
                lineRoutine = null;
            }

            if (speakerSource != null)
            {
                speakerSource.Stop();
            }

            SetSubtitleVisible(false, true);
            this.enabled = false;
        }

        private void SubscribeToSceneEvents()
        {
            if (cameraFocus != null)
            {
                cameraFocus.ReturnedToFar -= HandleReturnedToFar;
                cameraFocus.ReturnedToFar += HandleReturnedToFar;
            }

            if (mainMenuController != null)
            {
                mainMenuController.ArchiveMounted -= HandleArchiveMounted;
                mainMenuController.ArchiveMounted += HandleArchiveMounted;
            }
        }

        private void UnsubscribeFromSceneEvents()
        {
            if (cameraFocus != null)
            {
                cameraFocus.ReturnedToFar -= HandleReturnedToFar;
            }

            if (mainMenuController != null)
            {
                mainMenuController.ArchiveMounted -= HandleArchiveMounted;
            }
        }

        private void HandleReturnedToFar()
        {
            if (hasFocusedTerminal)
            {
                hasReturnedToFarAfterFocus = true;
            }
        }

        private void HandleArchiveMounted(int archiveIndex, string archiveName)
        {
            hasMountedMemory = true;

            // Once a memory is mounted, the terminal has taught its part of the flow.
            // Prompt the player to leave focus before they already return to Far.
            if (hasFocusedTerminal)
            {
                hasReturnedToFarAfterFocus = true;
            }
        }

        private void CreateRuntimeSubtitleUi()
        {
            GameObject canvasObject = new("OfficeSpeakerTutorialSubtitles", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelObject = new("SubtitlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            Image panel = panelObject.GetComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.58f);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 78f);
            panelRect.sizeDelta = new Vector2(1040f, 92f);

            GameObject textObject = new("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            subtitleText = textObject.GetComponent<TextMeshProUGUI>();
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.fontSize = 28f;
            subtitleText.color = new Color(0.82f, 0.96f, 0.91f, 1f);
            subtitleText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform textRect = subtitleText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 12f);
            textRect.offsetMax = new Vector2(-28f, -12f);

            subtitleGroup = canvasObject.GetComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            subtitleGroup.interactable = false;
            subtitleGroup.blocksRaycasts = false;
        }

        private void Start()
        {
            if (ShouldSkipTutorial())
            {
                enabled = false;
                return;
            }

            StartCoroutine(StartAfterDelay());
        }

        private void Update()
        {
            if (currentStep == TutorialStep.Completed || isPlayingLine)
            {
                return;
            }

            activeTime += Time.deltaTime;

            switch (currentStep)
            {
                case TutorialStep.Movement:
                    if (activeTime >= movementPromptDelay)
                    {
                        PlayStep(TutorialStep.Movement);
                    }
                    break;
                case TutorialStep.SitPrompt:
                    if (cameraFocus != null && cameraFocus.IsInFarPose)
                    {
                        PlayStep(TutorialStep.TerminalReady);
                    }
                    else if (activeTime >= sitPromptDelay)
                    {
                        PlayStep(TutorialStep.SitPrompt);
                    }
                    break;
                case TutorialStep.TerminalReady:
                    if (cameraFocus != null && cameraFocus.IsFocused)
                    {
                        hasFocusedTerminal = true;
                        PlayStep(TutorialStep.TerminalFocus);
                    }
                    break;
                case TutorialStep.TerminalFocus:
                    if (hasFocusedTerminal && hasReturnedToFarAfterFocus)
                    {
                        PlayStep(TutorialStep.ReturnToFar);
                    }
                    break;
                case TutorialStep.ReturnToFar:
                    if (hasMountedMemory || (mainMenuController != null && mainMenuController.HasMountedArchive))
                    {
                        PlayStep(TutorialStep.MemoryMounted);
                    }
                    break;
                case TutorialStep.MemoryMounted:
                    if (vrHeadsetStarter != null && vrHeadsetStarter.IsEquipped)
                    {
                        PlayStep(TutorialStep.VrEquipped);
                    }
                    break;
            }
        }

        private IEnumerator StartAfterDelay()
        {
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            PlayStep(TutorialStep.Welcome);
        }

        private bool ShouldSkipTutorial()
        {
            if (!PlayerAssistanceSettings.ShouldShowHelp)
            {
                return true;
            }

#if UNITY_EDITOR
            if (alwaysShowInEditor)
            {
                return false;
            }
#endif
            return playOnlyFirstTime && PlayerPrefs.GetInt(CompletedPref, 0) == 1;
        }

        private void PlayStep(TutorialStep step)
        {
            if (lineRoutine != null)
            {
                StopCoroutine(lineRoutine);
            }

            lineRoutine = StartCoroutine(PlayLineRoutine(step));
        }

        private IEnumerator PlayLineRoutine(TutorialStep step)
        {
            isPlayingLine = true;
            currentStep = step;
            activeTime = 0f;

            TutorialLine line = GetLine(step);
            if (line != null)
            {
                if (subtitleText != null)
                {
                    subtitleText.text = line.subtitle;
                }

                SetSubtitleVisible(true, false);

                if (line.voiceClip != null && speakerSource != null)
                {
                    speakerSource.Stop();
                    speakerSource.clip = line.voiceClip;
                    speakerSource.Play();
                }

                float duration = line.voiceClip != null ? line.voiceClip.length : GetFallbackDuration(line);
                yield return new WaitForSeconds(duration);
                SetSubtitleVisible(false, false);
            }

            if (lineGap > 0f)
            {
                yield return new WaitForSeconds(lineGap);
            }

            AdvanceFrom(step);
            isPlayingLine = false;
            lineRoutine = null;
        }

        private void AdvanceFrom(TutorialStep completedStep)
        {
            currentStep = completedStep switch
            {
                TutorialStep.Welcome => TutorialStep.Movement,
                TutorialStep.Movement => TutorialStep.SitPrompt,
                TutorialStep.SitPrompt => TutorialStep.SitPrompt,
                TutorialStep.TerminalReady => TutorialStep.TerminalReady,
                TutorialStep.TerminalFocus => TutorialStep.TerminalFocus,
                TutorialStep.ReturnToFar => TutorialStep.ReturnToFar,
                TutorialStep.MemoryMounted => TutorialStep.MemoryMounted,
                TutorialStep.VrEquipped => TutorialStep.Completed,
                _ => currentStep
            };

            activeTime = 0f;

            if (currentStep == TutorialStep.Completed)
            {
                PlayerPrefs.SetInt(CompletedPref, 1);
                PlayerPrefs.Save();
            }
        }

        private TutorialLine GetLine(TutorialStep step)
        {
            if (lines == null)
            {
                return null;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && lines[i].step == step)
                {
                    return lines[i];
                }
            }

            return null;
        }

        private static float GetFallbackDuration(TutorialLine line)
        {
            if (line == null)
            {
                return 2f;
            }

            float textDuration = string.IsNullOrWhiteSpace(line.subtitle) ? 0f : line.subtitle.Length * 0.045f;
            return Mathf.Max(line.fallbackDuration, textDuration, 1.25f);
        }

        private void SetSubtitleVisible(bool visible, bool immediate)
        {
            if (subtitleText == null && subtitleGroup == null)
            {
                return;
            }

            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(true);
            }

            if (subtitleGroup == null)
            {
                if (subtitleText != null)
                {
                    Color color = subtitleText.color;
                    color.a = visible ? 1f : 0f;
                    subtitleText.color = color;
                    subtitleText.gameObject.SetActive(visible);
                }
                return;
            }

            if (immediate || subtitleFadeDuration <= 0f)
            {
                subtitleGroup.alpha = visible ? 1f : 0f;
                subtitleGroup.interactable = false;
                subtitleGroup.blocksRaycasts = false;
                subtitleGroup.gameObject.SetActive(visible);
                return;
            }

            StartCoroutine(FadeSubtitle(visible));
        }

        private IEnumerator FadeSubtitle(bool visible)
        {
            if (subtitleGroup == null)
            {
                yield break;
            }

            subtitleGroup.gameObject.SetActive(true);
            float from = subtitleGroup.alpha;
            float to = visible ? 1f : 0f;
            float timer = 0f;
            while (timer < subtitleFadeDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, subtitleFadeDuration));
                subtitleGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            subtitleGroup.alpha = to;
            subtitleGroup.interactable = false;
            subtitleGroup.blocksRaycasts = false;
            subtitleGroup.gameObject.SetActive(visible);
        }
    }
}
