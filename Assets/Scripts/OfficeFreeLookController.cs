using UnityEngine;
using UnityEngine.InputSystem;
using ArchiveNull.Evidence;

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
        [Tooltip("CapsuleCollider creado y ajustado a mano para representar el volumen de la camara/freecam. Si esta vacio, usa SphereCast.")]
        [SerializeField] private CapsuleCollider _movementCollider;
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
        private readonly RaycastHit[] _capsuleCastHits = new RaycastHit[16];
        private readonly Collider[] _overlapHits = new Collider[16];

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

            if (_movementCollider == null)
            {
                _movementCollider = _cameraRoot.GetComponent<CapsuleCollider>();
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

            if (_cameraRoot == null || IsComputerFocused() || !_freeFlightEnabled || !CanMoveFreely() || IsUiBlockingLook())
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

        private static bool IsUiBlockingLook()
        {
            return EvidenceNotebookUI.IsAnyNotebookOpen || EvidenceCameraController.IsAnyRadialMenuOpen;
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
            if (_movementCollider != null)
            {
                return ResolveCapsuleCollision(currentPosition, targetPosition, direction, distance);
            }

            if (Physics.SphereCast(currentPosition, _collisionRadius, direction, out RaycastHit hit, distance + _collisionPadding, _collisionLayers, QueryTriggerInteraction.Ignore))
            {
                float allowedDistance = Mathf.Max(0f, hit.distance - _collisionPadding);
                return currentPosition + direction * allowedDistance;
            }

            return targetPosition;
        }

        private Vector3 ResolveCapsuleCollision(Vector3 currentPosition, Vector3 targetPosition, Vector3 direction, float distance)
        {
            Transform colliderTransform = _movementCollider.transform;
            GetCapsulePoints(currentPosition, out Vector3 pointA, out Vector3 pointB, out float radius);
            Quaternion orientation = colliderTransform.rotation;
            radius = Mathf.Max(0.001f, radius + _collisionPadding);

            int hitCount = Physics.CapsuleCastNonAlloc(pointA, pointB, radius, direction, _capsuleCastHits, distance + _collisionPadding, _collisionLayers, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _capsuleCastHits[i].collider;
                if (IsMovementCollider(hitCollider))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, _capsuleCastHits[i].distance);
            }

            if (!float.IsPositiveInfinity(nearestDistance))
            {
                float allowedDistance = Mathf.Max(0f, nearestDistance - _collisionPadding);
                return currentPosition + direction * allowedDistance;
            }

            GetCapsulePoints(targetPosition, out Vector3 targetA, out Vector3 targetB, out float targetRadius);
            targetRadius = Mathf.Max(0.001f, targetRadius + _collisionPadding);
            int overlapCount = Physics.OverlapCapsuleNonAlloc(targetA, targetB, targetRadius, _overlapHits, _collisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                if (!IsMovementCollider(_overlapHits[i]))
                {
                    return currentPosition;
                }
            }

            return targetPosition;
        }

        private void GetCapsulePoints(Vector3 cameraPosition, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            pointA = cameraPosition;
            pointB = cameraPosition;
            radius = _collisionRadius;

            if (_movementCollider == null)
            {
                return;
            }

            Transform colliderTransform = _movementCollider.transform;
            Vector3 currentCameraPosition = _cameraRoot != null ? _cameraRoot.position : cameraPosition;
            Vector3 centerOffset = colliderTransform.TransformPoint(_movementCollider.center) - currentCameraPosition;
            Vector3 center = cameraPosition + centerOffset;
            Vector3 lossy = colliderTransform.lossyScale;

            int axis = Mathf.Clamp(_movementCollider.direction, 0, 2);
            float heightScale = axis == 0 ? Mathf.Abs(lossy.x) : axis == 1 ? Mathf.Abs(lossy.y) : Mathf.Abs(lossy.z);
            float radiusScaleA = axis == 0 ? Mathf.Abs(lossy.y) : Mathf.Abs(lossy.x);
            float radiusScaleB = axis == 2 ? Mathf.Abs(lossy.y) : Mathf.Abs(lossy.z);
            radius = Mathf.Max(0.001f, _movementCollider.radius * Mathf.Max(radiusScaleA, radiusScaleB));
            float halfHeight = Mathf.Max(radius, (_movementCollider.height * heightScale) * 0.5f);
            float segmentOffset = Mathf.Max(0f, halfHeight - radius);

            Vector3 axisDir = axis == 0 ? colliderTransform.right : axis == 1 ? colliderTransform.up : colliderTransform.forward;
            pointA = center + axisDir * segmentOffset;
            pointB = center - axisDir * segmentOffset;
        }

        private bool IsMovementCollider(Collider candidate)
        {
            return candidate == null || candidate == _movementCollider;
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
