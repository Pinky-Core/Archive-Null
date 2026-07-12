using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class VRHeadsetArchiveStarter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Collider _headsetClickCollider;
        [SerializeField] private Transform _headsetVisual;
        [SerializeField] private Transform _equippedPose;
        [SerializeField] private TMP_Text _startPromptText;
        [SerializeField] private TMP_Text _unequipPromptText;
        [SerializeField] private CRTMainMenuController _menuController;
        [SerializeField] private CRTMenuCameraFocus _computerFocus;
        [SerializeField] private MemorySceneLoader _memorySceneLoader;

        [Header("Equip")]
        [SerializeField] private float _equipDuration = 0.65f;
        [SerializeField] private AnimationCurve _equipEase = null;
        [SerializeField] private string _startPrompt = "INICIAR MEMORIA";
        [SerializeField] private Vector3 _headOffset = new(0f, -0.05f, 0.12f);
        [SerializeField] private Vector3 _liftOffset = new(0f, 0.22f, -0.12f);
        [SerializeField] private Vector3 _wearRotationOffset = new(0f, 180f, 0f);
        [SerializeField] private float _nearFaceScale = 1.08f;
        [SerializeField] private bool _hideHeadsetWhenEquipped = true;

        [Header("VR View")]
        [SerializeField] private CanvasGroup _fadeToBlack;
        [SerializeField] private CanvasGroup _vrViewOverlay;
        [SerializeField] private float _vrOverlayVisibleAlpha = 0.42f;
        [SerializeField] private float _equipBlackoutDuration = 0.12f;
        [SerializeField] private float _equipRevealDelay = 0.45f;
        [SerializeField] private float _equipRevealDuration = 2f;
        [SerializeField] private float _overlayFadeDuration = 0.9f;

        [Header("Start Prompt")]
        [SerializeField] private bool _blinkStartPrompt = true;
        [SerializeField] private float _promptBlinkSpeed = 5.5f;
        [SerializeField] private float _promptMinAlpha = 0.22f;
        [SerializeField] private float _promptMaxAlpha = 1f;

        [Header("Feedback")]
        [SerializeField] private bool _autoHideStatusMessages = true;
        [SerializeField] private float _statusMessageDuration = 1.6f;

        private bool _equipped;
        private bool _busy;
        private float _promptBlinkTimer;
        private Vector3 _headsetInitialLocalPosition;
        private Quaternion _headsetInitialLocalRotation;
        private Vector3 _headsetInitialLocalScale;
        private bool _headsetInitiallyActive;
        private Renderer[] _headsetRenderers;
        private bool[] _headsetRendererInitialStates;
        private Coroutine _statusMessageRoutine;

        public bool IsEquipped => _equipped;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_equipEase == null || _equipEase.length == 0)
            {
                _equipEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (_headsetVisual != null)
            {
                _headsetInitialLocalPosition = _headsetVisual.localPosition;
                _headsetInitialLocalRotation = _headsetVisual.localRotation;
                _headsetInitialLocalScale = _headsetVisual.localScale;
                _headsetInitiallyActive = _headsetVisual.gameObject.activeSelf;
                _headsetRenderers = _headsetVisual.GetComponentsInChildren<Renderer>(true);
                _headsetRendererInitialStates = new bool[_headsetRenderers.Length];
                for (int i = 0; i < _headsetRenderers.Length; i++)
                {
                    _headsetRendererInitialStates[i] = _headsetRenderers[i] != null && _headsetRenderers[i].enabled;
                }
            }

            ResetVrUi();
            SetPromptVisible(false);
            SetUnequipPromptVisible(false);
        }

        private void Update()
        {
            if (_busy || CaseFileReader.IsAnyCaseFileOpen)
            {
                return;
            }

            if (!_equipped && WasHeadsetClickThisFrame())
            {
                TryEquip();
                return;
            }

            if (_equipped && WasUnequipPressed())
            {
                StartCoroutine(UnequipRoutine());
                return;
            }

            if (_equipped && WasStartPressed())
            {
                StartCoroutine(StartMemoryRoutine());
                return;
            }

            UpdatePromptBlink();
        }

        private void TryEquip()
        {
            if (_computerFocus != null && _computerFocus.IsFocused)
            {
                return;
            }

            if (_computerFocus != null && !_computerFocus.IsInFarPose)
            {
                ShowPromptMessage("MOVE TO FAR TO USE VR");
                return;
            }

            if (_menuController == null || !_menuController.HasMountedArchive)
            {
                ShowPromptMessage("NO ARCHIVE MOUNTED");
                return;
            }

            StartCoroutine(EquipRoutine());
        }

        private IEnumerator EquipRoutine()
        {
            _busy = true;
            ResetVrUi();
            SetPromptVisible(false);

            if (_headsetVisual != null)
            {
                Vector3 startPosition = _headsetVisual.position;
                Quaternion startRotation = _headsetVisual.rotation;
                Vector3 startScale = _headsetVisual.localScale;
                Transform target = _equippedPose != null ? _equippedPose : _camera.transform;
                Vector3 endPosition = target.TransformPoint(_headOffset);
                Quaternion endRotation = target.rotation * Quaternion.Euler(_wearRotationOffset);
                Vector3 controlPosition = Vector3.Lerp(startPosition, endPosition, 0.45f) + target.TransformDirection(_liftOffset);

                float timer = 0f;
                while (timer < _equipDuration)
                {
                    timer += Time.deltaTime;
                    float rawT = Mathf.Clamp01(timer / Mathf.Max(0.001f, _equipDuration));
                    float t = _equipEase.Evaluate(rawT);
                    _headsetVisual.position = QuadraticBezier(startPosition, controlPosition, endPosition, t);
                    _headsetVisual.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                    float scalePulse = Mathf.Sin(rawT * Mathf.PI) * (_nearFaceScale - 1f);
                    _headsetVisual.localScale = startScale * (1f + scalePulse);
                    yield return null;
                }

                _headsetVisual.position = endPosition;
                _headsetVisual.rotation = endRotation;
                _headsetVisual.localScale = startScale;
            }

            yield return FadeCanvasGroup(_fadeToBlack, 0f, 1f, _equipBlackoutDuration, true, true);

            ApplyEquippedState();
            yield return FadeCanvasGroup(_vrViewOverlay, 0f, _vrOverlayVisibleAlpha, _overlayFadeDuration, true, true);
            if (_equipRevealDelay > 0f)
            {
                yield return new WaitForSeconds(_equipRevealDelay);
            }
            yield return FadeCanvasGroup(_fadeToBlack, 1f, 0f, _equipRevealDuration, true, false);

            _equipped = true;
            _busy = false;
            SetPromptText(_startPrompt);
            SetPromptVisible(true);
            SetUnequipPromptVisible(true);
            _promptBlinkTimer = 0f;
            SetCanvasGroup(_fadeToBlack, 0f, false);
            SetCanvasGroup(_vrViewOverlay, _vrOverlayVisibleAlpha, _vrViewOverlay != null && _vrOverlayVisibleAlpha > 0.001f);
        }

        private IEnumerator UnequipRoutine()
        {
            _busy = true;
            SetPromptVisible(false);
            SetUnequipPromptVisible(false);

            yield return FadeCanvasGroup(_vrViewOverlay, _vrViewOverlay != null ? _vrViewOverlay.alpha : 0f, 0f, _overlayFadeDuration, true, false);

            yield return AnimateUnequipToTable();
            RestoreUnequippedState(false);

            ResetVrUi();
            _busy = false;
        }

        private IEnumerator StartMemoryRoutine()
        {
            _busy = true;
            SetPromptVisible(false);
            SetUnequipPromptVisible(false);

            if (_menuController == null || !_menuController.HasMountedArchive)
            {
                _busy = false;
                ShowPromptMessage("NO ARCHIVE MOUNTED");
                yield break;
            }

            string sceneName = _menuController.MountedArchiveSceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                _busy = false;
                ShowPromptMessage("ARCHIVE SCENE NAME MISSING");
                yield break;
            }

            yield return FadeCanvasGroup(_vrViewOverlay, _vrViewOverlay != null ? _vrViewOverlay.alpha : 0f, 0f, _overlayFadeDuration * 0.65f, true, false);

            if (_memorySceneLoader != null)
            {
                yield return _memorySceneLoader.PlayMemory(sceneName);
                yield break;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void ApplyEquippedState()
        {
            if (_hideHeadsetWhenEquipped && _headsetRenderers != null)
            {
                for (int i = 0; i < _headsetRenderers.Length; i++)
                {
                    if (_headsetRenderers[i] != null)
                    {
                        _headsetRenderers[i].enabled = false;
                    }
                }
            }

            if (_headsetClickCollider != null)
            {
                _headsetClickCollider.enabled = false;
            }
        }

        private IEnumerator AnimateUnequipToTable()
        {
            if (_headsetVisual == null)
            {
                yield break;
            }

            Transform target = _equippedPose != null ? _equippedPose : _camera.transform;
            Vector3 startPosition = target.TransformPoint(_headOffset);
            Quaternion startRotation = target.rotation * Quaternion.Euler(_wearRotationOffset);
            Vector3 startScale = _headsetInitialLocalScale;

            if (_headsetVisual.parent != null)
            {
                startScale = _headsetVisual.localScale;
            }

            _headsetVisual.gameObject.SetActive(true);
            SetHeadsetRenderersToInitialState();
            _headsetVisual.position = startPosition;
            _headsetVisual.rotation = startRotation;
            _headsetVisual.localScale = startScale;

            Vector3 endPosition = _headsetVisual.parent != null ? _headsetVisual.parent.TransformPoint(_headsetInitialLocalPosition) : _headsetInitialLocalPosition;
            Quaternion endRotation = _headsetVisual.parent != null ? _headsetVisual.parent.rotation * _headsetInitialLocalRotation : _headsetInitialLocalRotation;
            Vector3 controlPosition = Vector3.Lerp(startPosition, endPosition, 0.45f) + target.TransformDirection(_liftOffset);

            float timer = 0f;
            while (timer < _equipDuration)
            {
                timer += Time.deltaTime;
                float rawT = Mathf.Clamp01(timer / Mathf.Max(0.001f, _equipDuration));
                float t = _equipEase.Evaluate(rawT);
                _headsetVisual.position = QuadraticBezier(startPosition, controlPosition, endPosition, t);
                _headsetVisual.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                float scalePulse = Mathf.Sin(rawT * Mathf.PI) * (_nearFaceScale - 1f);
                _headsetVisual.localScale = startScale * (1f + scalePulse);
                yield return null;
            }
        }

        private void RestoreUnequippedState(bool restoreRenderers = true)
        {
            _equipped = false;

            if (_headsetVisual != null)
            {
                _headsetVisual.localPosition = _headsetInitialLocalPosition;
                _headsetVisual.localRotation = _headsetInitialLocalRotation;
                _headsetVisual.localScale = _headsetInitialLocalScale;
                _headsetVisual.gameObject.SetActive(_headsetInitiallyActive);
            }

            if (restoreRenderers)
            {
                SetHeadsetRenderersToInitialState();
            }

            if (_headsetClickCollider != null)
            {
                _headsetClickCollider.enabled = true;
            }
        }

        private void SetHeadsetRenderersToInitialState()
        {
            if (_headsetRenderers != null)
            {
                for (int i = 0; i < _headsetRenderers.Length; i++)
                {
                    if (_headsetRenderers[i] != null)
                    {
                        bool enabled = i < _headsetRendererInitialStates.Length && _headsetRendererInitialStates[i];
                        _headsetRenderers[i].enabled = enabled;
                    }
                }
            }
        }

        private bool WasHeadsetClickThisFrame()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            if (_camera == null || _headsetClickCollider == null)
            {
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return _headsetClickCollider.Raycast(ray, out _, 100f);
        }

        private static bool WasStartPressed()
        {
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null &&
                                   (Keyboard.current.enterKey.wasPressedThisFrame ||
                                    Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                    Keyboard.current.spaceKey.wasPressedThisFrame);
            return mousePressed || keyboardPressed;
        }

        private static bool WasUnequipPressed()
        {
            bool rightMousePressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null &&
                                   (Keyboard.current.escapeKey.wasPressedThisFrame ||
                                    Keyboard.current.backspaceKey.wasPressedThisFrame);
            return rightMousePressed || keyboardPressed;
        }

        private void UpdatePromptBlink()
        {
            if (!_blinkStartPrompt || !_equipped || _startPromptText == null || !_startPromptText.gameObject.activeSelf)
            {
                return;
            }

            _promptBlinkTimer += Time.deltaTime * _promptBlinkSpeed;
            float pulse = (Mathf.Sin(_promptBlinkTimer) + 1f) * 0.5f;
            Color color = _startPromptText.color;
            color.a = Mathf.Lerp(_promptMinAlpha, _promptMaxAlpha, pulse);
            _startPromptText.color = color;
        }

        private void ShowPromptMessage(string message)
        {
            if (_statusMessageRoutine != null)
            {
                StopCoroutine(_statusMessageRoutine);
                _statusMessageRoutine = null;
            }

            SetPromptText(message);
            SetPromptVisible(true);

            if (_autoHideStatusMessages)
            {
                _statusMessageRoutine = StartCoroutine(HidePromptAfterDelay());
            }
        }

        private void SetPromptText(string value)
        {
            if (_startPromptText != null)
            {
                _startPromptText.text = value;
            }
        }

        private void SetPromptVisible(bool visible)
        {
            if (_startPromptText == null)
            {
                return;
            }

            _startPromptText.gameObject.SetActive(visible);
            Color color = _startPromptText.color;
            color.a = visible ? _promptMaxAlpha : 0f;
            _startPromptText.color = color;
        }

        private void SetUnequipPromptVisible(bool visible)
        {
            if (_unequipPromptText == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_unequipPromptText.text))
            {
                _unequipPromptText.text = "ESC / BACKSPACE: QUITAR VR";
            }

            _unequipPromptText.gameObject.SetActive(visible);
            Color color = _unequipPromptText.color;
            color.a = visible ? 1f : 0f;
            _unequipPromptText.color = color;
        }

        private void ResetVrUi()
        {
            if (_statusMessageRoutine != null)
            {
                StopCoroutine(_statusMessageRoutine);
                _statusMessageRoutine = null;
            }

            SetCanvasGroup(_fadeToBlack, 0f, false);
            SetCanvasGroup(_vrViewOverlay, 0f, false);
            SetUnequipPromptVisible(false);
        }

        private IEnumerator HidePromptAfterDelay()
        {
            yield return new WaitForSeconds(_statusMessageDuration);
            if (!_equipped)
            {
                SetPromptVisible(false);
            }

            _statusMessageRoutine = null;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, bool activateAtStart, bool keepActiveAtEnd)
        {
            if (group == null)
            {
                yield break;
            }

            group.alpha = from;
            if (activateAtStart)
            {
                group.gameObject.SetActive(true);
            }

            if (duration <= 0f)
            {
                SetCanvasGroup(group, to, keepActiveAtEnd || to > 0.001f);
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

            SetCanvasGroup(group, to, keepActiveAtEnd || to > 0.001f);
        }

        private static void SetCanvasGroup(CanvasGroup group, float alpha, bool active)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(active);
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            Vector3 ab = Vector3.Lerp(a, b, t);
            Vector3 bc = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(ab, bc, t);
        }
    }
}
