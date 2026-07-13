using System.Collections.Generic;
using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ArchiveNull.Accusation
{
    /// <summary>
    /// Runtime CRT-styled final report. It scores each part of the accusation independently.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccusationPanel : MonoBehaviour
    {
        public const string CaseCompletedPref = "archive.case01.completed";
        private const int RequiredEvidenceCount = 8;
        private const int RequiredSupportedConnections = 3;

        private static readonly string[][] SpanishOptions =
        {
            new[] { "SIN SELECCION", "VICTOR SALAS", "SOFIA HERRERA", "NICOLAS HERRERA" },
            new[] { "SIN SELECCION", "BEBIDA ENVENENADA", "SOBREDOSIS VOLUNTARIA", "GOLPE" },
            new[] { "SIN SELECCION", "EVITAR DENUNCIA POR LA OBRA", "HERENCIA", "CONFLICTO SENTIMENTAL" },
            new[] { "SIN SELECCION", "PUERTA CERRADA DESDE AFUERA", "MENSAJE ESPONTANEO", "SIN MANIPULACION" },
            new[] { "SIN SELECCION", "SOFIA Y NICOLAS", "VICTOR Y SOFIA", "NADIE" }
        };

        private static readonly string[][] EnglishOptions =
        {
            new[] { "NO SELECTION", "VICTOR SALAS", "SOFIA HERRERA", "NICOLAS HERRERA" },
            new[] { "NO SELECTION", "POISONED DRINK", "VOLUNTARY OVERDOSE", "BLUNT FORCE" },
            new[] { "NO SELECTION", "AVOID CONSTRUCTION FRAUD REPORT", "INHERITANCE", "ROMANTIC CONFLICT" },
            new[] { "NO SELECTION", "DOOR LOCKED FROM OUTSIDE", "SPONTANEOUS MESSAGE", "NO MANIPULATION" },
            new[] { "NO SELECTION", "SOFIA AND NICOLAS", "VICTOR AND SOFIA", "NO ONE" }
        };

        private static readonly string[] SpanishLabels =
        {
            "RESPONSABLE", "METODO", "MOTIVO", "MANIPULACION", "SOSPECHOSOS DESCARTADOS"
        };

        private static readonly string[] EnglishLabels =
        {
            "CULPRIT", "METHOD", "MOTIVE", "MANIPULATION", "CLEARED SUSPECTS"
        };

        private readonly int[] selections = new int[5];
        private readonly List<TMP_Text> rowTexts = new();
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private TMP_Text titleText;
        private TMP_Text statusText;
        private int selectedRow;
        private bool completed;

        public static AccusationPanel ExistingInstance { get; private set; }
        public static bool IsAnyOpen => ExistingInstance != null && ExistingInstance.canvasGroup != null && ExistingInstance.canvasGroup.gameObject.activeSelf;

        public static bool CanPresentReport(out string reason)
        {
            int evidenceCount = EvidenceInventory.Instance.GetAllEvidence().Count;
            int supportedConnections = EvidenceConnectionNarration.CountSupportedConnections();
            if (evidenceCount < RequiredEvidenceCount)
            {
                reason = GameLocalization.Text(
                    $"Faltan evidencias: {evidenceCount:00}/{RequiredEvidenceCount:00}.",
                    $"Missing evidence: {evidenceCount:00}/{RequiredEvidenceCount:00}.");
                return false;
            }

            if (supportedConnections < RequiredSupportedConnections)
            {
                reason = GameLocalization.Text(
                    $"Faltan conexiones respaldadas: {supportedConnections:00}/{RequiredSupportedConnections:00}.",
                    $"Missing supported connections: {supportedConnections:00}/{RequiredSupportedConnections:00}.");
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static void OpenRuntime()
        {
            if (!CanPresentReport(out string reason))
            {
                EvidenceGuidanceController.ExistingInstance?.ShowInspectionSubtitle(reason);
                return;
            }

            if (ExistingInstance == null)
            {
                ExistingInstance = new GameObject("FinalAccusationReport").AddComponent<AccusationPanel>();
            }

            ExistingInstance.Open();
        }

        private void Awake()
        {
            if (ExistingInstance != null && ExistingInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            ExistingInstance = this;
            BuildRuntimeUi();
            canvasGroup.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ExistingInstance == this)
            {
                ExistingInstance = null;
            }
        }

        private void Update()
        {
            if (!IsAnyOpen)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (completed)
            {
                return;
            }

            HandleKeyboard();
            HandlePointer();
        }

        private void Open()
        {
            completed = false;
            selectedRow = 0;
            titleText.text = GameLocalization.Text("INFORME FINAL // LA LLAVE POR DENTRO", "FINAL REPORT // THE KEY WITHIN");
            for (int i = 0; i < rowTexts.Count; i++)
            {
                rowTexts[i].gameObject.SetActive(true);
            }
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshRows();
            statusText.text = GameLocalization.Text(
                "Complete cada campo. Cada respuesta correcta vale 20 puntos.",
                "Complete every field. Each correct answer is worth 20 points.");
        }

        private void Close()
        {
            canvasGroup.gameObject.SetActive(false);
        }

        private void HandleKeyboard()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
            {
                selectedRow = (selectedRow + rowTexts.Count - 1) % rowTexts.Count;
                RefreshRows();
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
            {
                selectedRow = (selectedRow + 1) % rowTexts.Count;
                RefreshRows();
            }

            if (selectedRow < selections.Length &&
                (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame))
            {
                CycleSelection(selectedRow, -1);
            }
            else if (selectedRow < selections.Length &&
                     (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
            {
                CycleSelection(selectedRow, 1);
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                if (selectedRow == selections.Length)
                {
                    SubmitReport();
                }
                else
                {
                    CycleSelection(selectedRow, 1);
                }
            }
        }

        private void HandlePointer()
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            for (int i = 0; i < rowTexts.Count; i++)
            {
                TMP_Text row = rowTexts[i];
                if (!RectTransformUtility.RectangleContainsScreenPoint(row.rectTransform, pointer, null))
                {
                    continue;
                }

                selectedRow = i;
                RefreshRows();
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (i == selections.Length)
                    {
                        SubmitReport();
                    }
                    else
                    {
                        CycleSelection(i, 1);
                    }
                }

                return;
            }
        }

        private void CycleSelection(int row, int direction)
        {
            string[][] options = GameLocalization.IsSpanish ? SpanishOptions : EnglishOptions;
            selections[row] = (selections[row] + direction + options[row].Length) % options[row].Length;
            RefreshRows();
        }

        private void SubmitReport()
        {
            int correct = 0;
            List<string> mistakes = new();
            string[] labels = GameLocalization.IsSpanish ? SpanishLabels : EnglishLabels;
            for (int i = 0; i < selections.Length; i++)
            {
                if (selections[i] == 1)
                {
                    correct++;
                }
                else
                {
                    mistakes.Add(labels[i]);
                }
            }

            int score = correct * 20;
            if (score == 100)
            {
                completed = true;
                PlayerPrefs.SetInt(CaseCompletedPref, 1);
                PlayerPrefs.Save();
                titleText.text = GameLocalization.Text("CASO CERRADO // 100%", "CASE CLOSED // 100%");
                statusText.text = GameLocalization.Text(
                    "La acusación explica responsable, método, motivo y manipulación. Informe aceptado. ESC para cerrar.",
                    "The accusation explains culprit, method, motive, and manipulation. Report accepted. ESC to close.");
                for (int i = 0; i < rowTexts.Count; i++)
                {
                    rowTexts[i].gameObject.SetActive(false);
                }
                return;
            }

            string failedAreas = string.Join(", ", mistakes);
            string message = GameLocalization.Text(
                $"PRECISION DEL INFORME: {score}%. Revise conexiones relacionadas con: {failedAreas}.",
                $"REPORT ACCURACY: {score}%. Review connections related to: {failedAreas}.");
            EvidenceGuidanceController.ExistingInstance?.ShowInspectionSubtitle(message);
            Close();
        }

        private void RefreshRows()
        {
            string[][] options = GameLocalization.IsSpanish ? SpanishOptions : EnglishOptions;
            string[] labels = GameLocalization.IsSpanish ? SpanishLabels : EnglishLabels;
            for (int i = 0; i < selections.Length; i++)
            {
                rowTexts[i].text = $"{(i == selectedRow ? ">" : " ")} {labels[i]} .... < {options[i][selections[i]]} >";
                rowTexts[i].color = i == selectedRow ? new Color(0.78f, 1f, 0.91f) : new Color(0.58f, 0.78f, 0.7f);
            }

            int submitIndex = selections.Length;
            rowTexts[submitIndex].text = (submitIndex == selectedRow ? "> " : "  ") +
                                         GameLocalization.Text("ENVIAR INFORME", "SUBMIT REPORT");
            rowTexts[submitIndex].color = submitIndex == selectedRow ? new Color(1f, 0.78f, 0.42f) : new Color(0.72f, 0.58f, 0.35f);
        }

        private void BuildRuntimeUi()
        {
            GameObject canvasObject = new("FinalReportCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            Image backdrop = CreateImage(canvasObject.transform, "Backdrop", new Color(0.008f, 0.018f, 0.016f, 0.985f));
            SetRect(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            titleText = CreateText(canvasObject.transform, "Title", 38f, TextAlignmentOptions.Left);
            titleText.text = GameLocalization.Text("INFORME FINAL // LA LLAVE POR DENTRO", "FINAL REPORT // THE KEY WITHIN");
            SetRect(titleText.rectTransform, new Vector2(0.16f, 0.82f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero);

            for (int i = 0; i < 6; i++)
            {
                TMP_Text row = CreateText(canvasObject.transform, "ReportRow_" + i, 25f, TextAlignmentOptions.Left);
                float top = 0.75f - i * 0.085f;
                SetRect(row.rectTransform, new Vector2(0.18f, top - 0.055f), new Vector2(0.82f, top), Vector2.zero, Vector2.zero);
                rowTexts.Add(row);
            }

            statusText = CreateText(canvasObject.transform, "Status", 21f, TextAlignmentOptions.TopLeft);
            statusText.color = new Color(0.72f, 0.72f, 0.58f);
            SetRect(statusText.rectTransform, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.22f), Vector2.zero, Vector2.zero);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            GameObject child = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.72f, 0.9f, 0.82f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
