using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class StandInteractionReticle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private CRTMenuCameraFocus _cameraFocus;
        [SerializeField] private VRHeadsetArchiveStarter _vrHeadset;
        [SerializeField] private CanvasGroup _reticleGroup;
        [SerializeField] private RectTransform _reticleRoot;
        [SerializeField] private Image _reticleDot;
        [SerializeField] private Image _reticleRing;
        [SerializeField] private Collider[] _interactableColliders;

        [Header("Behaviour")]
        [SerializeField] private float _interactDistance = 4f;
        [SerializeField] private float _fadeSpeed = 9f;
        [SerializeField] private float _scaleSpeed = 10f;
        [SerializeField] private float _idleScale = 1f;
        [SerializeField] private float _hoverScale = 1.35f;
        [SerializeField] private Color _idleColor = new(0.75f, 0.88f, 0.9f, 0.42f);
        [SerializeField] private Color _hoverColor = new(0.1f, 0.92f, 1f, 0.95f);

        private float _targetAlpha;
        private float _currentScale = 1f;

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

            ApplyImmediateState(false, false);
        }

        private void Update()
        {
            bool visible = ShouldShowReticle();
            bool hovering = visible && IsLookingAtInteractable();

            _targetAlpha = visible ? 1f : 0f;
            UpdateVisuals(hovering);
        }

        private bool ShouldShowReticle()
        {
            if (_reticleGroup == null || _cameraFocus == null)
            {
                return false;
            }

            if (_cameraFocus.IsTransitioning || !_cameraFocus.IsInStandPose)
            {
                return false;
            }

            if (_vrHeadset != null && _vrHeadset.IsEquipped)
            {
                return false;
            }

            return true;
        }

        private bool IsLookingAtInteractable()
        {
            if (_targetCamera == null || _interactableColliders == null || _interactableColliders.Length == 0)
            {
                return false;
            }

            Ray ray = _targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            for (int i = 0; i < _interactableColliders.Length; i++)
            {
                Collider target = _interactableColliders[i];
                if (target != null && target.Raycast(ray, out RaycastHit hit, _interactDistance))
                {
                    return hit.collider == target;
                }
            }

            return false;
        }

        private void UpdateVisuals(bool hovering)
        {
            if (_reticleGroup == null)
            {
                return;
            }

            _reticleGroup.alpha = Mathf.Lerp(_reticleGroup.alpha, _targetAlpha, Time.deltaTime * _fadeSpeed);
            float desiredScale = hovering ? _hoverScale : _idleScale;
            _currentScale = Mathf.Lerp(_currentScale, desiredScale, Time.deltaTime * _scaleSpeed);

            if (_reticleRoot != null)
            {
                _reticleRoot.localScale = Vector3.one * _currentScale;
            }

            Color currentColor = hovering ? _hoverColor : _idleColor;
            if (_reticleDot != null)
            {
                _reticleDot.color = currentColor;
            }

            if (_reticleRing != null)
            {
                _reticleRing.color = currentColor;
            }

            _reticleGroup.gameObject.SetActive(_reticleGroup.alpha > 0.01f || _targetAlpha > 0.01f);
        }

        private void ApplyImmediateState(bool visible, bool hovering)
        {
            if (_reticleGroup == null)
            {
                return;
            }

            _targetAlpha = visible ? 1f : 0f;
            _reticleGroup.alpha = _targetAlpha;
            _currentScale = hovering ? _hoverScale : _idleScale;
            if (_reticleRoot != null)
            {
                _reticleRoot.localScale = Vector3.one * _currentScale;
            }

            Color currentColor = hovering ? _hoverColor : _idleColor;
            if (_reticleDot != null)
            {
                _reticleDot.color = currentColor;
            }

            if (_reticleRing != null)
            {
                _reticleRing.color = currentColor;
            }

            _reticleGroup.gameObject.SetActive(visible);
        }
    }
}
