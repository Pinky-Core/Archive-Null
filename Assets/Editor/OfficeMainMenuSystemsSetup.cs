using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using ArchiveNull.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OfficeMainMenuSystemsSetup
{
    private const string RootName = "ArchiveNull_OfficeSystems";
    private const string PrefabFolder = "Assets/Prefabs/InvestigationBoard";
    private const string CardPrefabPath = PrefabFolder + "/EvidenceCardUI.prefab";
    private const string LinePrefabPath = PrefabFolder + "/BoardConnectionLine.prefab";

    [MenuItem("Archive Null/Setup/Office Main Menu Systems")]
    public static void SetupOfficeSystems()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);

        EvidenceCardUI cardPrefab = EnsureEvidenceCardPrefab();
        BoardConnectionRenderer linePrefab = EnsureConnectionLinePrefab();

        GameObject root = FindOrCreateRoot(RootName);
        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Archive Null Office Systems");

        EnsureComponent<EvidenceInventory>(GetOrCreateChild(root.transform, "EvidenceSystem"));
        SimpleMessageUI messageUi = EnsureMessageUi(root.transform);
        OfficeSpeakerTutorial tutorial = EnsureTutorial(root.transform);
        EnsureInvestigationBoard(root.transform, cardPrefab, linePrefab);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"Archive Null office systems configured. Message UI: {messageUi.name}, Tutorial: {tutorial.name}");
    }

    private static void EnsureInvestigationBoard(Transform root, EvidenceCardUI cardPrefab, BoardConnectionRenderer linePrefab)
    {
        GameObject canvasObject = GetOrCreateChild(root, "InvestigationBoardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        RectTransform boardRoot = GetOrCreateRect(canvasObject.transform, "BoardRoot");
        Stretch(boardRoot);

        Image background = EnsureComponent<Image>(boardRoot.gameObject);
        background.color = new Color(0.025f, 0.032f, 0.03f, 0.08f);
        background.raycastTarget = false;

        RectTransform zonesContainer = GetOrCreateRect(boardRoot, "ZonesContainer");
        Stretch(zonesContainer);
        RectTransform connectionsContainer = GetOrCreateRect(boardRoot, "ConnectionsContainer");
        Stretch(connectionsContainer);
        RectTransform cardsContainer = GetOrCreateRect(boardRoot, "CardsContainer");
        Stretch(cardsContainer);

        string[] zoneNames =
        {
            "Nicolas",
            "Sofia",
            "Victor",
            "Method",
            "Motive",
            "Alibi",
            "Manipulated Scene",
            "Timeline",
            "Unclassified"
        };

        BoardZone[] zones = new BoardZone[zoneNames.Length];
        const float zoneWidth = 350f;
        const float zoneHeight = 150f;
        Vector2 start = new(-560f, 310f);
        for (int i = 0; i < zoneNames.Length; i++)
        {
            int row = i / 3;
            int column = i % 3;
            RectTransform zoneRect = GetOrCreateRect(zonesContainer, "Zone_" + zoneNames[i]);
            zoneRect.anchorMin = new Vector2(0.5f, 0.5f);
            zoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.sizeDelta = new Vector2(zoneWidth, zoneHeight);
            zoneRect.anchoredPosition = start + new Vector2(column * 390f, -row * 180f);

            Image zoneImage = EnsureComponent<Image>(zoneRect.gameObject);
            zoneImage.color = new Color(0.08f, 0.12f, 0.105f, 0.24f);
            zoneImage.raycastTarget = false;

            BoardZone zone = EnsureComponent<BoardZone>(zoneRect.gameObject);
            SetSerialized(zone, "zoneId", zoneNames[i]);
            SetSerialized(zone, "zoneRect", zoneRect);
            zones[i] = zone;

            TMP_Text label = EnsureText(zoneRect, "Label", zoneNames[i].ToUpperInvariant(), 20f, TextAlignmentOptions.TopLeft);
            label.color = new Color(0.72f, 0.92f, 0.86f, 0.92f);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 10f);
            labelRect.offsetMax = new Vector2(-14f, -10f);
        }

        BoardZoneManager zoneManager = EnsureComponent<BoardZoneManager>(boardRoot.gameObject);
        SetSerialized(zoneManager, "boardCanvas", canvas);
        SetSerialized(zoneManager, "defaultZone", zones[zones.Length - 1]);
        SetSerializedArray(zoneManager, "zones", zones);

        BoardConnectionManager connectionManager = EnsureComponent<BoardConnectionManager>(boardRoot.gameObject);
        SetSerialized(connectionManager, "connectionContainer", connectionsContainer);
        SetSerialized(connectionManager, "connectionPrefab", linePrefab);

        InvestigationBoardController controller = EnsureComponent<InvestigationBoardController>(boardRoot.gameObject);
        SetSerialized(controller, "cardContainer", cardsContainer);
        SetSerialized(controller, "cardPrefab", cardPrefab);
        SetSerialized(controller, "zoneManager", zoneManager);
        SetSerialized(controller, "connectionManager", connectionManager);
        SetSerialized(controller, "firstCardPosition", new Vector2(-690f, -330f));
        SetSerialized(controller, "cardSpacing", new Vector2(230f, 0f));
        SetSerialized(controller, "cardsPerRow", 6);
    }

    private static SimpleMessageUI EnsureMessageUi(Transform root)
    {
        GameObject canvasObject = GetOrCreateChild(root, "OfficeMessageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = GetOrCreateRect(canvasObject.transform, "MessagePanel");
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -42f);
        panel.sizeDelta = new Vector2(780f, 74f);

        Image panelImage = EnsureComponent<Image>(panel.gameObject);
        panelImage.color = new Color(0f, 0f, 0f, 0.62f);
        panelImage.raycastTarget = false;

        CanvasGroup group = EnsureComponent<CanvasGroup>(panel.gameObject);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        TMP_Text text = EnsureText(panel, "MessageText", string.Empty, 26f, TextAlignmentOptions.Center);
        text.color = new Color(0.82f, 0.96f, 0.91f, 1f);
        text.raycastTarget = false;
        Stretch(text.rectTransform, new Vector2(24f, 8f), new Vector2(-24f, -8f));

        SimpleMessageUI messageUi = EnsureComponent<SimpleMessageUI>(panel.gameObject);
        SetSerialized(messageUi, "messageText", text);
        SetSerialized(messageUi, "group", group);
        return messageUi;
    }

    private static OfficeSpeakerTutorial EnsureTutorial(Transform root)
    {
        GameObject tutorialObject = GetOrCreateChild(root, "OfficeSpeakerTutorial", typeof(AudioSource));
        OfficeSpeakerTutorial tutorial = EnsureComponent<OfficeSpeakerTutorial>(tutorialObject);
        AudioSource source = tutorialObject.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        SetSerialized(tutorial, "cameraFocus", Object.FindObjectOfType<CRTMenuCameraFocus>());
        SetSerialized(tutorial, "mainMenuController", Object.FindObjectOfType<CRTMainMenuController>());
        SetSerialized(tutorial, "vrHeadsetStarter", Object.FindObjectOfType<VRHeadsetArchiveStarter>());
        SetSerialized(tutorial, "speakerSource", source);
        return tutorial;
    }

    private static EvidenceCardUI EnsureEvidenceCardPrefab()
    {
        EvidenceCardUI existing = AssetDatabase.LoadAssetAtPath<EvidenceCardUI>(CardPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new("EvidenceCardUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(EvidenceCardUI), typeof(DraggableBoardItem));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(210f, 132f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.055f, 0.075f, 0.066f, 0.95f);
        background.raycastTarget = true;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;

        Image photo = CreateChildImage(rect, "Photo", new Color(0.1f, 0.12f, 0.11f, 1f));
        RectTransform photoRect = photo.rectTransform;
        photoRect.anchorMin = new Vector2(0f, 0.48f);
        photoRect.anchorMax = new Vector2(0f, 1f);
        photoRect.pivot = new Vector2(0f, 1f);
        photoRect.offsetMin = new Vector2(10f, -58f);
        photoRect.offsetMax = new Vector2(72f, -10f);
        photo.raycastTarget = false;

        TMP_Text name = CreateChildText(rect, "NameText", "Evidence", 18f, TextAlignmentOptions.TopLeft);
        SetRectOffsets(name.rectTransform, new Vector2(88f, -12f), new Vector2(-10f, -42f), new Vector2(0f, 1f), new Vector2(1f, 1f));
        name.fontStyle = FontStyles.Bold;

        TMP_Text category = CreateChildText(rect, "CategoryText", "Object", 13f, TextAlignmentOptions.TopLeft);
        SetRectOffsets(category.rectTransform, new Vector2(88f, -45f), new Vector2(-10f, -66f), new Vector2(0f, 1f), new Vector2(1f, 1f));
        category.color = new Color(0.62f, 0.84f, 0.76f, 1f);

        TMP_Text description = CreateChildText(rect, "DescriptionText", "Short description.", 13f, TextAlignmentOptions.TopLeft);
        SetRectOffsets(description.rectTransform, new Vector2(10f, 10f), new Vector2(-10f, 58f), Vector2.zero, Vector2.one);
        description.color = new Color(0.78f, 0.86f, 0.82f, 1f);

        EvidenceCardUI card = root.GetComponent<EvidenceCardUI>();
        SetSerialized(card, "photoImage", photo);
        SetSerialized(card, "nameText", name);
        SetSerialized(card, "categoryText", category);
        SetSerialized(card, "descriptionText", description);
        SetSerialized(card, "selectButton", button);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<EvidenceCardUI>();
    }

    private static BoardConnectionRenderer EnsureConnectionLinePrefab()
    {
        BoardConnectionRenderer existing = AssetDatabase.LoadAssetAtPath<BoardConnectionRenderer>(LinePrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new("BoardConnectionLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoardConnectionRenderer));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 4f);
        Image image = root.GetComponent<Image>();
        image.color = new Color(0.82f, 0.96f, 0.91f, 0.78f);
        image.raycastTarget = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LinePrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<BoardConnectionRenderer>();
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Image CreateChildImage(RectTransform parent, string name, Color color)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        Image image = child.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateChildText(RectTransform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        TMP_Text text = child.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.82f, 0.96f, 0.91f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRectOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject root = GameObject.Find(name);
        if (root != null)
        {
            return root;
        }

        root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        return root;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (components != null)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null && existing.GetComponent(components[i]) == null)
                    {
                        Undo.AddComponent(existing.gameObject, components[i]);
                    }
                }
            }

            return existing.gameObject;
        }

        GameObject child = components == null || components.Length == 0 ? new GameObject(name) : new GameObject(name, components);
        child.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        return child;
    }

    private static RectTransform GetOrCreateRect(Transform parent, string name)
    {
        GameObject child = GetOrCreateChild(parent, name, typeof(RectTransform));
        return child.GetComponent<RectTransform>();
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void SetSerialized(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerialized(Object target, string propertyName, string value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerialized(Object target, string propertyName, Vector2 value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerialized(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetSerializedArray(Object target, string propertyName, Object[] values)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.isArray)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
