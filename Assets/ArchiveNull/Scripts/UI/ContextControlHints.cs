using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed class ContextControlHints : MonoBehaviour
    {
        private TMP_Text text;
        private string lastValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name == "MainMenu" || FindAnyObjectByType<ContextControlHints>() != null) return;
            new GameObject("ContextControlHints").AddComponent<ContextControlHints>();
        }

        private void Awake()
        {
            GameObject canvasObject = new("ContextControlsCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 14500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panelObject = new("Controls", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            Image panel = panelObject.GetComponent<Image>();
            panel.color = Color.clear;
            panel.raycastTarget = false;
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(38f, 0f);
            rect.sizeDelta = new Vector2(390f, 280f);

            GameObject textObject = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(rect, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 21f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color(0.88f, 0.87f, 0.78f, 1f);
            text.outlineWidth = 0.16f;
            text.outlineColor = new Color32(0, 0, 0, 230);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, 18f);
            textRect.offsetMax = new Vector2(-22f, -18f);
        }

        private void Update()
        {
            string value = GetText();
            if (value == lastValue) return;
            lastValue = value;
            text.text = value;
            text.transform.parent.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }

        private static string GetText()
        {
            if (GlobalPauseMenu.IsPaused) return string.Empty;
            if (PhoneEvidenceReader.IsAnyOpen) return GameLocalization.Text("TELÉFONO\n\nCLIC  Seleccionar\nWASD / FLECHAS  Navegar\nENTER  Confirmar\nESC  Volver\nG  Guardar teléfono", "PHONE\n\nCLICK  Select\nWASD / ARROWS  Navigate\nENTER  Confirm\nESC  Back\nG  Put phone away");
            if (global::InspectObject.IsAnyInspecting) return GameLocalization.Text("INSPECCIÓN\n\nCLIC IZQ. + MOUSE  Rotar\nRUEDA  Acercar / alejar\nE  Dejar objeto\nESC  Pausa", "INSPECTION\n\nLEFT CLICK + MOUSE  Rotate\nWHEEL  Zoom\nE  Release object\nESC  Pause");
            if (EvidenceCameraController.IsAnyCameraModeActive) return GameLocalization.Text("CÁMARA ABIERTA\n\nCLIC IZQ.  Fotografiar\nRUEDA  Zoom\nF  Bajar cámara\nG  Cambiar herramienta", "CAMERA OPEN\n\nLEFT CLICK  Photograph\nWHEEL  Zoom\nF  Lower camera\nG  Change tool");
            if (EvidenceCameraController.IsAnyCameraEquipped) return GameLocalization.Text("CÁMARA EQUIPADA\n\nF  Llevar a la cara\nG  Cambiar herramienta", "CAMERA EQUIPPED\n\nF  Raise to face\nG  Change tool");
            if (EvidenceCameraController.IsAnyUvLightActive) return GameLocalization.Text("LUZ UV\n\nF  Apagar\nMOUSE  Dirigir haz\nG  Cambiar herramienta", "UV LIGHT\n\nF  Turn off\nMOUSE  Aim beam\nG  Change tool");
            if (EvidenceCameraController.IsAnyRadialMenuOpen) return GameLocalization.Text("HERRAMIENTAS\n\nMOUSE  Seleccionar\nSOLTAR G  Equipar", "TOOLS\n\nMOUSE  Select\nRELEASE G  Equip");
            return GameLocalization.Text("ACCIÓN\n\nE  Interactuar\nG  Herramientas\nTAB  Expediente", "ACTION\n\nE  Interact\nG  Tools\nTAB  Case file");
        }
    }
}
