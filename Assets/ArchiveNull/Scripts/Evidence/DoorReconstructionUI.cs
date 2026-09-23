using System.Collections.Generic;
using ArchiveNull.InvestigationBoard;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    public sealed class DoorReconstructionUI : MonoBehaviour
    {
        public const string ConclusionId = "case01.door_reconstructed";
        private readonly List<int> sequence = new();
        private readonly List<Button> choices = new();
        private TMP_Text status;
        private TMP_Text orderedSteps;
        private static readonly string[] Steps =
        {
            "Salir por la ventana lateral",
            "Dejar la ventana sin trabar por completo",
            "Pasar el hilo por debajo de la puerta",
            "Tirar del hilo para girar la llave interior",
            "Retirar el hilo; queda un fragmento atrapado"
        };

        public void Build(RectTransform parent)
        {
            Label(parent, "RECONSTRUCCIÓN // LA LLAVE POR DENTRO", 30f, new Vector2(70, 610), new Vector2(1100, 50));
            status = Label(parent, "", 21f, new Vector2(70, 485), new Vector2(1100, 110));
            orderedSteps = Label(parent, "", 22f, new Vector2(730, 170), new Vector2(450, 300));
            int[] shuffled = { 3, 0, 4, 2, 1 };
            for (int i = 0; i < shuffled.Length; i++)
            {
                int step = shuffled[i];
                choices.Add(MakeButton(parent, Steps[step], new Vector2(70, 408 - i * 64), new Vector2(620, 52), () => Choose(step)));
            }
            MakeButton(parent, "REINICIAR SECUENCIA", new Vector2(70, 76), new Vector2(300, 48), ResetSequence);
            MakeButton(parent, "VALIDAR RECONSTRUCCIÓN", new Vector2(730, 76), new Vector2(450, 48), ValidateSequence);
            Refresh();
        }

        private void OnEnable() { if (status != null) Refresh(); }

        private void Choose(int step)
        {
            if (sequence.Contains(step)) return;
            sequence.Add(step);
            Refresh();
        }

        private void ResetSequence() { sequence.Clear(); Refresh(); }

        private void Refresh()
        {
            bool solved = BoardSessionState.UnlockedConclusions.Contains(ConclusionId);
            status.text = solved ? "RECONSTRUCCIÓN CONSERVADA. La ventana y el hilo explican el cierre aparente desde adentro. Podés revisar la secuencia o continuar con la acusación."
                : "Ordená las acciones del cierre desde el exterior. Para validar necesitás la llave y útiles de Salas, la huella de la casa y las huellas de su oficina. Las conexiones en la pizarra siguen siendo necesarias.";
            var lines = new List<string>();
            for (int i = 0; i < sequence.Count; i++) lines.Add($"{i + 1}. {Steps[sequence[i]]}");
            orderedSteps.text = lines.Count == 0 ? "SECUENCIA PROPUESTA\n\nSeleccioná los pasos de la izquierda." : string.Join("\n\n", lines);
        }

        private void ValidateSequence()
        {
            foreach (string id in new[] { "f1_ofice_llave", "f1_house_huella-zapato", "f1_office_Huellas" })
                if (!EvidenceInventory.Instance.HasEvidence(id)) { status.text = "Falta documentar: " + CaseEvidenceCatalog.Label(id); return; }
            if (sequence.Count != Steps.Length) { status.text = "La reconstrucción necesita los cinco pasos."; return; }
            for (int i = 0; i < sequence.Count; i++)
                if (sequence[i] != i)
                {
                    status.text = "La secuencia no reproduce el cierre: comprobá cuándo se abandona la casa y cuándo se pasa, tensa y retira el hilo. Podés reiniciar e intentarlo otra vez.";
                    return;
                }
            BoardSessionState.UnlockedConclusions.Add(ConclusionId);
            GameSaveSystem.SaveNow();
            Refresh();
            status.text = "RECONSTRUCCIÓN VALIDADA\nSalida por la ventana → hilo bajo la puerta → giro de la llave → retirada del hilo. El encierro era una puesta en escena. Esta secuencia queda incorporada al informe.";
            EvidenceGuidanceController.ExistingInstance?.ShowInspectionSubtitle("La llave estaba por dentro, pero quien la giró ya estaba afuera. La escena podía cerrarse sin que nadie permaneciera en la casa.");
        }

        private static TMP_Text Label(RectTransform parent, string value, float size, Vector2 position, Vector2 dimensions)
        {
            var obj = new GameObject("ReconstructionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TMP_Text>();
            text.text = value; text.fontSize = size; text.color = new Color(0.82f, 0.9f, 0.85f);
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            return text;
        }

        private static Button MakeButton(RectTransform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject("ReconstructionChoice", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<Image>(); image.color = new Color(0.12f, 0.2f, 0.18f);
            var button = obj.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Label(rect, label, 20f, new Vector2(14, 4), size - new Vector2(28, 8)).alignment = TextAlignmentOptions.MidlineLeft;
            return button;
        }
    }
}
