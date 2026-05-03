using UnityEngine;
using UnityEngine.InputSystem;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeFreeLookController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CRTMenuCameraFocus _computerFocus;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private bool _autoSyncWithCameraPose = true;

        [Header("Movement")]
        [SerializeField] private bool _startLockedToDesk = true;
        [SerializeField] private float _moveSpeed = 2.4f;
        [SerializeField] private float _fastMoveMultiplier = 2.2f;
        [SerializeField] private bool _moveOnlyOnHorizontalPlane = true;
        [SerializeField] private bool _lockMovementHeight = true;

        [Header("Collision")]
        [SerializeField] private bool _enableCollision = true;
        [SerializeField] private float _collisionRadius = 0.22f;
        [SerializeField] private float _collisionPadding = 0.04f;
        [SerializeField] private LayerMask _collisionLayers = ~0;

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.08f;
        [SerializeField] private float _minimumPitch = -55f;
        [SerializeField] private float _maximumPitch = 65f;
        [SerializeField] private bool _lockCursorWhileFlying = true;
        [SerializeField] private bool _lookRequiresRightMouse = false;

        private bool _freeFlightEnabled;
        private float _pitch;
        private float _yaw;
        private float _lockedHeight;

        private void Reset()
        {
            _cameraRoot = Camera.main != null ? Camera.main.transform : transform;
        }

        private void Awake()
        {
            if (_cameraRoot == null)
            {
                _cameraRoot = Camera.main != null ? Camera.main.transform : transform;
            }

            _freeFlightEnabled = !_startLockedToDesk;
            Vector3 euler = _cameraRoot.rotation.eulerAngles;
            _pitch = NormalizeAngle(euler.x);
            _yaw = euler.y;
            _lockedHeight = _cameraRoot.position.y;
        }

        private void Update()
        {
            SyncFlightState();

            if (_cameraRoot == null || IsComputerFocused() || !_freeFlightEnabled || !CanMoveFreely())
            {
                ReleaseCursor();
                return;
            }

            HandleLook();
            HandleMove();
        }

        public void EnableFreeFlight()
        {
            _freeFlightEnabled = true;
            Vector3 euler = _cameraRoot.rotation.eulerAngles;
            _pitch = NormalizeAngle(euler.x);
            _yaw = euler.y;
            _lockedHeight = _cameraRoot.position.y;
        }

        public void DisableFreeFlight()
        {
            _freeFlightEnabled = false;
            ReleaseCursor();
        }

        private bool IsComputerFocused()
        {
            return _computerFocus != null && _computerFocus.IsFocused;
        }

        private bool CanMoveFreely()
        {
            return _computerFocus == null || _computerFocus.IsInStandPose;
        }

        private void SyncFlightState()
        {
            if (!_autoSyncWithCameraPose || _computerFocus == null)
            {
                return;
            }

            if (_computerFocus.IsInStandPose)
            {
                if (!_freeFlightEnabled)
                {
                    EnableFreeFlight();
                }

                return;
            }

            if (_freeFlightEnabled)
            {
                DisableFreeFlight();
            }
        }

        private void HandleLook()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (_lookRequiresRightMouse && !Mouse.current.rightButton.isPressed)
            {
                ReleaseCursor();
                return;
            }

            if (_lockCursorWhileFlying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector2 delta = Mouse.current.delta.ReadValue();
            _yaw += delta.x * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * _lookSensitivity, _minimumPitch, _maximumPitch);
            _cameraRoot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMove()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            Vector3 input = Vector3.zero;
            Vector3 forward = _cameraRoot.forward;
            Vector3 right = _cameraRoot.right;
            if (_moveOnlyOnHorizontalPlane)
            {
                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            }

            if (Keyboard.current.wKey.isPressed) input += forward;
            if (Keyboard.current.sKey.isPressed) input -= forward;
            if (Keyboard.current.dKey.isPressed) input += right;
            if (Keyboard.current.aKey.isPressed) input -= right;
            if (!_lockMovementHeight)
            {
                if (Keyboard.current.eKey.isPressed) input += Vector3.up;
                if (Keyboard.current.qKey.isPressed) input -= Vector3.up;
            }

            if (input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float speed = _moveSpeed;
            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
            {
                speed *= _fastMoveMultiplier;
            }

            Vector3 nextPosition = _cameraRoot.position + input.normalized * (speed * Time.deltaTime);
            if (_enableCollision)
            {
                nextPosition = ResolveCollision(_cameraRoot.position, nextPosition);
            }

            if (_lockMovementHeight)
            {
                nextPosition.y = _lockedHeight;
            }

            _cameraRoot.position = nextPosition;
        }

        private Vector3 ResolveCollision(Vector3 currentPosition, Vector3 targetPosition)
        {
            Vector3 movement = targetPosition - currentPosition;
            float distance = movement.magnitude;
            if (distance <= 0.0001f)
            {
                return currentPosition;
            }

            Vector3 direction = movement / distance;
            if (Physics.SphereCast(currentPosition, _collisionRadius, direction, out RaycastHit hit, distance + _collisionPadding, _collisionLayers, QueryTriggerInteraction.Ignore))
            {
                float allowedDistance = Mathf.Max(0f, hit.distance - _collisionPadding);
                return currentPosition + direction * allowedDistance;
            }

            return targetPosition;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
