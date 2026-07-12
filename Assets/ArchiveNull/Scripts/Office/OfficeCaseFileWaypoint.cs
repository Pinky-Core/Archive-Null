using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed class OfficeCaseFileWaypoint : MonoBehaviour
    {
        private const string DiscoveredPref = "archive.office.casefile.discovered";
        private Camera targetCamera;
        private CaseFileReader target;
        private Image marker;
        private TMP_Text label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" || PlayerPrefs.GetInt(DiscoveredPref, 0) == 1 || FindAnyObjectByType<OfficeCaseFileWaypoint>() != null) return;
            new GameObject("OfficeCaseFileWaypoint").AddComponent<OfficeCaseFileWaypoint>();
        }

        private void Awake()
        {
            targetCamera = Camera.main;
            target = FindAnyObjectByType<CaseFileReader>();
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }
            BuildUi();
        }

        private void Update()
        {
            if (CaseFileReader.IsAnyCaseFileOpen)
            {
                PlayerPrefs.SetInt(DiscoveredPref, 1);
                PlayerPrefs.Save();
                Destroy(gameObject);
                return;
            }

            targetCamera ??= Camera.main;
            if (targetCamera == null || target == null) return;
            Vector3 screen = targetCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 0.22f);
            bool visible = screen.z > 0f;
            marker.gameObject.SetActive(visible);
            label.gameObject.SetActive(visible);
            if (!visible) return;
            float margin = 54f;
            screen.x = Mathf.Clamp(screen.x, margin, Screen.width - margin);
            screen.y = Mathf.Clamp(screen.y, margin, Screen.height - margin);
            marker.rectTransform.position = screen;
            label.rectTransform.position = screen + new Vector3(0f, 34f);
        }

        private void BuildUi()
        {
            GameObject canvasObject = new("CaseFileWaypointCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 17500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject markerObject = new("Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObject.transform.SetParent(canvasObject.transform, false);
            marker = markerObject.GetComponent<Image>();
            marker.color = new Color(0.92f, 0.78f, 0.38f, 1f);
            marker.raycastTarget = false;
            marker.rectTransform.sizeDelta = new Vector2(18f, 18f);

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = GameLocalization.Text("EXPEDIENTE ASIGNADO", "ASSIGNED CASE FILE");
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.84f, 0.52f, 1f);
            label.outlineWidth = 0.15f;
            label.outlineColor = Color.black;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(320f, 34f);
        }
    }
}
