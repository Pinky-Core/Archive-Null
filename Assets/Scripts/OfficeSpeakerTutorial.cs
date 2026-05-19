using System.Collections;
using TMPro;
using UnityEngine;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeSpeakerTutorial : MonoBehaviour
    {
        public const string CompletedPref = "archive.office.speaker_tutorial.completed";

        private enum TutorialStep
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
        private sealed class TutorialLine
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
                subtitle = "Bienvenido, Operador 253. Estacion Archive Null en espera.",
                fallbackDuration = 3.5f
            },
            new()
            {
                step = TutorialStep.Movement,
                subtitle = "Antes de abrir un expediente, familiaricese con la oficina. Use W A S D para moverse y el mouse para mirar.",
                fallbackDuration = 5f
            },
            new()
            {
                step = TutorialStep.SitPrompt,
                subtitle = "Cuando este listo, tome asiento frente al terminal.",
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
        private float activeTime;
        private Coroutine lineRoutine;

        private void Awake()
        {
            if (resetTutorialOnStart)
            {
                PlayerPrefs.DeleteKey(CompletedPref);
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

            SetSubtitleVisible(false, true);
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
                    if (hasFocusedTerminal && cameraFocus != null && cameraFocus.IsInFarPose)
                    {
                        PlayStep(TutorialStep.ReturnToFar);
                    }
                    break;
                case TutorialStep.ReturnToFar:
                    if (mainMenuController != null && mainMenuController.HasMountedArchive)
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
