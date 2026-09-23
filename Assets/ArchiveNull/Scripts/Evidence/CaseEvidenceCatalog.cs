using System;
using System.Collections.Generic;
using ArchiveNull.InvestigationBoard;

namespace ArchiveNull.Evidence
{
    // Stable scene IDs are retained so existing photographs and board links still load.
    public static class CaseEvidenceCatalog
    {
        public sealed class Entry
        {
            public readonly string Id, Name, Description;
            public readonly int Number;
            public Entry(int number, string id, string name, string description)
            { Number = number; Id = id; Name = name; Description = description; }
        }

        public static readonly Entry[] Entries =
        {
            new(1, "f1_house_documento", "Documento sucesorio adulterado", "La copia favorece la venta que propone Nicolás. La firma de Elena difiere de la original. Demuestra un conflicto documental, pero no ubica a Nicolás en la casa a la hora de la muerte."),
            new(2, "f1_house_cube", "Nota de Julián", "Revisar firma de mamá y copia de Nicolás. Comprobantes Obra Salas: números repetidos, montos distintos. Llamar al Dr. Funes mañana. Avisar a Sofía si no respondo. Teléfono: 2530."),
            new(3, "f1_house_pastillero", "Pastillero junto al cuerpo", "El frasco fue colocado junto al cuerpo. No hay huellas claras que demuestren una ingesta voluntaria; su presencia por sí sola no explica cómo llegó la medicación a Julián."),
            new(4, "f1_house_guest_cup", "Taza de la visita", "Una segunda taza muestra que Julián recibió a alguien. La huella de una visita anterior no determina quién estuvo presente durante la muerte."),
            new(5, "f1_house_cup", "Taza lavada de Julián", "La taza de té de Julián está recién lavada. Quedan restos de azúcar y polvo en el borde interior. Comparar con el azucarero y la medicación encontrada en la oficina de Salas."),
            new(6, "f1_house_ornamentmedium_01", "Tarro del lavabo", "Recipiente doméstico junto al lavabo. No contiene un rastro que permita vincularlo al crimen."),
            new(7, "f1_house_huella-dactilar", "Huella en la taza de la visita", "Huella parcial de una visita. Es compatible con el relato de Sofía de haber estado antes en la casa, pero no fija la hora del contacto."),
            new(8, "f1_house_phone", "Teléfono de Julián", "El dispositivo conserva mensajes y llamadas. Leer ambas aplicaciones permite contrastar la despedida con los planes de Julián y los horarios de sus contactos."),
            new(9, "phone_messages", "Mensajes de Julián", "Julián escribe mensajes extensos y planea denunciar documentos adulterados al día siguiente. La despedida final, breve y sin explicación, contradice ese estilo y sus planes. Sofía relata que ya se había marchado."),
            new(10, "phone_call_log", "Registro de llamadas", "El registro permite contrastar los mensajes y la salida de los visitantes con la franja de muerte del expediente, 19:35–20:08. No confundir una llamada con presencia física en la casa."),
            new(11, "f1_house_comprobante", "Ticket de Nicolás", "Comprobante de estación de servicio a las 19:47, fuera del barrio. El trayecto requiere más de treinta minutos. Nicolás no puede estar en la casa durante la franja 19:35–20:08; su falsificación sucesoria sigue siendo un hecho separado."),
            new(12, "f1_house_ornamentsmall_01", "Azúcar con polvo de medicación", "El revelado UV permite distinguir un polvo fino mezclado con el azúcar. Su aspecto coincide con la medicación de la oficina de Salas. La taza lavada sitúa la mezcla en una bebida."),
            new(13, "f1_house_huella-zapato", "Huella de barro en la salida", "Rastro junto al acceso: tres líneas diagonales y borde rectangular. Compararlo con las huellas en la oficina del contratista antes de atribuirlo a una persona."),
            new(14, "f1_office_tarro_pastillas", "Medicación de la oficina de Salas", "Restos de medicación triturada del mismo aspecto que el polvo del azucarero. La coincidencia apoya el método al relacionarla con el azúcar y la taza; no basta por sí sola para acusar."),
            new(15, "f1_office_Huellas", "Huellas de las botas de Salas", "Tres líneas diagonales y borde rectangular, coincidentes con la huella de barro de la casa. El rastro debe relacionarse con el acceso, no solo con la existencia de estas botas."),
            new(16, "f1_ofice_llave", "Llave y útiles de acceso de Salas", "Copia de la llave de Casa Herrera conservada con hilo de nylon de nivelación. El aro presenta una marca de tensión. Una anotación de reforma identifica la ventana lateral: permite salir y manipular el giro de la llave interior con el hilo bajo la puerta."),
            new(17, "f1_ofice_factura1", "Facturas duplicadas de Obra Salas", "Dos comprobantes de Obra Salas repiten número pero cambian fecha y monto. La nota de Julián anuncia que llevará estas irregularidades al abogado. La denuncia constituye un motivo distinto del conflicto sucesorio.")
        };

        // Each argument requires particular evidence AND the player's explicit board links.
        public static readonly string[][] ArgumentLinks =
        {
            new[] { "f1_house_huella-zapato|f1_office_Huellas", "f1_office_Huellas|f1_ofice_llave" },
            new[] { "f1_house_cup|f1_house_ornamentsmall_01", "f1_house_ornamentsmall_01|f1_office_tarro_pastillas" },
            new[] { "f1_house_cube|f1_ofice_factura1" },
            new[] { "f1_ofice_llave|f1_house_huella-zapato", "phone_messages|phone_call_log" },
            new[] { "f1_house_comprobante|phone_call_log", "f1_house_documento|f1_house_comprobante" }
        };

        public static Entry Find(string id) => Array.Find(Entries, e => e.Id == id);
        public static string Label(string id) => Find(id) is Entry entry ? $"#{entry.Number:00} {entry.Name}" : id;

        public static bool IsSupportedPair(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || a == b) return false;
            string key = BoardSessionState.GetConnectionKey(a, b);
            foreach (string[] links in ArgumentLinks)
                foreach (string link in links)
                {
                    string[] ids = link.Split('|');
                    if (BoardSessionState.GetConnectionKey(ids[0], ids[1]) == key) return true;
                }
            return false;
        }

        public static bool HasArgument(int index, out string objection)
        {
            foreach (string link in ArgumentLinks[index])
            {
                string[] ids = link.Split('|');
                foreach (string id in ids)
                    if (!EvidenceInventory.Instance.HasEvidence(id))
                    {
                        objection = GameLocalization.Text("Falta investigar: ", "Investigate: ") + Label(id);
                        return false;
                    }
                if (!BoardSessionState.Connections.Contains(BoardSessionState.GetConnectionKey(ids[0], ids[1])))
                {
                    objection = GameLocalization.Text("Contrastá en la pizarra: ", "Compare on the board: ") + Label(ids[0]) + " / " + Label(ids[1]);
                    return false;
                }
            }
            if (index == 3 && !BoardSessionState.UnlockedConclusions.Contains(DoorReconstructionUI.ConclusionId))
            {
                objection = GameLocalization.Text("Completá la reconstrucción de la salida en la libreta (TAB → RECONSTRUIR).", "Complete the exit reconstruction in the notebook (TAB → RECONSTRUCT).");
                return false;
            }
            objection = string.Empty;
            return true;
        }

        public static bool CanVisitContractor =>
            EvidenceInventory.Instance.HasEvidence("f1_house_cube") &&
            EvidenceInventory.Instance.HasEvidence("phone_messages") &&
            EvidenceInventory.Instance.HasEvidence("phone_call_log");

        public static string NextObjective()
        {
            if (!CanVisitContractor)
                return GameLocalization.Text("Casa Herrera: revisá la nota del estante y leé mensajes y llamadas del teléfono. La nota contiene su PIN.", "Herrera house: examine the shelf note and read the phone's messages and calls. The note contains its PIN.");
            for (int i = 0; i < ArgumentLinks.Length; i++)
                if (!HasArgument(i, out string reason)) return reason;
            return GameLocalization.Text("La cadena está respaldada. Presentá el informe final en el terminal de la oficina.", "The evidence chain is supported. Submit the final report at the office terminal.");
        }
    }
}
