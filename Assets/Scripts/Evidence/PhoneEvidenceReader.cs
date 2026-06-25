using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class PhoneEvidenceReader : MonoBehaviour
    {
        public enum UnlockMode
        {
            Click,
            Pin
        }

        private enum PhoneScreen
        {
            Lock,
            Pin,
            Home,
            ChatList,
            Chat,
            Calls,
            Recents
        }

        [System.Serializable]
        private sealed class PhoneChat
        {
            public string contact = "Sofia Roldan";
            public string status = "ultima vez hoy a las 22:16";
            public string preview = "Necesito que hablemos.";
            [TextArea(2, 5)] public string[] conversation =
            {
                "IN|21:48|Llegaste a casa?",
                "OUT|21:51|Si. Tenemos que terminar con esto.",
                "IN|22:03|No por mensaje. Llamame.",
                "OUT|22:14|No puedo seguir con esto. Necesito que hablemos."
            };
        }

        [Header("Inventory")]
        [SerializeField] private string inventoryDisplayName = "TELEFONO";
        [SerializeField] private Sprite inventoryIcon;
        [SerializeField] private Vector3 heldLocalPosition = new(0.22f, -0.25f, 0.48f);
        [SerializeField] private Vector3 heldLocalEuler = new(8f, 172f, 0f);
        [SerializeField] private Vector3 heldLocalScale = Vector3.one;
        [SerializeField] private float pickupDistance = 2.5f;
        [SerializeField] private LayerMask pickupLayers = ~0;

        [Header("Unlock")]
        [SerializeField] private UnlockMode unlockMode = UnlockMode.Pin;
        [SerializeField] private string pin = "2530";
        [SerializeField] private string ownerName = "Julian Herrera";
        [SerializeField] private string lockDate = "17 OCT";

        [Header("Evidence")]
        [SerializeField] private EvidenceTarget evidenceTarget;
        [TextArea(2, 4)]
        [SerializeField] private string pickupNarration = "Este telefono parece pertenecer a Julian. Si puedo desbloquearlo, tal vez conserve algo util.";
        [SerializeField] private Collider[] pickupColliders;

        [Header("Messages")]
        [SerializeField] private PhoneChat[] chats =
        {
            new()
            {
                contact = "Sofia Roldan",
                status = "ultima vez hoy a las 22:16",
                preview = "Necesito que hablemos.",
                conversation = new[]
                {
                    "IN|21:48|Llegaste a casa?",
                    "OUT|21:51|Si. Tenemos que terminar con esto.",
                    "IN|22:03|No por mensaje. Llamame.",
                    "OUT|22:14|No puedo seguir con esto. Necesito que hablemos."
                }
            },
            new()
            {
                contact = "Martin Herrera",
                status = "ultima vez ayer",
                preview = "La copia de la llave sigue donde acordamos.",
                conversation = new[]
                {
                    "OUT|18:32|Necesito entrar sin que Sofia se entere.",
                    "IN|18:40|La copia de la llave sigue donde acordamos.",
                    "OUT|18:42|Despues hablamos."
                }
            },
            new()
            {
                contact = "Numero desconocido",
                status = "sin conexion",
                preview = "No firmes nada hasta que llegue.",
                conversation = new[]
                {
                    "IN|19:42|No firmes nada hasta que llegue.",
                    "OUT|19:44|Quien sos?",
                    "IN|19:45|Alguien que sabe lo que intentan hacer."
                }
            }
        };

        [Header("Calls")]
        [SerializeField] private string[] callRecords =
        {
            "MISSED|Sofia Roldan|22:14|Llamada perdida",
            "OUT|Martin Herrera|20:08|02:41",
            "IN|Estudio Valdez|18:32|05:12",
            "MISSED|Numero desconocido|17:56|Llamada perdida"
        };

        [Header("Presentation")]
        [SerializeField] private float powerOnDuration = 0.32f;
        [SerializeField] private Color accentColor = new(0.2f, 0.82f, 0.72f, 1f);

        [Header("Application Icons")]
        [SerializeField] private Sprite messagesIcon;
        [SerializeField] private Sprite callsIcon;
        [SerializeField] private Sprite galleryIcon;
        [SerializeField] private Sprite mailIcon;
        [SerializeField] private Sprite notesIcon;
        [SerializeField] private Sprite settingsIcon;

        [Header("Navigation Icons")]
        [SerializeField] private Sprite recentsIcon;
        [SerializeField] private Sprite homeIcon;
        [SerializeField] private Sprite backIcon;

        private Camera playerCamera;
        private static PhoneEvidenceReader equippedPhone;
        private bool collected;
        private bool equipped;
        private bool unlocked;
        private PhoneScreen currentScreen = PhoneScreen.Lock;
        private string enteredPin = string.Empty;
        private int selectedItem;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private Coroutine powerRoutine;

        private CanvasGroup phoneGroup;
        private RectTransform phoneRoot;
        private TMP_Text statusTime;
        private TMP_Text headerText;
        private TMP_Text contentTitle;
        private TMP_Text contentBody;
        private TMP_Text pinDots;
        private RectTransform homeGrid;
        private RectTransform pinGrid;
        private RectTransform chatListViewport;
        private RectTransform callsViewport;
        private RectTransform chatListRoot;
        private RectTransform callsRoot;
        private RectTransform conversationRoot;
        private RectTransform recentsRoot;
        private RectTransform navigationBarRoot;
        private ScrollRect conversationScroll;
        private TMP_InputField chatComposer;
        private readonly List<Button> selectableButtons = new();
        private readonly List<Button> homeButtons = new();
        private readonly List<Button> pinButtons = new();
        private Button unlockButton;
        private Button backButton;
        private Button recentsButton;
        private Button homeButton;
        private Button recentAppButton;
        private int selectedChat;
        private PhoneScreen previousAppScreen = PhoneScreen.Home;

        public string InventoryDisplayName => inventoryDisplayName;
        public Sprite InventoryIcon => inventoryIcon;
        public bool IsCollected => collected;
        public bool IsEquipped => equipped;
        public bool IsOpen => equipped;
        public static bool IsAnyOpen =>
            equippedPhone != null &&
            equippedPhone.equipped &&
            equippedPhone.gameObject.activeInHierarchy;

        private void Awake()
        {
            collected = false;
            equipped = false;
            if (equippedPhone == this)
            {
                equippedPhone = null;
            }

            evidenceTarget ??= GetComponent<EvidenceTarget>();
            playerCamera = Camera.main;
            if (pickupColliders == null || pickupColliders.Length == 0)
            {
                pickupColliders = GetComponentsInChildren<Collider>(true);
            }

            EnsureEventSystem();
            BuildPhoneUi();
            SetUiVisible(false, true);
        }

        private void OnDisable()
        {
            StopPowerRoutine();
            if (equippedPhone == this)
            {
                equippedPhone = null;
            }

            SetUiVisible(false, true);
        }

        private void OnEnable()
        {
            if (equipped)
            {
                StartPowerOn();
            }
        }

        private void Update()
        {
            if (collected || equipped || !GlobalInputBindings.WasPressed(GameInputAction.Interact))
            {
                return;
            }

            playerCamera ??= Camera.main;
            if (playerCamera == null)
            {
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance, pickupLayers, QueryTriggerInteraction.Collide))
            {
                return;
            }

            PhoneEvidenceReader phone = hit.collider.GetComponentInParent<PhoneEvidenceReader>();
            if (phone == this)
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            EvidenceCameraController controller = FindObjectOfType<EvidenceCameraController>();
            if (controller == null)
            {
                return;
            }

            collected = true;
            if (!controller.RegisterCollectedPhone(this))
            {
                collected = false;
                return;
            }

            SetPickupColliders(false);
            if (evidenceTarget != null && evidenceTarget.CanRegister(out _))
            {
                EvidenceData data = evidenceTarget.CreateCapturedEvidence(null);
                if (data != null && string.IsNullOrWhiteSpace(data.narrativeLine))
                {
                    data.narrativeLine = pickupNarration;
                }

                EvidenceInventory.Instance.RegisterEvidence(data);
            }
        }

        public void Open()
        {
            Collect();
        }

        public void AttachToInventory(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            transform.SetParent(anchor, false);
            transform.localPosition = heldLocalPosition;
            transform.localRotation = Quaternion.Euler(heldLocalEuler);
            transform.localScale = heldLocalScale;
            SetPickupColliders(false);
            gameObject.SetActive(false);
        }

        public void SetEquippedState(bool value)
        {
            equipped = value;
            equippedPhone = value ? this : equippedPhone == this ? null : equippedPhone;
            if (!value)
            {
                StopPowerRoutine();
                SetUiVisible(false, true);
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                return;
            }

            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            currentScreen = unlocked ? PhoneScreen.Home : PhoneScreen.Lock;
            enteredPin = string.Empty;
            selectedItem = 0;
            RefreshScreen();
            if (gameObject.activeInHierarchy)
            {
                StartPowerOn();
            }
        }

        public void HandleEquippedInput()
        {
            if (!equipped)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    if (chatComposer != null && chatComposer.isFocused)
                    {
                        chatComposer.DeactivateInputField();
                        return;
                    }

                    NavigateBack();
                }

                if (unlockMode == UnlockMode.Pin && currentScreen == PhoneScreen.Pin)
                {
                    HandlePinKeyboard();
                }

                int direction = 0;
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) direction = 1;
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) direction = -1;
                if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) direction = 1;
                if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) direction = -1;
                if (direction != 0)
                {
                    CycleSelection(direction);
                }

                if (Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    ActivateSelection();
                }
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll < -0.01f) CycleSelection(1);
                if (scroll > 0.01f) CycleSelection(-1);
            }
        }

        private void HandlePinKeyboard()
        {
            Key[] keys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (Keyboard.current[keys[i]].wasPressedThisFrame)
                {
                    EnterPinDigit(i);
                    return;
                }
            }
        }

        private void StartPowerOn()
        {
            StopPowerRoutine();
            powerRoutine = StartCoroutine(PowerOnRoutine());
        }

        private IEnumerator PowerOnRoutine()
        {
            SetUiVisible(true, true);
            phoneGroup.alpha = 0f;
            phoneRoot.anchoredPosition += new Vector2(0f, -90f);
            Vector2 target = phoneRoot.anchoredPosition + new Vector2(0f, 90f);
            Vector2 start = phoneRoot.anchoredPosition;
            float timer = 0f;
            while (timer < powerOnDuration && equipped)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, timer / Mathf.Max(0.01f, powerOnDuration));
                phoneGroup.alpha = t;
                phoneRoot.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }

            phoneGroup.alpha = equipped ? 1f : 0f;
            phoneRoot.anchoredPosition = target;
            powerRoutine = null;
        }

        private void StopPowerRoutine()
        {
            if (powerRoutine != null)
            {
                StopCoroutine(powerRoutine);
                powerRoutine = null;
            }
        }

        private void OpenUnlock()
        {
            if (unlockMode == UnlockMode.Click)
            {
                Unlock();
                return;
            }

            currentScreen = PhoneScreen.Pin;
            enteredPin = string.Empty;
            RefreshScreen();
        }

        private void EnterPinDigit(int digit)
        {
            if (currentScreen != PhoneScreen.Pin || enteredPin.Length >= 4)
            {
                return;
            }

            enteredPin += digit.ToString();
            RefreshPinDots();
            if (enteredPin.Length == 4)
            {
                StartCoroutine(ValidatePinRoutine());
            }
        }

        private IEnumerator ValidatePinRoutine()
        {
            yield return new WaitForSecondsRealtime(0.12f);
            if (enteredPin == NormalizePin(pin))
            {
                Unlock();
                yield break;
            }

            if (pinDots != null)
            {
                pinDots.text = "PIN INCORRECTO";
                pinDots.color = new Color(1f, 0.35f, 0.32f);
            }

            yield return new WaitForSecondsRealtime(0.65f);
            enteredPin = string.Empty;
            RefreshPinDots();
        }

        private void Unlock()
        {
            unlocked = true;
            currentScreen = PhoneScreen.Home;
            selectedItem = 0;
            RefreshScreen();
        }

        private void OpenScreen(PhoneScreen screen)
        {
            if (screen != PhoneScreen.Home && screen != PhoneScreen.Lock && screen != PhoneScreen.Pin && screen != PhoneScreen.Recents)
            {
                previousAppScreen = screen;
            }

            currentScreen = screen;
            selectedItem = 0;
            RefreshScreen();
        }

        private void NavigateHome()
        {
            if (!unlocked)
            {
                currentScreen = PhoneScreen.Lock;
            }
            else
            {
                RememberCurrentApp();
                currentScreen = PhoneScreen.Home;
            }

            selectedItem = 0;
            RefreshScreen();
        }

        private void OpenRecents()
        {
            if (!unlocked)
            {
                return;
            }

            RememberCurrentApp();
            currentScreen = PhoneScreen.Recents;
            selectedItem = 0;
            RefreshScreen();
        }

        private void ResumeRecentApp()
        {
            currentScreen = previousAppScreen == PhoneScreen.Home ? PhoneScreen.ChatList : previousAppScreen;
            selectedItem = 0;
            RefreshScreen();
        }

        private void RememberCurrentApp()
        {
            if (currentScreen != PhoneScreen.Home &&
                currentScreen != PhoneScreen.Lock &&
                currentScreen != PhoneScreen.Pin &&
                currentScreen != PhoneScreen.Recents)
            {
                previousAppScreen = currentScreen == PhoneScreen.Chat ? PhoneScreen.ChatList : currentScreen;
            }
        }

        private void NavigateBack()
        {
            if (currentScreen == PhoneScreen.Home || currentScreen == PhoneScreen.Lock)
            {
                return;
            }

            if (currentScreen == PhoneScreen.Chat)
            {
                currentScreen = PhoneScreen.ChatList;
                selectedItem = selectedChat;
                RefreshScreen();
                return;
            }

            if (currentScreen == PhoneScreen.Recents)
            {
                currentScreen = PhoneScreen.Home;
                RefreshScreen();
                return;
            }

            currentScreen = unlocked ? PhoneScreen.Home : PhoneScreen.Lock;
            selectedItem = 0;
            RefreshScreen();
        }

        private void CycleSelection(int direction)
        {
            if (selectableButtons.Count > 0)
            {
                selectedItem = (selectedItem + direction + selectableButtons.Count) % selectableButtons.Count;
                selectableButtons[selectedItem].Select();
            }
        }

        private void ActivateSelection()
        {
            if (currentScreen == PhoneScreen.Lock)
            {
                OpenUnlock();
                return;
            }

            if (selectableButtons.Count > 0)
            {
                selectableButtons[Mathf.Clamp(selectedItem, 0, selectableButtons.Count - 1)].onClick.Invoke();
            }
        }

        private void RefreshScreen()
        {
            selectableButtons.Clear();
            SetActive(homeGrid, currentScreen == PhoneScreen.Home);
            SetActive(pinGrid, currentScreen == PhoneScreen.Pin);
            SetActive(pinDots, currentScreen == PhoneScreen.Pin);
            SetActive(chatListViewport, currentScreen == PhoneScreen.ChatList);
            SetActive(callsViewport, currentScreen == PhoneScreen.Calls);
            SetActive(conversationRoot, currentScreen == PhoneScreen.Chat);
            SetActive(recentsRoot, currentScreen == PhoneScreen.Recents);
            SetActive(navigationBarRoot, unlocked);
            SetActive(contentTitle, currentScreen == PhoneScreen.Lock);
            SetActive(contentBody, currentScreen == PhoneScreen.Lock);
            SetActive(unlockButton, currentScreen == PhoneScreen.Lock);
            SetActive(backButton, currentScreen != PhoneScreen.Lock && currentScreen != PhoneScreen.Home);

            if (currentScreen == PhoneScreen.Lock && unlockButton != null)
            {
                selectableButtons.Add(unlockButton);
            }
            else if (currentScreen == PhoneScreen.Pin)
            {
                selectableButtons.AddRange(pinButtons);
            }
            else if (currentScreen == PhoneScreen.Home)
            {
                selectableButtons.AddRange(homeButtons);
            }
            else if (currentScreen == PhoneScreen.ChatList)
            {
                BuildChatList();
            }
            else if (currentScreen == PhoneScreen.Calls)
            {
                BuildCallList();
            }
            else if (currentScreen == PhoneScreen.Recents && recentAppButton != null)
            {
                selectableButtons.Add(recentAppButton);
            }

            statusTime.text = System.DateTime.Now.ToString("HH:mm");
            headerText.text = currentScreen switch
            {
                PhoneScreen.Lock => ownerName.ToUpperInvariant(),
                PhoneScreen.Pin => "DESBLOQUEAR",
                PhoneScreen.Home => ownerName,
                PhoneScreen.ChatList => "Mensajes",
                PhoneScreen.Chat => chats != null && chats.Length > 0 ? chats[Mathf.Clamp(selectedChat, 0, chats.Length - 1)].contact : "Chat",
                PhoneScreen.Calls => "Registro de llamadas",
                PhoneScreen.Recents => "Aplicaciones recientes",
                _ => ownerName
            };

            RefreshContent();
            if (currentScreen == PhoneScreen.Chat)
            {
                BuildConversation();
            }

            if (currentScreen == PhoneScreen.Pin)
            {
                RefreshPinDots();
            }

            SelectCurrentButton();
        }

        private void RefreshContent()
        {
            if (currentScreen == PhoneScreen.Lock)
            {
                contentTitle.text = System.DateTime.Now.ToString("HH:mm");
                contentBody.text = lockDate + "\n1 NOTIFICACION";
                return;
            }

        }

        private void RefreshPinDots()
        {
            if (pinDots == null)
            {
                return;
            }

            pinDots.color = Color.white;
            pinDots.text = string.Concat(
                enteredPin.Length > 0 ? "● " : "○ ",
                enteredPin.Length > 1 ? "● " : "○ ",
                enteredPin.Length > 2 ? "● " : "○ ",
                enteredPin.Length > 3 ? "●" : "○");
        }

        private void BuildPhoneUi()
        {
            GameObject canvasObject = new("PhoneOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            phoneGroup = canvasObject.GetComponent<CanvasGroup>();

            phoneRoot = CreateRect("Phone", canvasObject.transform as RectTransform);
            phoneRoot.anchorMin = new Vector2(1f, 0f);
            phoneRoot.anchorMax = new Vector2(1f, 0f);
            phoneRoot.pivot = new Vector2(1f, 0f);
            phoneRoot.anchoredPosition = new Vector2(-62f, 26f);
            phoneRoot.sizeDelta = new Vector2(430f, 840f);

            Image shadow = CreateImage("Shadow", phoneRoot, new Color(0f, 0f, 0f, 0.42f));
            Stretch(shadow.rectTransform, new Vector2(-16f, -18f), new Vector2(18f, 12f));

            Image shell = CreateImage("Shell", phoneRoot, new Color(0.025f, 0.028f, 0.032f, 1f));
            Stretch(shell.rectTransform);
            AddOutline(shell, new Color(0.34f, 0.38f, 0.42f, 1f), 3f);

            RectTransform screen = CreateRect("Screen", phoneRoot);
            screen.anchorMin = Vector2.zero;
            screen.anchorMax = Vector2.one;
            screen.offsetMin = new Vector2(16f, 18f);
            screen.offsetMax = new Vector2(-16f, -18f);
            CreateImage("ScreenBackground", screen, new Color(0.045f, 0.06f, 0.072f, 1f)).rectTransform.SetAsFirstSibling();
            Stretch(screen.GetChild(0) as RectTransform);

            Image topBar = CreateImage("StatusBar", screen, new Color(0.02f, 0.025f, 0.03f, 0.96f));
            SetRect(topBar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -42f), Vector2.zero);
            statusTime = CreateText("Time", topBar.rectTransform, 16f, FontStyles.Bold, TextAlignmentOptions.Left);
            Stretch(statusTime.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            statusTime.text = "22:14";

            TMP_Text signal = CreateText("Signal", topBar.rectTransform, 15f, FontStyles.Normal, TextAlignmentOptions.Right);
            Stretch(signal.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            signal.text = "4G  ▮▮▮  37%";

            headerText = CreateText("Header", screen, 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetRect(headerText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(22f, -94f), new Vector2(-22f, -48f));

            contentTitle = CreateText("ContentTitle", screen, 18f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            SetRect(contentTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(24f, 560f), new Vector2(-24f, -122f));
            contentBody = CreateText("ContentBody", screen, 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetRect(contentBody.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(24f, 118f), new Vector2(-24f, -210f));

            unlockButton = CreateButton("Unlock", screen, "TOCAR PARA DESBLOQUEAR", accentColor, OpenUnlock);
            SetRect(unlockButton.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-145f, 102f), new Vector2(145f, 160f));

            pinDots = CreateText("PinDots", screen, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(pinDots.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(24f, -190f), new Vector2(-24f, -126f));

            pinGrid = CreateRect("PinGrid", screen);
            SetRect(pinGrid, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(38f, 130f), new Vector2(-38f, -220f));
            GridLayoutGroup pinLayout = pinGrid.gameObject.AddComponent<GridLayoutGroup>();
            pinLayout.cellSize = new Vector2(88f, 76f);
            pinLayout.spacing = new Vector2(14f, 12f);
            pinLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            pinLayout.constraintCount = 3;
            for (int i = 1; i <= 9; i++)
            {
                int digit = i;
                pinButtons.Add(CreateButton("Digit" + i, pinGrid, i.ToString(), new Color(0.12f, 0.15f, 0.17f, 1f), () => EnterPinDigit(digit)));
            }

            CreateButton("Empty", pinGrid, "", Color.clear, null).interactable = false;
            pinButtons.Add(CreateButton("Digit0", pinGrid, "0", new Color(0.12f, 0.15f, 0.17f, 1f), () => EnterPinDigit(0)));
            pinButtons.Add(CreateButton("Delete", pinGrid, "BORRAR", new Color(0.16f, 0.11f, 0.11f, 1f), DeletePinDigit));

            homeGrid = CreateRect("HomeGrid", screen);
            SetRect(homeGrid, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(30f, 156f), new Vector2(-30f, -142f));
            GridLayoutGroup homeLayout = homeGrid.gameObject.AddComponent<GridLayoutGroup>();
            homeLayout.cellSize = new Vector2(145f, 126f);
            homeLayout.spacing = new Vector2(30f, 28f);
            homeLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            homeLayout.constraintCount = 2;
            homeButtons.Add(CreateAppButton(homeGrid, "MENSAJES", chats?.Length.ToString() ?? "0", messagesIcon, new Color(0.14f, 0.66f, 0.42f), () => OpenScreen(PhoneScreen.ChatList)));
            homeButtons.Add(CreateAppButton(homeGrid, "LLAMADAS", callRecords?.Length.ToString() ?? "0", callsIcon, new Color(0.18f, 0.55f, 0.38f), () => OpenScreen(PhoneScreen.Calls)));
            CreateLockedApp(homeGrid, "GALERIA", galleryIcon, new Color(0.28f, 0.48f, 0.82f));
            CreateLockedApp(homeGrid, "CORREO", mailIcon, new Color(0.72f, 0.34f, 0.22f));
            CreateLockedApp(homeGrid, "NOTAS", notesIcon, new Color(0.72f, 0.62f, 0.18f));
            CreateLockedApp(homeGrid, "AJUSTES", settingsIcon, new Color(0.34f, 0.38f, 0.44f));

            chatListRoot = CreateListRoot("ChatList", screen, new Vector2(18f, 82f), new Vector2(-18f, -108f), out chatListViewport);
            callsRoot = CreateListRoot("CallList", screen, new Vector2(18f, 82f), new Vector2(-18f, -108f), out callsViewport);
            BuildConversationUi(screen);

            recentsRoot = CreateRect("Recents", screen);
            SetRect(recentsRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(28f, 112f), new Vector2(-28f, -132f));
            Image recentsBackground = recentsRoot.gameObject.AddComponent<Image>();
            recentsBackground.color = new Color(0.04f, 0.052f, 0.06f, 1f);
            recentAppButton = CreateButton("RecentApp", recentsRoot, "ULTIMA APLICACION\nTOCAR PARA CONTINUAR", new Color(0.1f, 0.14f, 0.16f, 1f), ResumeRecentApp);
            SetRect(recentAppButton.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -100f), new Vector2(150f, 100f));

            Image navigationBar = CreateImage("NavigationBar", screen, new Color(0.018f, 0.022f, 0.026f, 1f));
            navigationBarRoot = navigationBar.rectTransform;
            SetRect(navigationBar.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 66f));

            recentsButton = CreateNavigationButton("Recents", navigationBar.rectTransform, "□", recentsIcon, OpenRecents);
            SetRect(recentsButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0.333f, 1f), new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            homeButton = CreateNavigationButton("Home", navigationBar.rectTransform, "○", homeIcon, NavigateHome);
            SetRect(homeButton.transform as RectTransform, new Vector2(0.333f, 0f), new Vector2(0.666f, 1f), new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            backButton = CreateNavigationButton("Back", navigationBar.rectTransform, "<", backIcon, NavigateBack);
            SetRect(backButton.transform as RectTransform, new Vector2(0.666f, 0f), Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), new Vector2(-4f, -4f));

            phoneGroup.alpha = 0f;
            phoneGroup.interactable = false;
            phoneGroup.blocksRaycasts = false;
            RefreshScreen();
        }

        private RectTransform CreateListRoot(
            string name,
            RectTransform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            out RectTransform viewport)
        {
            viewport = CreateRect(name + "Viewport", parent);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = offsetMin;
            viewport.offsetMax = offsetMax;
            Image maskImage = viewport.gameObject.AddComponent<Image>();
            maskImage.color = new Color(0.035f, 0.045f, 0.052f, 0.96f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(name, viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        private void BuildChatList()
        {
            ClearChildren(chatListRoot);
            if (chats == null)
            {
                return;
            }

            for (int i = 0; i < chats.Length; i++)
            {
                int index = i;
                PhoneChat chat = chats[i];
                Button button = CreateButton("Chat_" + i, chatListRoot,
                    $"<b>{chat.contact}</b>\n<size=15><color=#A5B1B4>{chat.preview}</color></size>",
                    new Color(0.07f, 0.09f, 0.1f, 1f),
                    () => OpenChat(index));
                LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 86f;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                selectableButtons.Add(button);
            }

            SelectCurrentButton();
        }

        private void BuildCallList()
        {
            ClearChildren(callsRoot);
            if (callRecords == null)
            {
                return;
            }

            foreach (string record in callRecords)
            {
                string[] parts = (record ?? string.Empty).Split('|');
                string type = parts.Length > 0 ? parts[0] : "IN";
                string contact = parts.Length > 1 ? parts[1] : "Desconocido";
                string time = parts.Length > 2 ? parts[2] : "--:--";
                string duration = parts.Length > 3 ? parts[3] : string.Empty;
                string icon = type == "MISSED" ? "PERDIDA" : type == "OUT" ? "SALIENTE" : "ENTRANTE";
                Color color = type == "MISSED"
                    ? new Color(0.55f, 0.12f, 0.12f)
                    : new Color(0.07f, 0.14f, 0.12f);
                Button row = CreateButton("Call", callsRoot,
                    $"<b>{contact}</b>  <size=14>{time}</size>\n<size=15>{icon}  {duration}</size>",
                    color, null);
                row.interactable = false;
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 78f;
                row.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private void BuildConversationUi(RectTransform screen)
        {
            conversationRoot = CreateRect("Conversation", screen);
            conversationRoot.anchorMin = Vector2.zero;
            conversationRoot.anchorMax = Vector2.one;
            conversationRoot.offsetMin = new Vector2(16f, 78f);
            conversationRoot.offsetMax = new Vector2(-16f, -104f);

            RectTransform viewport = CreateRect("MessagesViewport", conversationRoot);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, 72f);
            viewport.offsetMax = Vector2.zero;
            Image background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(0.055f, 0.072f, 0.068f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("MessagesContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 14, 14);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            conversationScroll = viewport.gameObject.AddComponent<ScrollRect>();
            conversationScroll.viewport = viewport;
            conversationScroll.content = content;
            conversationScroll.horizontal = false;
            conversationScroll.vertical = true;

            RectTransform composer = CreateRect("Composer", conversationRoot);
            composer.anchorMin = new Vector2(0f, 0f);
            composer.anchorMax = new Vector2(1f, 0f);
            composer.pivot = new Vector2(0.5f, 0f);
            composer.sizeDelta = new Vector2(0f, 62f);
            Image composerBackground = composer.gameObject.AddComponent<Image>();
            composerBackground.color = new Color(0.04f, 0.05f, 0.055f, 1f);

            Image inputBackground = CreateImage("Input", composer, new Color(0.11f, 0.13f, 0.14f, 1f));
            SetRect(inputBackground.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 8f), new Vector2(-62f, -8f));
            TMP_Text inputText = CreateText("Text", inputBackground.rectTransform, 17f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(inputText.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            TMP_Text placeholder = CreateText("Placeholder", inputBackground.rectTransform, 17f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            Stretch(placeholder.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            placeholder.text = "Escribir mensaje...";
            placeholder.color = new Color(0.55f, 0.6f, 0.62f);
            chatComposer = inputBackground.gameObject.AddComponent<TMP_InputField>();
            chatComposer.textComponent = inputText;
            chatComposer.placeholder = placeholder;
            chatComposer.lineType = TMP_InputField.LineType.SingleLine;

            Button send = CreateButton("Send", composer, ">", accentColor, SendChatMessage);
            SetRect(send.transform as RectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-54f, 8f), new Vector2(-8f, -8f));
        }

        private void OpenChat(int index)
        {
            selectedChat = Mathf.Clamp(index, 0, Mathf.Max(0, (chats?.Length ?? 1) - 1));
            currentScreen = PhoneScreen.Chat;
            selectedItem = 0;
            RefreshScreen();
        }

        private void BuildConversation()
        {
            if (conversationScroll == null)
            {
                return;
            }

            RectTransform content = conversationScroll.content;
            ClearChildren(content);
            if (chats == null || chats.Length == 0)
            {
                return;
            }

            PhoneChat chat = chats[Mathf.Clamp(selectedChat, 0, chats.Length - 1)];
            headerText.text = chat.contact + "\n<size=12><color=#9DAAAD>" + chat.status + "</color></size>";
            foreach (string raw in chat.conversation)
            {
                CreateChatBubble(content, raw);
            }

            Canvas.ForceUpdateCanvases();
            conversationScroll.verticalNormalizedPosition = 0f;
        }

        private void CreateChatBubble(RectTransform parent, string raw)
        {
            string[] parts = (raw ?? string.Empty).Split('|');
            bool outgoing = parts.Length > 0 && parts[0] == "OUT";
            string time = parts.Length > 1 ? parts[1] : string.Empty;
            string message = parts.Length > 2 ? parts[2] : string.Empty;

            RectTransform row = CreateRect("MessageRow", parent);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();

            Image bubble = CreateImage("Bubble", row, outgoing
                ? new Color(0.08f, 0.34f, 0.28f, 1f)
                : new Color(0.12f, 0.14f, 0.15f, 1f));
            bubble.rectTransform.anchorMin = new Vector2(outgoing ? 0.24f : 0f, 0f);
            bubble.rectTransform.anchorMax = new Vector2(outgoing ? 0.98f : 0.74f, 1f);
            bubble.rectTransform.offsetMin = new Vector2(outgoing ? 4f : 0f, 0f);
            bubble.rectTransform.offsetMax = new Vector2(outgoing ? 0f : -4f, 0f);

            TMP_Text text = CreateText("Message", bubble.rectTransform, 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            text.text = message;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 25f);
            text.rectTransform.offsetMax = new Vector2(-12f, -8f);

            TMP_Text timestamp = CreateText("Timestamp", bubble.rectTransform, 11f, FontStyles.Normal, TextAlignmentOptions.BottomRight);
            timestamp.text = time + (outgoing ? "  OK" : string.Empty);
            timestamp.color = new Color(0.68f, 0.75f, 0.75f, 1f);
            timestamp.rectTransform.anchorMin = new Vector2(0f, 0f);
            timestamp.rectTransform.anchorMax = new Vector2(1f, 0f);
            timestamp.rectTransform.pivot = new Vector2(1f, 0f);
            timestamp.rectTransform.offsetMin = new Vector2(12f, 5f);
            timestamp.rectTransform.offsetMax = new Vector2(-10f, 23f);

            const float availableTextWidth = 245f;
            Vector2 preferred = text.GetPreferredValues(message, availableTextWidth, 0f);
            rowLayout.preferredHeight = Mathf.Max(64f, preferred.y + 43f);
        }

        private void SendChatMessage()
        {
            if (chatComposer == null || string.IsNullOrWhiteSpace(chatComposer.text) || chats == null || chats.Length == 0)
            {
                return;
            }

            PhoneChat chat = chats[Mathf.Clamp(selectedChat, 0, chats.Length - 1)];
            List<string> updated = new(chat.conversation ?? System.Array.Empty<string>())
            {
                "OUT|" + System.DateTime.Now.ToString("HH:mm") + "|" + chatComposer.text.Trim()
            };
            chat.conversation = updated.ToArray();
            chat.preview = chatComposer.text.Trim();
            chatComposer.text = string.Empty;
            BuildConversation();
            chatComposer.ActivateInputField();
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        private void SelectCurrentButton()
        {
            if (selectableButtons.Count == 0)
            {
                return;
            }

            selectedItem = Mathf.Clamp(selectedItem, 0, selectableButtons.Count - 1);
            selectableButtons[selectedItem].Select();
        }

        private void DeletePinDigit()
        {
            if (enteredPin.Length > 0)
            {
                enteredPin = enteredPin[..^1];
                RefreshPinDots();
            }
        }

        private Button CreateAppButton(RectTransform parent, string label, string badge, Sprite icon, Color color, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(label, parent, label + "\n<size=15>" + badge + "</size>", color, action);
            button.GetComponentInChildren<TMP_Text>().fontSize = 19f;
            ApplyButtonIcon(button, icon, label + "\n<size=15>" + badge + "</size>", new Vector2(48f, 48f));
            return button;
        }

        private void CreateLockedApp(RectTransform parent, string label, Sprite icon, Color color)
        {
            Button button = CreateButton(label, parent, label + "\n<size=13>NO DISPONIBLE</size>", color * new Color(0.65f, 0.65f, 0.65f, 1f), null);
            ApplyButtonIcon(button, icon, label + "\n<size=13>NO DISPONIBLE</size>", new Vector2(44f, 44f));
            button.interactable = false;
        }

        private Button CreateNavigationButton(
            string name,
            RectTransform parent,
            string fallback,
            Sprite icon,
            UnityEngine.Events.UnityAction action)
        {
            Button button = CreateButton(name, parent, fallback, Color.clear, action);
            ApplyButtonIcon(button, icon, fallback, new Vector2(30f, 30f));
            return button;
        }

        private static void ApplyButtonIcon(Button button, Sprite icon, string fallbackText, Vector2 iconSize)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (icon == null)
            {
                if (label != null)
                {
                    label.text = fallbackText;
                }
                return;
            }

            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Bottom;
                label.fontSize = 14f;
                label.text = fallbackText;
                label.rectTransform.offsetMin = new Vector2(4f, 4f);
                label.rectTransform.offsetMax = new Vector2(-4f, -60f);
            }

            Image iconImage = CreateImage("Icon", button.transform as RectTransform, Color.white);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.sizeDelta = iconSize;
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        }

        private void SetUiVisible(bool visible, bool immediate)
        {
            if (phoneGroup == null)
            {
                return;
            }

            phoneGroup.gameObject.SetActive(visible);
            phoneGroup.interactable = visible;
            phoneGroup.blocksRaycasts = visible;
            if (immediate)
            {
                phoneGroup.alpha = visible ? 1f : 0f;
            }
        }

        private void SetPickupColliders(bool value)
        {
            if (pickupColliders == null)
            {
                return;
            }

            foreach (Collider pickupCollider in pickupColliders)
            {
                if (pickupCollider != null)
                {
                    pickupCollider.enabled = value;
                }
            }
        }

        private static string NormalizePin(string value)
        {
            string result = string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (char character in value)
                {
                    if (char.IsDigit(character) && result.Length < 4)
                    {
                        result += character;
                    }
                }
            }

            return result.PadRight(4, '0');
        }

        private static Color GetGalleryColor(int index)
        {
            Color[] colors =
            {
                new(0.3f, 0.23f, 0.18f),
                new(0.2f, 0.27f, 0.3f),
                new(0.38f, 0.36f, 0.25f)
            };
            return colors[Mathf.Abs(index) % colors.Length];
        }

        private static void EnsureEventSystem()
        {
            // Scenes normally provide the input module; this fallback only guarantees an EventSystem.
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new("EventSystem", typeof(EventSystem));
            Object.DontDestroyOnLoad(eventSystem);
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Color color, UnityEngine.Events.UnityAction action)
        {
            Image image = CreateImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.38f);
            button.colors = colors;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TMP_Text text = CreateText("Label", image.rectTransform, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, new Vector2(8f, 6f), new Vector2(-8f, -6f));
            text.text = label;
            return button;
        }

        private static void AddOutline(Graphic graphic, Color color, float size)
        {
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(size, -size);
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }

        private static void Stretch(RectTransform rect, Vector2? min = null, Vector2? max = null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min ?? Vector2.zero;
            rect.offsetMax = max ?? Vector2.zero;
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
}
