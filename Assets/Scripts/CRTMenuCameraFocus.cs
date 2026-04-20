using UnityEngine;
using UnityEngine.InputSystem;

namespace ArchiveNull.UI
{
    public sealed class CRTMenuCameraFocus : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Transform _farPose;
        [SerializeField] private Transform _focusPose;
        [SerializeField] private Collider _computerClickCollider;
        [SerializeField] private CRTMainMenuController _menuController;

        [Header("Focus")]
        [SerializeField] private bool _focusOnComputerClick = true;
        [SerializeField] private bool _openMenuWhenFocused = true;
        [SerializeField] private bool _openMenuAfterMoveCompletes = true;
        [SerializeField] private float _moveDuration = 0.55f;
        [SerializeField] private AnimationCurve _moveEase = null;

        [Header("Far Camera Motion")]
        [SerializeField] private bool _enableFarSway = true;
        [SerializeField] private float _swayPositionAmount = 0.015f;
        [SerializeField] private float _swayRotationAmount = 0.7f;
        [SerializeField] private float _swaySpeed = 0.9f;
        [SerializeField] private bool _useMouseParallax = true;
        [SerializeField] private float _mousePositionAmount = 0.045f;
        [SerializeField] private float _mouseRotationAmount = 2f;
        [SerializeField] private float _mouseFollowSpeed = 4f;

        [Header("Return")]
        [SerializeField] private bool _allowReturnWithEscape = false;
        [SerializeField] private bool _allowReturnWithRightClick = true;

        private bool _isFocused;
        private float _moveTimer;
        private Vector3 _moveStartPosition;
        private Quaternion _moveStartRotation;
        private Vector3 _moveTargetPosition;
        private Quaternion _moveTargetRotation;
        private bool _pendingMenuOpen;
        private Vector2 _mouseLook;

        private void Reset()
        {
            _targetCamera = Camera.main;
        }

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_moveEase == null || _moveEase.length == 0)
            {
                _moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private void Update()
        {
            if (_targetCamera == null || _farPose == null || _focusPose == null)
            {
                return;
            }

            HandleInput();
            UpdateCameraMotion();
        }

        private void HandleInput()
        {
            if (!_isFocused && _focusOnComputerClick && WasLeftClickThisFrame() && IsClickingComputer())
            {
                FocusComputer();
                return;
            }

            if (!_isFocused)
            {
                return;
            }

            if (_allowReturnWithRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ReturnToFarPose();
            }
        }

        private void UpdateCameraMotion()
        {
            if (_moveTimer < _moveDuration)
            {
                _moveTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_moveTimer / Mathf.Max(0.001f, _moveDuration));
                float eased = _moveEase.Evaluate(t);
                _targetCamera.transform.position = Vector3.Lerp(_moveStartPosition, _moveTargetPosition, eased);
                _targetCamera.transform.rotation = Quaternion.Slerp(_moveStartRotation, _moveTargetRotation, eased);

                if (_pendingMenuOpen && t >= 1f)
                {
                    _pendingMenuOpen = false;
                    _menuController?.FocusOpenMenu();
                }
                return;
            }

            if (_isFocused || !_enableFarSway)
            {
                return;
            }

            float swayTime = Time.time * _swaySpeed;
            Vector3 basePosition = _farPose.position;
            Quaternion baseRotation = _farPose.rotation;

            Vector3 positionOffset = new(
                Mathf.Sin(swayTime * 0.9f) * _swayPositionAmount,
                Mathf.Sin(swayTime * 1.2f) * (_swayPositionAmount * 0.65f),
                0f);

            Quaternion rotationOffset = Quaternion.Euler(
                Mathf.Sin(swayTime * 1.15f) * _swayRotationAmount,
                Mathf.Cos(swayTime * 0.85f) * _swayRotationAmount,
                0f);

            Vector3 parallaxPosition = Vector3.zero;
            Quaternion parallaxRotation = Quaternion.identity;
            if (_useMouseParallax && Mouse.current != null)
            {
                Vector2 mouse = Mouse.current.position.ReadValue();
                Vector2 viewport = new(mouse.x / Screen.width, mouse.y / Screen.height);
                Vector2 centered = (viewport - new Vector2(0.5f, 0.5f)) * 2f;
                _mouseLook = Vector2.Lerp(_mouseLook, centered, Time.deltaTime * _mouseFollowSpeed);

                parallaxPosition = new Vector3(
                    _mouseLook.x * _mousePositionAmount,
                    _mouseLook.y * _mousePositionAmount * 0.55f,
                    0f);

                parallaxRotation = Quaternion.Euler(
                    -_mouseLook.y * _mouseRotationAmount,
                    _mouseLook.x * _mouseRotationAmount,
                    0f);
            }

            _targetCamera.transform.position = basePosition + positionOffset + parallaxPosition;
            _targetCamera.transform.rotation = baseRotation * rotationOffset * parallaxRotation;
        }

        public void FocusComputer()
        {
            _isFocused = true;
            StartMove(_focusPose.position, _focusPose.rotation);

            if (_openMenuWhenFocused && _menuController != null)
            {
                if (_openMenuAfterMoveCompletes)
                {
                    _pendingMenuOpen = true;
                }
                else
                {
                    _menuController.FocusOpenMenu();
                }
            }
        }

        public void ReturnToFarPose()
        {
            _isFocused = false;
            _pendingMenuOpen = false;
            StartMove(_farPose.position, _farPose.rotation);
        }

        private void StartMove(Vector3 targetPosition, Quaternion targetRotation)
        {
            _moveStartPosition = _targetCamera.transform.position;
            _moveStartRotation = _targetCamera.transform.rotation;
            _moveTargetPosition = targetPosition;
            _moveTargetRotation = targetRotation;
            _moveTimer = 0f;
        }

        private bool IsClickingComputer()
        {
            if (_computerClickCollider == null || _targetCamera == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = _targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return _computerClickCollider.Raycast(ray, out _, 100f);
        }

        private static bool WasLeftClickThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
    }
}
