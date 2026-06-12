using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StartupNoticeSequence : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string CurrentNoticeVersion = "0.2-dev";

    private CanvasGroup rootGroup;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text footerText;
    private int pageIndex;
    private bool busy;

    public static bool IsShowing { get; private set; }

    private readonly NoticePage[] pages =
    {
        new("NULL HOUR STUDIO", "Presenta\n\nARCHIVE NULL", "CLICK / ENTER: CONTINUAR"),
        new("NOTA PARA CREADORES", "Se permite grabar, transmitir y comentar el juego.\n\nEl contenido mostrado pertenece a una version universitaria en desarrollo: bugs, cambios visuales y escenas incompletas pueden aparecer durante la captura.", "CLICK / ENTER: CONTINUAR"),
        new("NOTA DE DESARROLLO", "Version 0.2-dev\n\nCambios recientes: expediente inicial, ayudas contextuales, herramientas de investigacion, galeria de evidencias, luz UV, pizarra y autosave preliminar.", "CLICK / ENTER: CONTINUAR"),
        new("PROYECTO UNIVERSITARIO", "Archive Null es un proyecto universitario en desarrollo.\n\nEl objetivo actual es probar mecanicas, experiencia de investigacion, claridad de UX y estructura narrativa. No representa una version final.", "CLICK / ENTER: INICIAR")
    };

    private readonly struct NoticePage
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string Footer;

        public NoticePage(string title, string body, string footer)
        {
            Title = title;
            Body = body;
            Footer = footer;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FindObjectOfType<StartupNoticeSequence>() != null)
        {
            return;
        }

        GameObject host = new("StartupNoticeSequence");
        host.AddComponent<StartupNoticeSequence>();
    }

    private void Awake()
    {
        IsShowing = true;
        BuildUi();
        ShowPage(0);
        StartCoroutine(Fade(0f, 1f));
    }

    private void OnDestroy()
    {
        IsShowing = false;
    }

    private void Update()
    {
        if (busy)
        {
            return;
        }

        bool advance = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        advance |= Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        if (advance)
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (pageIndex >= pages.Length - 1)
        {
            StartCoroutine(CloseRoutine());
            return;
        }

        pageIndex++;
        ShowPage(pageIndex);
    }

    private IEnumerator CloseRoutine()
    {
        yield return Fade(1f, 0f);
        if (GameSaveSystem.TryLoadSavedMemoryScene())
        {
            yield break;
        }

        Destroy(gameObject);
    }

    private void ShowPage(int index)
    {
        pageIndex = Mathf.Clamp(index, 0, pages.Length - 1);
        NoticePage page = pages[pageIndex];
        titleText.text = page.Title;
        bodyText.text = page.Body;
        footerText.text = page.Footer + $"  //  {pageIndex + 1:00}/{pages.Length:00}";
    }

    private IEnumerator Fade(float from, float to)
    {
        busy = true;
        rootGroup.gameObject.SetActive(true);
        float timer = 0f;
        const float duration = 0.22f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / duration));
            yield return null;
        }

        rootGroup.alpha = to;
        rootGroup.blocksRaycasts = to > 0.5f;
        rootGroup.interactable = to > 0.5f;
        rootGroup.gameObject.SetActive(to > 0f);
        busy = false;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new("StartupNoticeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGroup = canvasObject.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        Image background = CreateImage("Background", canvasRect, new Color(0.006f, 0.008f, 0.008f, 0.98f));
        Stretch(background.rectTransform);

        Image panel = CreateImage("NoticePanel", canvasRect, new Color(0.025f, 0.04f, 0.038f, 0.96f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1040f, 620f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.58f, 0.96f, 0.86f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);

        titleText = CreateText("Title", panelRect, 34f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.82f, 0.96f, 0.9f, 1f));
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(64f, -64f), new Vector2(-64f, -24f));

        bodyText = CreateText("Body", panelRect, 25f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.72f, 0.86f, 0.8f, 1f));
        SetRect(bodyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(64f, 118f), new Vector2(-64f, -150f));

        footerText = CreateText("Footer", panelRect, 18f, FontStyles.Bold, TextAlignmentOptions.BottomRight, new Color(0.58f, 0.96f, 0.86f, 0.82f));
        SetRect(footerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(64f, 42f), new Vector2(-64f, -540f));
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
