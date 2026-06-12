using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class RuntimeConfirmationDialog
{
    public static void Show(string title, string message, string confirmLabel, string cancelLabel, UnityAction onConfirm)
    {
        GameObject canvasObject = new("ConfirmationDialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        Image backdrop = CreateImage("Backdrop", canvasRect, new Color(0f, 0f, 0f, 0.78f));
        Stretch(backdrop.rectTransform);

        RectTransform panel = CreateImage("Panel", canvasRect, new Color(0.025f, 0.04f, 0.038f, 0.98f)).rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(720f, 340f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.95f, 0.86f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text titleText = CreateText("Title", panel, title, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(42f, 236f), new Vector2(-42f, -34f));

        TMP_Text messageText = CreateText("Message", panel, message, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetRect(messageText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(42f, 112f), new Vector2(-42f, -118f));

        Button cancelButton = CreateButton(panel, cancelLabel, new Vector2(-170f, 48f), () => Object.Destroy(canvasObject));
        Button confirmButton = CreateButton(panel, confirmLabel, new Vector2(170f, 48f), () =>
        {
            Object.Destroy(canvasObject);
            onConfirm?.Invoke();
        });

        cancelButton.Select();
    }

    private static Button CreateButton(RectTransform parent, string label, Vector2 position, UnityAction action)
    {
        RectTransform rect = CreateImage(label + "Button", parent, new Color(0.06f, 0.1f, 0.095f, 1f)).rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 54f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        TMP_Text text = CreateText("Label", rect, label, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string value, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.78f, 0.96f, 0.9f, 1f);
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
