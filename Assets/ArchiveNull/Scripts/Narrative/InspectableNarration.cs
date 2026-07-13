using UnityEngine;

namespace ArchiveNull.Narrative
{
    /// <summary>
    /// Optional dialogue authored explicitly for inspecting a non-evidence object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InspectableNarration : MonoBehaviour
    {
        [TextArea(2, 5)] [SerializeField] private string spanishText;
        [TextArea(2, 5)] [SerializeField] private string englishText;

        public string GetText()
        {
            string selected = GameLocalization.IsSpanish ? spanishText : englishText;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                return selected;
            }

            return GameLocalization.IsSpanish ? englishText : spanishText;
        }

        public void Configure(string spanish, string english)
        {
            spanishText = spanish;
            englishText = english;
        }
    }
}
