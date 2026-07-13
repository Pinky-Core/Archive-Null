using ArchiveNull.Evidence;

namespace ArchiveNull.InvestigationBoard
{
    public enum ConnectionQuality
    {
        Unsupported,
        Plausible,
        Supported
    }

    public static class EvidenceConnectionNarration
    {
        public static void Show(string evidenceA, string evidenceB)
        {
            if (EvidenceGuidanceController.ExistingInstance == null)
            {
                return;
            }

            ConnectionQuality quality = Evaluate(evidenceA, evidenceB);
            string line = quality switch
            {
                ConnectionQuality.Supported => GameLocalization.Text(
                    "La conexión está respaldada. Estas evidencias explican una parte concreta del caso.",
                    "This connection is supported. This evidence explains a concrete part of the case."),
                ConnectionQuality.Plausible => GameLocalization.Text(
                    "La relación es posible, pero todavía falta otra evidencia que demuestre causa, horario o acceso.",
                    "The relationship is plausible, but more evidence is needed to establish cause, time, or access."),
                _ => GameLocalization.Text(
                    "No encuentro una relación demostrable entre estas evidencias. Esta conexión agrega ruido a la hipótesis.",
                    "I cannot establish a supported relationship between this evidence. This connection adds noise to the hypothesis.")
            };
            EvidenceGuidanceController.ExistingInstance.ShowInspectionSubtitle(line);
        }

        public static ConnectionQuality Evaluate(string evidenceA, string evidenceB)
        {
            string a = Normalize(evidenceA);
            string b = Normalize(evidenceB);
            string pair = a + " " + b;

            if (Matches(a, b, PhoneMessages, PhoneCalls) ||
                Matches(a, b, Sugar, Cup) ||
                Matches(a, b, Sugar, Medication) ||
                Matches(a, b, Cup, Medication) ||
                Matches(a, b, Thread, Door) ||
                Matches(a, b, Plan, Access) ||
                Matches(a, b, Invoice, Contract) ||
                Matches(a, b, Footwear, GroundMark))
            {
                return ConnectionQuality.Supported;
            }

            if ((ContainsAny(pair, PhoneMessages) && ContainsAny(pair, "mensaje", "despedida")) ||
                (ContainsAny(pair, "victor", "salas") &&
                 (ContainsAny(pair, Invoice) || ContainsAny(pair, Contract) || ContainsAny(pair, Thread) || ContainsAny(pair, Plan))) ||
                (ContainsAny(pair, "sofia", "nicolas") && ContainsAny(pair, "horario", "ticket", "coartada")) ||
                (ContainsAny(pair, Sugar) && (ContainsAny(pair, Cup) || ContainsAny(pair, Medication))))
            {
                return ConnectionQuality.Plausible;
            }

            return ConnectionQuality.Unsupported;
        }

        public static int CountSupportedConnections()
        {
            int count = 0;
            foreach (string key in BoardSessionState.Connections)
            {
                string[] ids = key.Split('|');
                if (ids.Length == 2 && Evaluate(ids[0], ids[1]) == ConnectionQuality.Supported)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Matches(string a, string b, string[] groupA, string[] groupB)
        {
            return (ContainsAny(a, groupA) && ContainsAny(b, groupB)) ||
                   (ContainsAny(b, groupA) && ContainsAny(a, groupB));
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (value.Contains(terms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static readonly string[] PhoneMessages = { "phone_messages", "mensaje", "chat" };
        private static readonly string[] PhoneCalls = { "phone_call", "llamada", "registro" };
        private static readonly string[] Sugar = { "ornamentsmall", "azucar", "polvo" };
        private static readonly string[] Cup = { "taza", "copa", "vaso" };
        private static readonly string[] Medication = { "ornamentmedium", "pastilla", "frasco", "medicacion" };
        private static readonly string[] Thread = { "hilo", "thread", "cordel" };
        private static readonly string[] Door = { "puerta", "door", "picaporte" };
        private static readonly string[] Plan = { "plano", "plan", "reforma" };
        private static readonly string[] Access = { "ventana", "window", "acceso", "entrada" };
        private static readonly string[] Invoice = { "factura", "invoice", "comprobante" };
        private static readonly string[] Contract = { "contrato", "contract", "obra", "denuncia" };
        private static readonly string[] Footwear = { "calzado", "shoe", "zapat" };
        private static readonly string[] GroundMark = { "barro", "mud", "marca", "huella_suelo" };
    }
}
