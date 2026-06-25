using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class RuntimeConfirmationDialog
{
    public static bool IsOpen { get; private set; }

    public static void Show(string title, string message, string confirmLabel, string cancelLabel, UnityAction onConfirm)
    {
        GameObject existing = GameObject.Find("ConfirmationDialog");
        if (existing != null)
        {
            return;
        }

        EnsureEventSystem();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsOpen = true;

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

        ConfirmationDialogInput input = canvasObject.AddComponent<ConfirmationDialogInput>();
        input.Configure(cancelButton, confirmButton);
    }

    public static void NotifyClosed()
    {
        IsOpen = false;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
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
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.06f, 0.1f, 0.095f, 1f);
        colors.highlightedColor = new Color(0.13f, 0.32f, 0.28f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.08f, 0.5f, 0.4f, 1f);
        button.colors = colors;

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

public sealed class ConfirmationDialogInput : MonoBehaviour
{
    private Button cancelButton;
    private Button confirmButton;
    private bool confirmSelected;

    public void Configure(Button cancel, Button confirm)
    {
        cancelButton = cancel;
        confirmButton = confirm;
        Select(false);
    }

    private void OnDestroy()
    {
        RuntimeConfirmationDialog.NotifyClosed();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
            Keyboard.current.aKey.wasPressedThisFrame)
        {
            Select(false);
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                 Keyboard.current.dKey.wasPressedThisFrame)
        {
            Select(true);
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            (confirmSelected ? confirmButton : cancelButton)?.onClick.Invoke();
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            cancelButton?.onClick.Invoke();
        }
    }

    private void Select(bool confirm)
    {
        confirmSelected = confirm;
        Button target = confirm ? confirmButton : cancelButton;
        target?.Select();
    }
}
