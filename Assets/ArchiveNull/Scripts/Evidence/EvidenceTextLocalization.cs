namespace ArchiveNull.Evidence
{
    public static class EvidenceTextLocalization
    {
        public static string Name(EvidenceData data)
        {
            if (data == null)
            {
                return GameLocalization.Text("Evidencia", "Evidence");
            }

            string id = Normalize(data.evidenceId);
            if (id.Contains("phone_messages")) return GameLocalization.Text("Mensajes de Julián", "Julián's messages");
            if (id.Contains("phone_call")) return GameLocalization.Text("Registro de llamadas", "Call log");
            if (id.Contains("phone")) return GameLocalization.Text("Teléfono de Julián", "Julián's phone");
            if (id.Contains("hand") || id.Contains("huella")) return GameLocalization.Text("Huella parcial", "Partial print");
            if (id.Contains("ornamentsmall") || id.Contains("azucar")) return GameLocalization.Text("Azucarero", "Sugar bowl");
            if (id.Contains("ornamentmedium") || id.Contains("frasco") || id.Contains("pastilla")) return GameLocalization.Text("Frasco de pastillas", "Pill bottle");
            if (id.Contains("cube") || id.Contains("nota")) return GameLocalization.Text("Nota manuscrita", "Handwritten note");
            return data.evidenceName;
        }

        public static string Description(EvidenceData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string id = Normalize(data.evidenceId);
            if (id.Contains("phone_messages")) return GameLocalization.Text("Conversaciones anteriores que permiten comparar el estilo de escritura de Julián con el mensaje final.", "Earlier conversations that allow Julián's writing style to be compared with the final message.");
            if (id.Contains("phone_call")) return GameLocalization.Text("Historial de contactos y horarios durante las horas previas a la muerte.", "History of contacts and times during the hours before the death.");
            if (id.Contains("phone")) return GameLocalization.Text("Teléfono personal de la víctima. Contiene el supuesto mensaje de despedida enviado a Sofía.", "The victim's personal phone. It contains the alleged farewell message sent to Sofía.");
            if (id.Contains("hand") || id.Contains("huella")) return GameLocalization.Text("Rastro parcial que debe compararse antes de vincularlo con una persona.", "A partial trace that must be compared before linking it to a person.");
            if (id.Contains("ornamentsmall") || id.Contains("azucar")) return GameLocalization.Text("Azucarero con restos de un polvo fino mezclado con el contenido.", "A sugar bowl containing traces of fine powder mixed with its contents.");
            if (id.Contains("ornamentmedium") || id.Contains("frasco") || id.Contains("pastilla")) return GameLocalization.Text("Frasco colocado cerca del cuerpo y sin huellas claras de manipulación.", "A bottle placed near the body with no clear handling fingerprints.");
            if (id.Contains("cube") || id.Contains("nota")) return GameLocalization.Text("Anotación relacionada con documentos, llamadas pendientes y comprobantes de obra.", "A note concerning documents, pending calls, and construction receipts.");
            return data.description;
        }

        public static string Narrative(EvidenceData data)
        {
            return data != null && !string.IsNullOrWhiteSpace(data.narrativeLine)
                ? data.narrativeLine
                : string.Empty;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
