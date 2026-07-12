using ArchiveNull.Evidence;

namespace ArchiveNull.InvestigationBoard
{
    public static class EvidenceConnectionNarration
    {
        public static void Show(string a, string b)
        {
            if (EvidenceGuidanceController.ExistingInstance == null) return;
            string pair = ((a ?? string.Empty) + " " + (b ?? string.Empty)).ToLowerInvariant();
            bool phoneContext = pair.Contains("phone") && (pair.Contains("message") || pair.Contains("call"));
            bool poisoning = (pair.Contains("ornamentsmall") || pair.Contains("azucar")) && (pair.Contains("ornamentmedium") || pair.Contains("pastilla") || pair.Contains("frasco"));
            string line = phoneContext
                ? GameLocalization.Text("Esta conexión encaja: el contenido del teléfono permite contrastar el mensaje final con horarios y conversaciones anteriores.", "This connection fits: the phone's contents allow the final message to be compared with times and earlier conversations.")
                : poisoning
                    ? GameLocalization.Text("Esta relación puede explicar el método: medicación visible junto al cuerpo y polvo mezclado en el azucarero.", "This relationship may explain the method: visible medication beside the body and powder mixed into the sugar bowl.")
                    : GameLocalization.Text("No encuentro una relación directa entre estas evidencias. Conectarlas sin una explicación solo agrega ruido a la hipótesis.", "I cannot find a direct relationship between this evidence. Connecting them without an explanation only adds noise to the hypothesis.");
            EvidenceGuidanceController.ExistingInstance.ShowInspectionSubtitle(line);
        }
    }
}
