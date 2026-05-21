using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceCameraController : MonoBehaviour
    {
        [Header("Capture")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float maxCaptureDistance = 4f;
        [SerializeField] private int capturedPhotoWidth = 1024;
        [SerializeField] private bool createNotebookIfMissing = true;
        [SerializeField] private Key notebookToggleKey = Key.Tab;

        [Header("Optional Custom UI")]
        [SerializeField] private SimpleMessageUI messageUI;
        [SerializeField] private GameObject cameraModeUI;

        [Header("Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cameraOpenClip;
        [SerializeField] private AudioClip cameraCloseClip;
        [SerializeField] private AudioClip shutterClip;
        [SerializeField] private float cameraTransitionDuration = 0.22f;
        [SerializeField] private float captureFeedbackDuration = 0.18f;
        [SerializeField] private float captureCooldown = 0.12f;
        [SerializeField] private float hudOpenScale = 0.965f;
        [SerializeField] private float hudCaptureScale = 1.025f;

        [Header("Generated UI")]
        [SerializeField] private bool createUiIfMissing = true;
        [SerializeField] private string cameraModeLabel = "CAMARA DE EVIDENCIA";
        [SerializeField] private string captureHint = "CLICK: FOTO  //  F: CERRAR";
        [SerializeField] private Color hudColor = new Color(0.78f, 0.96f, 0.92f, 1f);

        public bool IsCameraModeActive { get; private set; }
        public static bool IsAnyCameraModeActive { get; private set; }

        private CanvasGroup hudGroup;
        private RectTransform hudAnimatedRoot;
        private Image captureFlash;
        private Coroutine cameraModeRoutine;
        private Coroutine captureFeedbackRoutine;
        private float nextCaptureTime;
        private readonly RaycastHit[] captureHits = new RaycastHit[24];

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (createUiIfMissing && (messageUI == null || cameraModeUI == null))
            {
                CreateRuntimeUi();
            }

            EnsureAudio();
            EnsureHudAnimationReferences();
            EnsureNotebook();
            SetCameraModeImmediate(false);
        }

        private void OnDisable()
        {
            if (IsCameraModeActive)
            {
                SetCameraModeImmediate(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && !global::InspectObject.IsAnyInspecting)
            {
                SetCameraMode(!IsCameraModeActive);
            }

            if (!IsCameraModeActive)
            {
                return;
            }

            if (EvidenceNotebookUI.IsAnyNotebookOpen)
            {
                return;
            }

            if (global::InspectObject.IsAnyInspecting)
            {
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryCapture();
            }
        }

        private void SetCameraMode(bool active)
        {
            if (IsCameraModeActive == active)
            {
                return;
            }

            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            if (cameraModeRoutine != null)
            {
                StopCoroutine(cameraModeRoutine);
            }

            cameraModeRoutine = StartCoroutine(AnimateCameraMode(active));
        }

        private void TryCapture()
        {
            if (Time.unscaledTime < nextCaptureTime)
            {
                return;
            }

            nextCaptureTime = Time.unscaledTime + captureCooldown;
            PlaySound(shutterClip);
            PlayCaptureFeedback();

            if (playerCamera == null)
            {
                ShowMessage("Camara no disponible.");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!TryGetCaptureTarget(ray, out EvidenceTarget target, out bool blocked))
            {
                ShowMessage(blocked ? "Objetivo no registrable." : "No hay evidencia en foco.");
                return;
            }

            if (!target.CanRegister(out string validationMessage))
            {
                ShowMessage(validationMessage);
                return;
            }

            Sprite capturedPhoto = CaptureCameraPhoto();
            EvidenceData capturedEvidence = target.CreateCapturedEvidence(capturedPhoto);
            bool registered = EvidenceInventory.Instance.RegisterEvidence(capturedEvidence);
            ShowMessage(registered ? "Evidencia registrada: " + capturedEvidence.evidenceName : "Evidencia ya registrada.");
        }

        private bool TryGetCaptureTarget(Ray ray, out EvidenceTarget target, out bool blocked)
        {
            target = null;
            blocked = false;

            int hitCount = Physics.RaycastNonAlloc(ray, captureHits, maxCaptureDistance, ~0, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                return false;
            }

            SortHitsByDistance(captureHits, hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = captureHits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                target = hitCollider.GetComponent<EvidenceTarget>();
                if (target == null)
                {
                    target = hitCollider.GetComponentInParent<EvidenceTarget>();
                }

                if (target != null)
                {
                    return true;
                }

                if (!hitCollider.isTrigger)
                {
                    blocked = true;
                    return false;
                }
            }

            return false;
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = current;
            }
        }

        private void ShowMessage(string message)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(message);
            }
            else
            {
                Debug.Log("[EvidenceCamera] " + message);
            }
        }

        private void EnsureNotebook()
        {
            EvidenceNotebookUI notebook = GetComponent<EvidenceNotebookUI>();
            if (notebook != null)
            {
                notebook.SetToggleKey(notebookToggleKey);
                return;
            }

            if (createNotebookIfMissing)
            {
                notebook = gameObject.AddComponent<EvidenceNotebookUI>();
                notebook.SetToggleKey(notebookToggleKey);
            }
        }

        private Sprite CaptureCameraPhoto()
        {
            if (playerCamera == null)
            {
                return null;
            }

            int width = Mathf.Clamp(capturedPhotoWidth, 256, 4096);
            float aspect = playerCamera.aspect > 0.01f ? playerCamera.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
            int height = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(0.01f, aspect)), 144, 4096);

            RenderTexture previousTarget = playerCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            playerCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            playerCamera.Render();
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            playerCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);

            texture.name = "CapturedEvidencePhoto";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "CapturedEvidencePhotoSprite";
            return sprite;
        }

        private void CreateRuntimeUi()
        {
            GameObject canvasObject = new GameObject("EvidenceCameraRuntimeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            if (cameraModeUI == null)
            {
                cameraModeUI = CreateCameraHud(canvasRect);
            }

            if (messageUI == null)
            {
                messageUI = CreateMessageUi(canvasRect);
            }
        }

        private GameObject CreateCameraHud(RectTransform parent)
        {
            GameObject root = CreateRectObject("CameraModeHUD", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            hudGroup = root.AddComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;
            hudAnimatedRoot = rootRect;

            Image dim = CreateImage("CameraTint", rootRect, new Color(0.02f, 0.06f, 0.055f, 0.12f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            RectTransform frame = CreateRectObject("Viewfinder", rootRect).GetComponent<RectTransform>();
            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1040f, 640f);

            CreateCorner(frame, "Corner_TL", new Vector2(0f, 1f), new Vector2(1f, -1f));
            CreateCorner(frame, "Corner_TR", new Vector2(1f, 1f), new Vector2(-1f, -1f));
            CreateCorner(frame, "Corner_BL", new Vector2(0f, 0f), new Vector2(1f, 1f));
            CreateCorner(frame, "Corner_BR", new Vector2(1f, 0f), new Vector2(-1f, 1f));

            Image crossHorizontal = CreateImage("CrosshairHorizontal", frame, hudColor);
            crossHorizontal.rectTransform.sizeDelta = new Vector2(46f, 2f);
            Center(crossHorizontal.rectTransform);
            crossHorizontal.raycastTarget = false;

            Image crossVertical = CreateImage("CrosshairVertical", frame, hudColor);
            crossVertical.rectTransform.sizeDelta = new Vector2(2f, 46f);
            Center(crossVertical.rectTransform);
            crossVertical.raycastTarget = false;

            TMP_Text title = CreateText("ModeLabel", rootRect, cameraModeLabel, 24f, TextAlignmentOptions.TopLeft);
            title.color = hudColor;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(70f, -52f), new Vector2(-70f, -16f));

            TMP_Text hint = CreateText("CaptureHint", rootRect, captureHint, 20f, TextAlignmentOptions.BottomRight);
            hint.color = hudColor;
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(70f, 18f), new Vector2(-70f, 54f));

            captureFlash = CreateImage("CaptureFlash", rootRect, Color.white);
            Stretch(captureFlash.rectTransform);
            captureFlash.raycastTarget = false;
            SetImageAlpha(captureFlash, 0f);
            captureFlash.transform.SetAsLastSibling();

            return root;
        }

        private SimpleMessageUI CreateMessageUi(RectTransform parent)
        {
            GameObject panelObject = CreateRectObject("EvidenceMessage", parent);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 96f);
            panelRect.sizeDelta = new Vector2(760f, 72f);

            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.7f);
            panel.raycastTarget = false;

            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            TMP_Text text = CreateText("MessageText", panelRect, string.Empty, 25f, TextAlignmentOptions.Center);
            text.color = hudColor;
            Stretch(text.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -8f));

            SimpleMessageUI simpleMessage = panelObject.AddComponent<SimpleMessageUI>();
            simpleMessage.Configure(text, group);
            return simpleMessage;
        }

        private void CreateCorner(RectTransform parent, string name, Vector2 anchor, Vector2 direction)
        {
            RectTransform corner = CreateRectObject(name, parent).GetComponent<RectTransform>();
            corner.anchorMin = anchor;
            corner.anchorMax = anchor;
            corner.pivot = new Vector2(0.5f, 0.5f);
            corner.anchoredPosition = new Vector2(28f * direction.x, 28f * direction.y);
            corner.sizeDelta = Vector2.zero;

            Image horizontal = CreateImage("Horizontal", corner, hudColor);
            Center(horizontal.rectTransform);
            horizontal.rectTransform.pivot = new Vector2(direction.x > 0f ? 0f : 1f, 0.5f);
            horizontal.rectTransform.sizeDelta = new Vector2(72f, 3f);
            horizontal.raycastTarget = false;

            Image vertical = CreateImage("Vertical", corner, hudColor);
            Center(vertical.rectTransform);
            vertical.rectTransform.pivot = new Vector2(0.5f, direction.y > 0f ? 0f : 1f);
            vertical.rectTransform.sizeDelta = new Vector2(3f, 72f);
            vertical.raycastTarget = false;
        }

        private void EnsureHudAnimationReferences()
        {
            if (cameraModeUI == null)
            {
                return;
            }

            if (hudGroup == null)
            {
                hudGroup = cameraModeUI.GetComponent<CanvasGroup>();
                if (hudGroup == null)
                {
                    hudGroup = cameraModeUI.AddComponent<CanvasGroup>();
                }
            }

            if (hudAnimatedRoot == null)
            {
                hudAnimatedRoot = cameraModeUI.transform as RectTransform;
            }
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            if (cameraOpenClip == null)
            {
                cameraOpenClip = CreateToneClip("EvidenceCameraOpen", 0.12f, 430f, 740f, 0.16f);
            }

            if (cameraCloseClip == null)
            {
                cameraCloseClip = CreateToneClip("EvidenceCameraClose", 0.11f, 620f, 280f, 0.13f);
            }

            if (shutterClip == null)
            {
                shutterClip = CreateShutterClip();
            }
        }

        private void SetCameraModeImmediate(bool active)
        {
            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            if (cameraModeUI != null)
            {
                cameraModeUI.SetActive(active);
            }

            if (hudGroup != null)
            {
                hudGroup.alpha = active ? 1f : 0f;
            }

            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = Vector3.one;
            }

            if (captureFlash != null)
            {
                SetImageAlpha(captureFlash, 0f);
            }
        }

        private IEnumerator AnimateCameraMode(bool active)
        {
            EnsureHudAnimationReferences();
            if (cameraModeUI == null || hudGroup == null)
            {
                if (cameraModeUI != null)
                {
                    cameraModeUI.SetActive(active);
                }

                yield break;
            }

            if (active)
            {
                cameraModeUI.SetActive(true);
                PlaySound(cameraOpenClip);
            }
            else
            {
                PlaySound(cameraCloseClip);
            }

            float fromAlpha = hudGroup.alpha;
            float toAlpha = active ? 1f : 0f;
            Vector3 fromScale = hudAnimatedRoot != null ? hudAnimatedRoot.localScale : Vector3.one;
            Vector3 toScale = active ? Vector3.one : Vector3.one * hudOpenScale;
            if (active && hudAnimatedRoot != null && fromAlpha <= 0.001f)
            {
                fromScale = Vector3.one * hudOpenScale;
                hudAnimatedRoot.localScale = fromScale;
            }

            float timer = 0f;
            while (timer < cameraTransitionDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Smooth01(timer / Mathf.Max(0.001f, cameraTransitionDuration));
                hudGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                if (hudAnimatedRoot != null)
                {
                    hudAnimatedRoot.localScale = Vector3.Lerp(fromScale, toScale, t);
                }

                yield return null;
            }

            hudGroup.alpha = toAlpha;
            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = toScale;
            }

            if (!active)
            {
                cameraModeUI.SetActive(false);
                if (hudAnimatedRoot != null)
                {
                    hudAnimatedRoot.localScale = Vector3.one;
                }
            }

            cameraModeRoutine = null;
        }

        private void PlayCaptureFeedback()
        {
            if (captureFeedbackRoutine != null)
            {
                StopCoroutine(captureFeedbackRoutine);
            }

            captureFeedbackRoutine = StartCoroutine(CaptureFeedbackRoutine());
        }

        private IEnumerator CaptureFeedbackRoutine()
        {
            float timer = 0f;
            Vector3 startScale = hudAnimatedRoot != null ? hudAnimatedRoot.localScale : Vector3.one;
            while (timer < captureFeedbackDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, captureFeedbackDuration));
                float flash = t < 0.32f ? Mathf.Lerp(0f, 0.82f, t / 0.32f) : Mathf.Lerp(0.82f, 0f, (t - 0.32f) / 0.68f);
                if (captureFlash != null)
                {
                    SetImageAlpha(captureFlash, flash);
                }

                if (hudAnimatedRoot != null)
                {
                    float scale = Mathf.Lerp(hudCaptureScale, 1f, Smooth01(t));
                    hudAnimatedRoot.localScale = startScale * scale;
                }

                yield return null;
            }

            if (captureFlash != null)
            {
                SetImageAlpha(captureFlash, 0f);
            }

            if (hudAnimatedRoot != null)
            {
                hudAnimatedRoot.localScale = Vector3.one;
            }

            captureFeedbackRoutine = null;
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private static GameObject CreateRectObject(string name, RectTransform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static AudioClip CreateToneClip(string name, float duration, float startFrequency, float endFrequency, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(phase * Mathf.PI * 2f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateShutterClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.16f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float clickEnvelope = Mathf.Exp(-t * 42f);
                float shutterEnvelope = Mathf.Clamp01(1f - Mathf.Abs(t - 0.36f) * 3.2f) * Mathf.Exp(-t * 4f);
                float click = Mathf.Sin(t * 4100f) * clickEnvelope * 0.34f;
                float shutter = (Mathf.PerlinNoise(i * 0.031f, 0.37f) * 2f - 1f) * shutterEnvelope * 0.18f;
                samples[i] = Mathf.Clamp(click + shutter, -0.8f, 0.8f);
            }

            AudioClip clip = AudioClip.Create("EvidenceCameraShutter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
