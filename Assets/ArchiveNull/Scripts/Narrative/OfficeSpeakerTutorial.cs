using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ArchiveNull.Narrative;

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
        [SerializeField] private float sitPromptDelay = 2.2f;

        [Header("Lines")]
        [SerializeField] private TutorialLine[] lines =
        {
            new()
            {
                step = TutorialStep.Welcome,
                subtitle = "Operador 253, se le asignó el caso de Julián Herrera, 41 años. Fue hallado muerto en la sala de su casa familiar, cerrada desde adentro.",
                fallbackDuration = 6f
            },
            new()
            {
                step = TutorialStep.Movement,
                subtitle = "La escena fue clasificada como posible suicidio: un frasco de pastillas y un mensaje final sostienen esa lectura. Hay inconsistencias. Lea el expediente sobre la mesa antes de entrar.",
                fallbackDuration = 7f
            },
            new()
            {
                step = TutorialStep.SitPrompt,
                subtitle = "Su tarea no es confirmar una sospecha. Debe separar evidencia real, circunstancial y plantada. Cuando termine de leer, tome asiento frente al terminal.",
                fallbackDuration = 6f
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
        private bool hasOpenedCaseFile;
        private float activeTime;
        private Coroutine lineRoutine;
        private readonly HashSet<TutorialStep> shownSteps = new();

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
                cameraFocus = FindAnyObjectByType<CRTMenuCameraFocus>();
            }

            if (mainMenuController == null)
            {
                mainMenuController = FindAnyObjectByType<CRTMainMenuController>();
            }

            if (vrHeadsetStarter == null)
            {
                vrHeadsetStarter = FindAnyObjectByType<VRHeadsetArchiveStarter>();
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
            PlayerPrefs.SetInt(CompletedPref, 1);
            PlayerPrefs.Save();

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
            canvas.sortingOrder = 20000;

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
            if (CaseFileReader.IsAnyCaseFileOpen) hasOpenedCaseFile = true;

            switch (currentStep)
            {
                case TutorialStep.Movement:
                    if (GlobalInputBindings.IsPressed(GameInputAction.MoveForward) ||
                        GlobalInputBindings.IsPressed(GameInputAction.MoveBackward) ||
                        GlobalInputBindings.IsPressed(GameInputAction.MoveLeft) ||
                        GlobalInputBindings.IsPressed(GameInputAction.MoveRight))
                    {
                        PlayStep(TutorialStep.Movement);
                    }
                    break;
                case TutorialStep.SitPrompt:
                    if (cameraFocus != null && cameraFocus.IsInFarPose)
                    {
                        PlayStep(TutorialStep.TerminalReady);
                    }
                    else if (hasOpenedCaseFile && !CaseFileReader.IsAnyCaseFileOpen && activeTime >= sitPromptDelay)
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
                        PlayerPrefs.SetInt(CompletedPref, 1);
                        PlayerPrefs.Save();
                        PlayStep(TutorialStep.VrEquipped);
                    }
                    break;
            }
        }

        private IEnumerator StartAfterDelay()
        {
            while (DetectiveFantasyIntro.IsPlaying)
            {
                yield return null;
            }

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
            if (shownSteps.Contains(step))
            {
                return;
            }

            shownSteps.Add(step);
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
                    subtitleText.text = GetLocalizedSubtitle(step, line.subtitle);
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

        private static string GetLocalizedSubtitle(TutorialStep step, string fallback)
        {
            return step switch
            {
                TutorialStep.Welcome => GameLocalization.Text(
                    "Operador 253, se le asignó el caso de Julián Herrera, 41 años. Fue hallado muerto en la sala de su casa familiar, cerrada desde adentro.",
                    "Operator 253, you have been assigned the case of Julián Herrera, age 41. He was found dead in the living room of his family home, locked from the inside."),
                TutorialStep.Movement => GameLocalization.Text(
                    "La escena fue clasificada como posible suicidio: un frasco de pastillas y un mensaje final sostienen esa lectura. Hay inconsistencias. Lea el expediente sobre la mesa antes de entrar.",
                    "The scene was classified as a possible suicide: a pill bottle and a final message support that reading. There are inconsistencies. Read the file on the desk before entering."),
                TutorialStep.SitPrompt => GameLocalization.Text(
                    "Su tarea no es confirmar una sospecha. Debe separar evidencia real, circunstancial y plantada. Cuando termine de leer, tome asiento frente al terminal.",
                    "Your task is not to confirm a suspicion. Separate real, circumstantial, and planted evidence. When you finish reading, sit at the terminal."),
                TutorialStep.TerminalReady => GameLocalization.Text("Asiento confirmado. El terminal está listo.", "Seat confirmed. The terminal is ready."),
                TutorialStep.TerminalFocus => GameLocalization.Text("Use el terminal para revisar expedientes, opciones y memorias montadas.", "Use the terminal to review files, settings, and mounted memories."),
                TutorialStep.ReturnToFar => GameLocalization.Text("Puede apartarse del monitor para volver a la posición de espera.", "You can move away from the monitor and return to the waiting position."),
                TutorialStep.MemoryMounted => GameLocalization.Text("Memoria montada. El visor está preparado.", "Memory mounted. The headset is ready."),
                TutorialStep.VrEquipped => GameLocalization.Text("Sincronización iniciada. Ejecute la memoria cuando esté preparado.", "Synchronization started. Run the memory when ready."),
                _ => fallback
            };
        }

        private static float GetFallbackDuration(TutorialLine line)
        {
            if (line == null)
            {
                return 2f;
            }

            float textDuration = string.IsNullOrWhiteSpace(line.subtitle) ? 0f : 2.5f + line.subtitle.Length * 0.065f;
            return Mathf.Clamp(Mathf.Max(line.fallbackDuration, textDuration, 6f), 6f, 14f);
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
