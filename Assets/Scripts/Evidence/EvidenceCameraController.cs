using UnityEngine;
using UnityEngine.InputSystem;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceCameraController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float maxCaptureDistance = 4f;
        [SerializeField] private LayerMask captureLayers = ~0;
        [SerializeField] private SimpleMessageUI messageUI;
        [SerializeField] private GameObject cameraModeUI;

        public bool IsCameraModeActive { get; private set; }
        public static bool IsAnyCameraModeActive { get; private set; }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            SetCameraMode(false);
        }

        private void OnDisable()
        {
            if (IsCameraModeActive)
            {
                SetCameraMode(false);
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

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryCapture();
            }
        }

        private void SetCameraMode(bool active)
        {
            IsCameraModeActive = active;
            IsAnyCameraModeActive = active;
            if (cameraModeUI != null)
            {
                cameraModeUI.SetActive(active);
            }
        }

        private void TryCapture()
        {
            if (playerCamera == null)
            {
                ShowMessage("Camara no disponible.");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxCaptureDistance, captureLayers, QueryTriggerInteraction.Ignore))
            {
                ShowMessage("No hay evidencia en foco.");
                return;
            }

            EvidenceTarget target = hit.collider.GetComponent<EvidenceTarget>();
            if (target == null)
            {
                ShowMessage("Objetivo no registrable.");
                return;
            }

            if (!target.CanRegister(out string validationMessage))
            {
                ShowMessage(validationMessage);
                return;
            }

            bool registered = EvidenceInventory.Instance.RegisterEvidence(target.EvidenceData);
            ShowMessage(registered ? "Evidencia registrada: " + target.EvidenceData.evidenceName : "Evidencia ya registrada.");
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
    }
}
