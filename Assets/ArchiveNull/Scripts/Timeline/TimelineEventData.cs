using UnityEngine;

namespace ArchiveNull.Timeline
{
    [CreateAssetMenu(menuName = "Archive Null/Timeline/Event Data", fileName = "TimelineEvent")]
    public sealed class TimelineEventData : ScriptableObject
    {
        public string eventId;
        public string title;
        [TextArea(3, 8)] public string description;
        public int correctOrderIndex;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = name;
            }
        }
    }
}
