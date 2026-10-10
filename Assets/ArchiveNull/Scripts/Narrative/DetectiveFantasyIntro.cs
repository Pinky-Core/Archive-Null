using UnityEngine;
namespace ArchiveNull.Narrative
{
    // Kept for serialized references from older scenes. Presentation cards were retired.
    public sealed class DetectiveFantasyIntro : MonoBehaviour
    {
        public static bool IsPlaying => false;
        private void Awake() => Destroy(gameObject);
    }
}