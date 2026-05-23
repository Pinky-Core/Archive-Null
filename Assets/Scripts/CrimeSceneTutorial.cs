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
    private int step;
    private bool evidenceRegistered;

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

        if (PlayerPrefs.GetInt(CompletedPref, 0) == 1)
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
        BuildUi();
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
        if (Keyboard.current == null)
        {
            return;
        }

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
                if (GlobalInputBindings.WasPressed(GameInputAction.Inspect))
                {
                    Advance();
                }
                break;
            case 2:
                if (GlobalInputBindings.WasPressed(GameInputAction.Camera))
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
            0 => $"MEMORIA ACTIVA\nMuévete con {GlobalInputBindings.GetDisplayName(GameInputAction.MoveForward)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveLeft)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveBackward)}/{GlobalInputBindings.GetDisplayName(GameInputAction.MoveRight)}.",
            1 => $"INSPECCIÓN\nMira un objeto marcado y pulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Inspect)}. Mantén click para girarlo y usa la rueda para acercarlo.",
            2 => $"CÁMARA DE EVIDENCIA\nPulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Camera)} para abrir o cerrar la cámara. La rueda controla el zoom.",
            3 => "REGISTRO\nEnfoca una evidencia válida y toma una foto con click izquierdo.",
            4 => $"LIBRETA\nPulsa {GlobalInputBindings.GetDisplayName(GameInputAction.Notebook)} para revisar fotos y escribir notas del operador.",
            _ => string.Empty
        };
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
        panelRect.sizeDelta = new Vector2(620f, 132f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.9f, 0.82f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        text = CreateText("Text", panelRect);
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
