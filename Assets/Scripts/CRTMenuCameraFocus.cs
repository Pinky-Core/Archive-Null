using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System;

namespace ArchiveNull.UI
{
    public sealed class CRTMenuCameraFocus : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Transform _standPose;
        [SerializeField] private Transform _farPose;
        [SerializeField] private Transform _focusPose;
        [SerializeField] private Collider _computerClickCollider;
        [SerializeField] private Collider[] _returnToFarClickColliders;
        [SerializeField] private CRTMainMenuController _menuController;
        [SerializeField] private VRHeadsetArchiveStarter _vrHeadsetStarter;

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
        [Tooltip("Tiempo que tarda en entrar el sway/parallax despues de volver desde Focus a Far.")]
        [SerializeField] private float _farMotionBlendDuration = 0.45f;
        [SerializeField] private bool _useMouseParallax = true;
        [SerializeField] private float _mousePositionAmount = 0.045f;
        [SerializeField] private float _mouseRotationAmount = 2f;
        [SerializeField] private float _mouseFollowSpeed = 4f;

        [Header("Return")]
        [SerializeField] private bool _allowReturnWithEscape = false;
        [SerializeField] private bool _allowReturnWithRightClick = true;
        [SerializeField] private bool _allowStandExitFromFarWithEscape = true;
        [SerializeField] private LayerMask _returnToFarFallbackLayers = ~0;
        [SerializeField] private string[] _returnToFarFallbackNameContains = { "mesa", "table", "silla", "chair" };
        [SerializeField] private bool _releaseCameraWhenUnfocused = true;

        [Header("Initial Pose")]
        [SerializeField] private bool _startAtFarPose = true;
        [SerializeField] private bool _startStandingUntilTutorialCompleted = true;
        [Tooltip("Pose opcional usada solo la primera vez, antes de completar el tutorial. Si queda vacia, usa Stand Pose.")]
        [SerializeField] private Transform _firstTimeStartPose;
        [SerializeField] private UnityEvent _onFocusReleased;

        private enum CameraPose
        {
            Stand,
            Far,
            Focus
        }

        private bool _isFocused;
        private float _moveTimer;
        private Vector3 _initialCameraPosition;
        private Quaternion _initialCameraRotation;
        private CameraPose _currentPose = CameraPose.Far;
        private Vector3 _moveStartPosition;
        private Quaternion _moveStartRotation;
        private Vector3 _moveTargetPosition;
        private Quaternion _moveTargetRotation;
        private bool _pendingMenuOpen;
        private bool _pendingFocusReleasedEvent;
        private Vector2 _mouseLook;
        private float _farMotionBlendTimer;

        public event Action ReturnedToFar;

        public bool IsFocused => _isFocused;
        public bool IsInFarPose => !_isFocused && _currentPose == CameraPose.Far;
        public bool IsInStandPose => !_isFocused && _currentPose == CameraPose.Stand;
        public bool IsTransitioning => _moveTimer < _moveDuration;

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

            if (_vrHeadsetStarter == null)
            {
                _vrHeadsetStarter = FindObjectOfType<VRHeadsetArchiveStarter>();
            }

            if (_moveEase == null || _moveEase.length == 0)
            {
                _moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (_targetCamera != null)
            {
                _initialCameraPosition = _targetCamera.transform.position;
                _initialCameraRotation = _targetCamera.transform.rotation;
                bool startAtFarPose = ShouldStartAtFarPose();
                Transform firstTimePose = !startAtFarPose && ShouldUseFirstTimeStartPose() ? _firstTimeStartPose : null;
                Vector3 startPosition = firstTimePose != null ? firstTimePose.position : startAtFarPose ? GetPosePosition(CameraPose.Far) : GetPosePosition(CameraPose.Stand);
                Quaternion startRotation = firstTimePose != null ? firstTimePose.rotation : startAtFarPose ? GetPoseRotation(CameraPose.Far) : GetPoseRotation(CameraPose.Stand);
                _currentPose = startAtFarPose ? CameraPose.Far : CameraPose.Stand;
                _targetCamera.transform.SetPositionAndRotation(startPosition, startRotation);
                _moveStartPosition = startPosition;
                _moveStartRotation = startRotation;
                _moveTargetPosition = startPosition;
                _moveTargetRotation = startRotation;
            }

            _moveTimer = _moveDuration;
            _farMotionBlendTimer = _farMotionBlendDuration;
        }

        private bool ShouldStartAtFarPose()
        {
            if (!_startAtFarPose)
            {
                return false;
            }

            if (_startStandingUntilTutorialCompleted && PlayerPrefs.GetInt(OfficeSpeakerTutorial.CompletedPref, 0) != 1)
            {
                return false;
            }

            return true;
        }

        private bool ShouldUseFirstTimeStartPose()
        {
            return _firstTimeStartPose != null &&
                   _startStandingUntilTutorialCompleted &&
                   PlayerPrefs.GetInt(OfficeSpeakerTutorial.CompletedPref, 0) != 1;
        }

        private void Update()
        {
            if (_targetCamera == null || _focusPose == null)
            {
                return;
            }

            HandleInput();
            UpdateCameraMotion();
        }

        private void HandleInput()
        {
            if (!_isFocused && _currentPose == CameraPose.Stand && WasLeftClickThisFrame() && IsClickingReturnToFarTarget())
            {
                MoveToFarPose();
                return;
            }

            if (!_isFocused && _allowStandExitFromFarWithEscape && _currentPose == CameraPose.Far && CanExitSeatWithEscape() && WasEscapePressedThisFrame())
            {
                MoveToStandPose();
                return;
            }

            if (!_isFocused && _currentPose == CameraPose.Far && _focusOnComputerClick && WasLeftClickThisFrame() && IsClickingComputer())
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
                return;
            }

            if (_allowReturnWithEscape && WasEscapePressedThisFrame())
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

                if (_pendingFocusReleasedEvent && t >= 1f)
                {
                    _pendingFocusReleasedEvent = false;
                    _onFocusReleased?.Invoke();
                    if (_currentPose == CameraPose.Far)
                    {
                        ReturnedToFar?.Invoke();
                    }
                }
                return;
            }

            if (!_isFocused && _releaseCameraWhenUnfocused && _currentPose == CameraPose.Stand)
            {
                return;
            }

            if (_isFocused || !_enableFarSway)
            {
                return;
            }

            float swayTime = Time.time * _swaySpeed;
            _farMotionBlendTimer += Time.deltaTime;
            float farMotionBlend = _farMotionBlendDuration <= 0f ? 1f : Mathf.Clamp01(_farMotionBlendTimer / _farMotionBlendDuration);
            farMotionBlend = farMotionBlend * farMotionBlend * (3f - 2f * farMotionBlend);

            Vector3 basePosition = GetPosePosition(CameraPose.Far);
            Quaternion baseRotation = GetPoseRotation(CameraPose.Far);

            Vector3 positionOffset = new(
                Mathf.Sin(swayTime * 0.9f) * _swayPositionAmount,
                Mathf.Sin(swayTime * 1.2f) * (_swayPositionAmount * 0.65f),
                0f);
            positionOffset *= farMotionBlend;

            Quaternion rotationOffset = Quaternion.Euler(
                Mathf.Sin(swayTime * 1.15f) * _swayRotationAmount * farMotionBlend,
                Mathf.Cos(swayTime * 0.85f) * _swayRotationAmount * farMotionBlend,
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
                    0f) * farMotionBlend;

                parallaxRotation = Quaternion.Euler(
                    -_mouseLook.y * _mouseRotationAmount * farMotionBlend,
                    _mouseLook.x * _mouseRotationAmount * farMotionBlend,
                    0f);
            }

            _targetCamera.transform.position = basePosition + positionOffset + parallaxPosition;
            _targetCamera.transform.rotation = baseRotation * rotationOffset * parallaxRotation;
        }

        public void FocusComputer()
        {
            _isFocused = true;
            _currentPose = CameraPose.Focus;
            StartMove(GetPosePosition(CameraPose.Focus), GetPoseRotation(CameraPose.Focus));

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
            _pendingFocusReleasedEvent = true;
            _menuController?.SuspendTerminalInteraction();
            _currentPose = CameraPose.Far;
            _farMotionBlendTimer = 0f;
            StartMove(GetPosePosition(CameraPose.Far), GetPoseRotation(CameraPose.Far));
        }

        public void MoveToStandPose()
        {
            _isFocused = false;
            _pendingMenuOpen = false;
            _pendingFocusReleasedEvent = true;
            _menuController?.SuspendTerminalInteraction();
            _currentPose = CameraPose.Stand;
            StartMove(GetPosePosition(CameraPose.Stand), GetPoseRotation(CameraPose.Stand));
        }

        public void MoveToFarPose()
        {
            _isFocused = false;
            _pendingMenuOpen = false;
            _pendingFocusReleasedEvent = true;
            _currentPose = CameraPose.Far;
            _farMotionBlendTimer = 0f;
            StartMove(GetPosePosition(CameraPose.Far), GetPoseRotation(CameraPose.Far));
        }

        private void StartMove(Vector3 targetPosition, Quaternion targetRotation)
        {
            _moveStartPosition = _targetCamera.transform.position;
            _moveStartRotation = _targetCamera.transform.rotation;
            _moveTargetPosition = targetPosition;
            _moveTargetRotation = targetRotation;
            _moveTimer = 0f;
        }

        private Vector3 GetPosePosition(CameraPose pose)
        {
            Transform poseTransform = GetPoseTransform(pose);
            return poseTransform != null ? poseTransform.position : _initialCameraPosition;
        }

        private Quaternion GetPoseRotation(CameraPose pose)
        {
            Transform poseTransform = GetPoseTransform(pose);
            return poseTransform != null ? poseTransform.rotation : _initialCameraRotation;
        }

        private Transform GetPoseTransform(CameraPose pose)
        {
            return pose switch
            {
                CameraPose.Stand => _standPose != null ? _standPose : _farPose,
                CameraPose.Far => _farPose,
                CameraPose.Focus => _focusPose,
                _ => null
            };
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

        private bool IsClickingReturnToFarTarget()
        {
            if (_targetCamera == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = _targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (_returnToFarClickColliders != null && _returnToFarClickColliders.Length > 0)
            {
                for (int i = 0; i < _returnToFarClickColliders.Length; i++)
                {
                    Collider target = _returnToFarClickColliders[i];
                    if (target != null && target.Raycast(ray, out _, 100f))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, _returnToFarFallbackLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return IsReturnFallbackTarget(hit.collider);
        }

        private bool IsReturnFallbackTarget(Collider target)
        {
            if (target == null)
            {
                return false;
            }

            if (_returnToFarFallbackNameContains == null || _returnToFarFallbackNameContains.Length == 0)
            {
                return false;
            }

            Transform current = target.transform;
            while (current != null)
            {
                string objectName = current.name.ToLowerInvariant();
                for (int i = 0; i < _returnToFarFallbackNameContains.Length; i++)
                {
                    string needle = _returnToFarFallbackNameContains[i];
                    if (!string.IsNullOrWhiteSpace(needle) && objectName.Contains(needle.ToLowerInvariant()))
                    {
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        private bool CanExitSeatWithEscape()
        {
            return _vrHeadsetStarter == null || !_vrHeadsetStarter.IsEquipped;
        }

        private static bool WasLeftClickThisFrame()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private static bool WasEscapePressedThisFrame()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }
    }
}
