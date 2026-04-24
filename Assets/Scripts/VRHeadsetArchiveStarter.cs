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
        [SerializeField] private CRTMainMenuController _menuController;
        [SerializeField] private CRTMenuCameraFocus _computerFocus;
        [SerializeField] private RoomDissolveTransition _roomTransition;

        [Header("Equip")]
        [SerializeField] private float _equipDuration = 0.65f;
        [SerializeField] private AnimationCurve _equipEase = null;
        [SerializeField] private string _startPrompt = "INICIAR";
        [SerializeField] private Vector3 _headOffset = new(0f, -0.05f, 0.12f);
        [SerializeField] private Vector3 _liftOffset = new(0f, 0.22f, -0.12f);
        [SerializeField] private Vector3 _wearRotationOffset = new(0f, 180f, 0f);
        [SerializeField] private float _nearFaceScale = 1.08f;
        [SerializeField] private bool _hideHeadsetWhenEquipped = true;

        [Header("VR View")]
        [SerializeField] private CanvasGroup _fadeToBlack;
        [SerializeField] private CanvasGroup _vrViewOverlay;
        [SerializeField] private float _visorFadeInDuration = 0.14f;
        [SerializeField] private float _visorRevealDuration = 0.42f;
        [SerializeField] private float _vrOverlayFadeDuration = 0.28f;

        [Header("Start Prompt")]
        [SerializeField] private bool _blinkStartPrompt = true;
        [SerializeField] private float _promptBlinkSpeed = 5.5f;
        [SerializeField] private float _promptMinAlpha = 0.22f;
        [SerializeField] private float _promptMaxAlpha = 1f;

        private bool _equipped;
        private bool _busy;
        private float _promptBlinkTimer;
        private Vector3 _headsetInitialLocalPosition;
        private Quaternion _headsetInitialLocalRotation;
        private Vector3 _headsetInitialLocalScale;
        private bool _headsetInitiallyActive;

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
            }

            SetPromptVisible(false);
            SetCanvasGroup(_fadeToBlack, 0f, false);
            SetCanvasGroup(_vrViewOverlay, 0f, false);
        }

        private void Update()
        {
            if (_busy)
            {
                return;
            }

            if (!_equipped && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && IsClickingHeadset())
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
                StartCoroutine(StartMountedArchive());
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
                SetPromptText("MOVE TO FAR TO USE VR");
                SetPromptVisible(true);
                return;
            }

            if (_menuController == null || !_menuController.HasMountedArchive)
            {
                SetPromptText("NO ARCHIVE MOUNTED");
                SetPromptVisible(true);
                return;
            }

            StartCoroutine(EquipRoutine());
        }

        private IEnumerator EquipRoutine()
        {
            _busy = true;
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

            if (_fadeToBlack != null)
            {
                yield return FadeCanvasGroup(_fadeToBlack, _fadeToBlack.alpha, 1f, _visorFadeInDuration, true, true);
            }

            ApplyEquippedState();

            if (_vrViewOverlay != null)
            {
                yield return FadeCanvasGroup(_vrViewOverlay, 0f, 1f, _vrOverlayFadeDuration, true, true);
            }

            if (_fadeToBlack != null)
            {
                yield return FadeCanvasGroup(_fadeToBlack, _fadeToBlack.alpha, 0f, _visorRevealDuration, true, false);
                SetCanvasGroup(_fadeToBlack, 0f, false);
            }

            FinishEquip();
        }

        private void ApplyEquippedState()
        {
            if (_hideHeadsetWhenEquipped && _headsetVisual != null)
            {
                _headsetVisual.gameObject.SetActive(false);
            }

            if (_headsetClickCollider != null)
            {
                _headsetClickCollider.enabled = false;
            }

            _equipped = true;
        }

        private void FinishEquip()
        {
            _busy = false;
            SetPromptText(_startPrompt);
            SetPromptVisible(true);
            _promptBlinkTimer = 0f;
        }

        private IEnumerator UnequipRoutine()
        {
            _busy = true;
            SetPromptVisible(false);

            if (_fadeToBlack != null)
            {
                yield return FadeCanvasGroup(_fadeToBlack, _fadeToBlack.alpha, 1f, _visorFadeInDuration, true, true);
            }

            if (_vrViewOverlay != null)
            {
                yield return FadeCanvasGroup(_vrViewOverlay, _vrViewOverlay.alpha, 0f, _vrOverlayFadeDuration, true, false);
                SetCanvasGroup(_vrViewOverlay, 0f, false);
            }

            RestoreUnequippedState();

            if (_fadeToBlack != null)
            {
                yield return FadeCanvasGroup(_fadeToBlack, _fadeToBlack.alpha, 0f, _visorRevealDuration, true, false);
                SetCanvasGroup(_fadeToBlack, 0f, false);
            }

            _busy = false;
        }

        private IEnumerator StartMountedArchive()
        {
            _busy = true;
            SetPromptVisible(false);
            if (_menuController == null || !_menuController.HasMountedArchive)
            {
                SetPromptText("NO ARCHIVE MOUNTED");
                SetPromptVisible(true);
                _busy = false;
                yield break;
            }

            int sceneIndex = _menuController.MountedArchiveSceneBuildIndex;
            if (sceneIndex < 0)
            {
                SetPromptText("ARCHIVE SCENE MISSING");
                SetPromptVisible(true);
                _busy = false;
                yield break;
            }

            if (_roomTransition != null)
            {
                yield return _roomTransition.PlayAndLoad(sceneIndex);
            }
            else
            {
                SetCanvasGroup(_fadeToBlack, 1f, true);
                SceneManager.LoadScene(sceneIndex);
            }

            _busy = false;
        }

        private bool IsClickingHeadset()
        {
            if (_camera == null || _headsetClickCollider == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return _headsetClickCollider.Raycast(ray, out _, 100f);
        }

        private bool WasStartPressed()
        {
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null &&
                                   (Keyboard.current.enterKey.wasPressedThisFrame ||
                                    Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                    Keyboard.current.spaceKey.wasPressedThisFrame);
            return mousePressed || keyboardPressed;
        }

        private bool WasUnequipPressed()
        {
            bool rightMousePressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            bool keyboardPressed = Keyboard.current != null &&
                                   (Keyboard.current.escapeKey.wasPressedThisFrame ||
                                    Keyboard.current.backspaceKey.wasPressedThisFrame);
            return rightMousePressed || keyboardPressed;
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            Vector3 ab = Vector3.Lerp(a, b, t);
            Vector3 bc = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(ab, bc, t);
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

        private void SetPromptText(string value)
        {
            if (_startPromptText != null)
            {
                _startPromptText.text = value;
            }
        }

        private void SetPromptVisible(bool visible)
        {
            if (_startPromptText != null)
            {
                _startPromptText.gameObject.SetActive(visible);
                Color color = _startPromptText.color;
                color.a = visible ? _promptMaxAlpha : 0f;
                _startPromptText.color = color;
            }
        }

        private void RestoreUnequippedState()
        {
            _equipped = false;

            if (_headsetVisual != null)
            {
                _headsetVisual.localPosition = _headsetInitialLocalPosition;
                _headsetVisual.localRotation = _headsetInitialLocalRotation;
                _headsetVisual.localScale = _headsetInitialLocalScale;
                _headsetVisual.gameObject.SetActive(_headsetInitiallyActive);
            }

            if (_headsetClickCollider != null)
            {
                _headsetClickCollider.enabled = true;
            }
        }
    }
}
