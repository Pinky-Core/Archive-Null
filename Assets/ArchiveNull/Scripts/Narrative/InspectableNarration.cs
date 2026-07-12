using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.Narrative
{
    [DisallowMultipleComponent]
    public sealed class InspectableNarration : MonoBehaviour
    {
        [TextArea(2, 5)] [SerializeField] private string spanishText;
        [TextArea(2, 5)] [SerializeField] private string englishText;

        public string GetText()
        {
            string selected = GameLocalization.IsSpanish ? spanishText : englishText;
            if (!string.IsNullOrWhiteSpace(selected)) return selected;
            return GameLocalization.IsSpanish ? englishText : spanishText;
        }

        public void Configure(string spanish, string english)
        {
            spanishText = spanish;
            englishText = english;
        }
    }

    public static class InspectableNarrationInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.name.Contains("House", System.StringComparison.OrdinalIgnoreCase)) return;
            GameObject[] objects = GameObject.FindGameObjectsWithTag("Inspectable");
            for (int i = 0; i < objects.Length; i++) Configure(objects[i]);
        }

        private static void Configure(GameObject target)
        {
            if (target == null) return;
            string name = target.name.ToLowerInvariant();
            InspectableNarration narration = target.GetComponent<InspectableNarration>() ?? target.AddComponent<InspectableNarration>();

            if (name.Contains("apple"))
            {
                narration.Configure("Una manzana olvidada. No parece relacionada con la muerte, pero ayuda a establecer que la casa seguía habitada.", "A forgotten apple. It does not appear related to the death, but it helps establish that the house was still inhabited.");
            }
            else if (name.Contains("book_a"))
            {
                narration.Configure("Un libro de arquitectura con varias páginas marcadas. Julián seguía trabajando en proyectos y no parece haber abandonado sus asuntos.", "An architecture book with several marked pages. Julián was still working on projects and does not appear to have abandoned his affairs.");
            }
            else if (name.Contains("book_b"))
            {
                narration.Configure("Un libro sobre propiedad y reformas. Hay anotaciones vinculadas con la casa familiar y comprobantes de obra.", "A book about property and renovations. It contains notes connected to the family house and construction receipts.");
            }
            else if (name.Contains("hourglass"))
            {
                narration.Configure("Un reloj de arena decorativo. No es evidencia directa, aunque su posición indica que nadie ordenó esta zona después del hecho.", "A decorative hourglass. It is not direct evidence, although its position suggests no one tidied this area after the incident.");
            }
            else if (name.Contains("ornamentmedium"))
            {
                narration.Configure("Esto parece ser el frasco encontrado cerca del cuerpo. Está demasiado visible y no conserva huellas claras.", "This appears to be the bottle found near the body. It is too visible and has no clear fingerprints.");
            }
            else if (name.Contains("ornamentsmall"))
            {
                narration.Configure("Parece un azucarero, pero el contenido tiene un polvo más fino mezclado. Debería revisarlo con atención.", "It appears to be a sugar bowl, but a finer powder is mixed into its contents. I should examine it carefully.");
            }
            else if (name == "cube" || name.Contains("nota"))
            {
                narration.Configure("Una nota escrita con apuro: menciona la firma de Elena, una llamada al abogado y comprobantes de la obra de Salas.", "A hurried note: it mentions Elena's signature, a call to the lawyer, and receipts from Salas's construction work.");
            }
            else
            {
                narration.Configure("Este objeto forma parte de la escena. Su posición puede ser más importante que el objeto en sí.", "This object is part of the scene. Its position may be more important than the object itself.");
            }
        }
    }
}
