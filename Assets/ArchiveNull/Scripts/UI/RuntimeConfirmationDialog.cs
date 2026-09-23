using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class RuntimeConfirmationDialog
{
    public static bool IsOpen { get; private set; }

    public static void Show(string title, string message, string confirmLabel, string cancelLabel, UnityAction onConfirm, UnityAction onCancel = null)
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        Image backdrop = CreateImage("Backdrop", canvasRect, new Color(0f, 0f, 0f, 0.78f));
        Stretch(backdrop.rectTransform);

        // Session sheet: generous spacing and a clear primary/secondary action hierarchy.
        RectTransform panel=CreateImage("Session sheet",canvasRect,new Color(.025f,.046f,.07f,1)).rectTransform;
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f); panel.sizeDelta=new Vector2(960,450);
        TMP_Text badge=CreateText("Session label",panel,"ARCHIVE: NULL   /   SESIÓN",16,FontStyles.Normal,TextAlignmentOptions.TopLeft);
        SetRect(badge.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(52,378),new Vector2(-52,-35)); badge.color=new Color(.43f,.74f,.96f);
        TMP_Text titleText=CreateText("Title",panel,title,38,FontStyles.Normal,TextAlignmentOptions.TopLeft);
        SetRect(titleText.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(52,284),new Vector2(-52,-88));
        TMP_Text messageText=CreateText("Message",panel,message,24,FontStyles.Normal,TextAlignmentOptions.TopLeft);
        SetRect(messageText.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(52,139),new Vector2(-52,-186)); messageText.color=new Color(.62f,.73f,.82f);
        Button cancelButton = CreateButton(panel, cancelLabel, new Vector2(-218f, 67f), () =>
        {
            Object.Destroy(canvasObject);
            onCancel?.Invoke();
        });
        Button confirmButton = CreateButton(panel, confirmLabel, new Vector2(218f, 67f), () =>
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
        RectTransform rect = CreateImage(label + "Button", parent, new Color(.15f,.28f,.4f,1)).rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(408f, 64f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.3f,.5f,.7f,1);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.4f,.65f,.9f,1);
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
        text.color = new Color(.9f,.95f,1,1);
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
