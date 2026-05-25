using System.Collections;
using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CrimeSceneTutorial : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string CompletedPref = "archive.tutorial.crime.completed";

    private CanvasGroup rootGroup;
    private TMP_Text text;
    private TMP_Text inspectWaypointLabel;
    private TMP_Text evidenceWaypointLabel;
    private Image inspectWaypointDot;
    private Image evidenceWaypointDot;
    private Camera mainCamera;
    private Transform inspectWaypointTarget;
    private Transform evidenceWaypointTarget;
    private int step;
    private bool evidenceRegistered;

    [Header("Debug")]
    [SerializeField] private bool alwaysShowInEditor = true;
    [SerializeField] private bool resetOnPlayInEditor = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

#if UNITY_EDITOR
        bool skipByCompletion = false;
#else
        bool skipByCompletion = true;
#endif

        if (skipByCompletion && PlayerPrefs.GetInt(CompletedPref, 0) == 1)
        {
            return;
        }

        if (FindObjectOfType<CrimeSceneTutorial>() != null)
        {
            return;
        }

        GameObject host = new GameObject("CrimeSceneTutorial");
        host.AddComponent<CrimeSceneTutorial>();
    }

    private void Awake()
    {
#if UNITY_EDITOR
        if (resetOnPlayInEditor)
        {
            PlayerPrefs.DeleteKey(CompletedPref);
        }
#endif

        mainCamera = Camera.main;
        BuildUi();
        CacheWaypointTargets();
        SetVisible(true);
        RefreshText();
    }

    private void OnEnable()
    {
        EvidenceInventory.Instance.OnEvidenceRegistered += HandleEvidenceRegistered;
    }

    private void OnDisable()
    {
        if (EvidenceInventory.ExistingInstance != null)
        {
            EvidenceInventory.ExistingInstance.OnEvidenceRegistered -= HandleEvidenceRegistered;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!alwaysShowInEditor && PlayerPrefs.GetInt(CompletedPref, 0) == 1)
        {
            StartCoroutine(FadeOutAndDestroy());
            enabled = false;
            return;
        }
#endif

        if (Keyboard.current == null)
        {
            return;
        }

        UpdateWaypointVisuals();

        switch (step)
        {
            case 0:
                if (GlobalInputBindings.IsPressed(GameInputAction.MoveForward) ||
                    GlobalInputBindings.IsPressed(GameInputAction.MoveBackward) ||
                    GlobalInputBindings.IsPressed(GameInputAction.MoveLeft) ||
                    GlobalInputBindings.IsPressed(GameInputAction.MoveRight))
                {
                    Advance();
                }
                break;
            case 1:
                if (InspectObject.IsAnyInspecting)
                {
                    Advance();
                }
                break;
            case 2:
                if (EvidenceCameraController.IsAnyCameraModeActive)
                {
                    Advance();
                }
                break;
            case 3:
                if (evidenceRegistered)
                {
                    Advance();
                }
                break;
            case 4:
                if (GlobalInputBindings.WasPressed(GameInputAction.Notebook))
                {
                    Complete();
                }
                break;
        }
    }

    private void HandleEvidenceRegistered(EvidenceData data)
    {
        evidenceRegistered = true;
    }

    private void Advance()
    {
        step++;
        RefreshText();
    }

    private void Complete()
    {
        PlayerPrefs.SetInt(CompletedPref, 1);
        PlayerPrefs.Save();
        StartCoroutine(FadeOutAndDestroy());
    }

    private void RefreshText()
    {
        if (text == null)
        {
            return;
        }

        text.text = step switch
        {
            0 => $"MEMORIA ACTIVA\nMuevete con {GlobalInputBindings.GetDisplayName(GameInputAction.MoveForward)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveLeft)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveBackward)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveRight)}.",
            1 => $"INSPECCION\nMira el objetivo marcado y pulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Inspect)}. Manten click para girarlo y usa rueda para zoom.",
            2 => $"CAMARA DE EVIDENCIA\nManten G para abrir inventario radial, equipa CAMARA y luego pulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Camera)} para abrirla.",
            3 => "REGISTRO\nEnfoca la evidencia marcada y toma una foto con click izquierdo.",
            4 => $"LIBRETA\nPulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Notebook)} para revisar fotos y escribir notas.",
            _ => string.Empty
        };
    }

    private void CacheWaypointTargets()
    {
        inspectWaypointTarget = FindNearestInspectableTarget();
        evidenceWaypointTarget = FindNearestEvidenceTarget();
    }

    private void UpdateWaypointVisuals()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        if (step == 1)
        {
            inspectWaypointTarget = FindNearestInspectableTarget();
        }
        else if (step == 3)
        {
            evidenceWaypointTarget = FindNearestEvidenceTarget();
        }

        bool showInspect = step == 1 && inspectWaypointTarget != null;
        bool showEvidence = step == 3 && evidenceWaypointTarget != null;
        SetWaypointVisible(inspectWaypointDot, inspectWaypointLabel, showInspect);
        SetWaypointVisible(evidenceWaypointDot, evidenceWaypointLabel, showEvidence);
        if (showInspect)
        {
            UpdateWaypointPosition(inspectWaypointTarget, inspectWaypointDot.rectTransform, inspectWaypointLabel.rectTransform, "INSPECCIONAR");
        }

        if (showEvidence)
        {
            UpdateWaypointPosition(evidenceWaypointTarget, evidenceWaypointDot.rectTransform, evidenceWaypointLabel.rectTransform, "EVIDENCIA");
        }
    }

    private Transform FindNearestInspectableTarget()
    {
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Inspectable");
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        Vector3 origin = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float bestSqrDistance = float.PositiveInfinity;
        Transform best = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = candidate.transform;
            }
        }

        return best;
    }

    private Transform FindNearestEvidenceTarget()
    {
        EvidenceTarget[] candidates = FindObjectsOfType<EvidenceTarget>(true);
        if (candidates == null || candidates.Length == 0)
        {
            return null;
        }

        Vector3 origin = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float bestSqrDistance = float.PositiveInfinity;
        Transform best = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            EvidenceTarget candidate = candidates[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = candidate.transform;
            }
        }

        return best;
    }

    private static void SetWaypointVisible(Image dot, TMP_Text label, bool visible)
    {
        if (dot != null)
        {
            dot.enabled = visible;
        }

        if (label != null)
        {
            label.enabled = visible;
        }
    }

    private void UpdateWaypointPosition(Transform target, RectTransform dot, RectTransform label, string labelText)
    {
        if (target == null || dot == null || label == null || mainCamera == null)
        {
            return;
        }

        Vector3 worldPoint = target.position + Vector3.up * 0.45f;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        bool inFront = screenPoint.z > 0f;
        bool onScreen = inFront && screenPoint.x >= 0f && screenPoint.x <= Screen.width && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
        dot.gameObject.SetActive(onScreen);
        label.gameObject.SetActive(onScreen);
        if (!onScreen)
        {
            return;
        }

        dot.position = screenPoint;
        label.position = screenPoint + new Vector3(0f, 26f, 0f);
        TMP_Text labelTmp = label.GetComponent<TMP_Text>();
        if (labelTmp != null)
        {
            labelTmp.text = labelText;
        }
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("CrimeSceneTutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGroup = canvasObject.GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        Image panel = CreateImage("Panel", canvasObject.transform as RectTransform, new Color(0.025f, 0.035f, 0.034f, 0.86f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(48f, 52f);
        panelRect.sizeDelta = new Vector2(700f, 150f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.9f, 0.82f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        text = CreateText("Text", panelRect);

        inspectWaypointDot = CreateImage("InspectWaypointDot", canvasObject.transform as RectTransform, new Color(0.2f, 1f, 0.9f, 0.95f));
        inspectWaypointDot.rectTransform.sizeDelta = new Vector2(14f, 14f);
        inspectWaypointLabel = CreateText("InspectWaypointLabel", canvasObject.transform as RectTransform);
        inspectWaypointLabel.fontSize = 18f;
        inspectWaypointLabel.alignment = TextAlignmentOptions.Center;
        inspectWaypointLabel.color = new Color(0.2f, 1f, 0.9f, 1f);
        inspectWaypointLabel.raycastTarget = false;

        evidenceWaypointDot = CreateImage("EvidenceWaypointDot", canvasObject.transform as RectTransform, new Color(1f, 0.86f, 0.22f, 0.95f));
        evidenceWaypointDot.rectTransform.sizeDelta = new Vector2(14f, 14f);
        evidenceWaypointLabel = CreateText("EvidenceWaypointLabel", canvasObject.transform as RectTransform);
        evidenceWaypointLabel.fontSize = 18f;
        evidenceWaypointLabel.alignment = TextAlignmentOptions.Center;
        evidenceWaypointLabel.color = new Color(1f, 0.86f, 0.22f, 1f);
        evidenceWaypointLabel.raycastTarget = false;
        SetWaypointVisible(inspectWaypointDot, inspectWaypointLabel, false);
        SetWaypointVisible(evidenceWaypointDot, evidenceWaypointLabel, false);
    }

    private void SetVisible(bool visible)
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = visible ? 1f : 0f;
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float timer = 0f;
        while (timer < 0.35f)
        {
            timer += Time.unscaledDeltaTime;
            if (rootGroup != null)
            {
                rootGroup.alpha = Mathf.Lerp(1f, 0f, timer / 0.35f);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text tmp = child.GetComponent<TMP_Text>();
        tmp.fontSize = 21f;
        tmp.color = new Color(0.78f, 0.98f, 0.92f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(24f, 14f);
        rect.offsetMax = new Vector2(-24f, -14f);
        return tmp;
    }
}
