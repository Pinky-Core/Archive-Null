using System;

namespace ArchiveNull.Evidence
{
    public static class EvidenceTextLocalization
    {
        public static string Name(EvidenceData data)
        {
            if (data == null) return GameLocalization.Text("Evidencia", "Evidence");
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
            if (data == null) return string.Empty;
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
            if (data == null) return string.Empty;
            string id = Normalize(data.evidenceId);
            if (id.Contains("phone_messages")) return GameLocalization.Text("El mensaje final no coincide con la forma habitual de escribir de Julián. Alguien pudo redactarlo desde su teléfono.", "The final message does not match Julián's usual writing style. Someone may have written it from his phone.");
            if (id.Contains("phone_call")) return GameLocalization.Text("Estos horarios pueden ubicar contactos dentro de la secuencia del crimen. Por sí solos todavía no demuestran culpabilidad.", "These times may place contacts within the crime sequence. On their own, they still do not prove guilt.");
            if (id.Contains("phone")) return GameLocalization.Text("El teléfono de Julián puede contener una explicación real o una explicación fabricada para la escena.", "Julián's phone may contain a real explanation, or one fabricated for the scene.");
            if (id.Contains("hand") || id.Contains("huella")) return GameLocalization.Text("Es una huella incompleta. Necesito compararla; una forma parcial no alcanza para acusar.", "It is an incomplete print. I need to compare it; a partial shape is not enough to accuse anyone.");
            if (id.Contains("ornamentsmall") || id.Contains("azucar")) return GameLocalization.Text("El contenido no parece azúcar pura. Si contiene medicación triturada, la intoxicación ocurrió mediante una bebida.", "The contents do not look like pure sugar. If crushed medication is present, the poisoning occurred through a drink.");
            if (id.Contains("ornamentmedium") || id.Contains("frasco") || id.Contains("pastilla")) return GameLocalization.Text("El frasco está demasiado visible y no conserva huellas claras. Parece colocado para imponer la idea de suicidio.", "The bottle is too visible and has no clear fingerprints. It appears placed to impose the idea of suicide.");
            if (id.Contains("cube") || id.Contains("nota")) return GameLocalization.Text("Julián estaba siguiendo dos conflictos distintos: la herencia familiar y los comprobantes de una obra.", "Julián was pursuing two separate conflicts: the family inheritance and construction receipts.");
            if (!string.IsNullOrWhiteSpace(data.narrativeLine)) return data.narrativeLine;
            string description = Description(data);
            return !string.IsNullOrWhiteSpace(description)
                ? description
                : GameLocalization.Text("Esto puede ser importante: ", "This may be important: ") + Name(data) + ".";
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
