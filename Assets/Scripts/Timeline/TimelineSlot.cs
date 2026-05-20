using UnityEngine;

namespace ArchiveNull.Timeline
{
    [DisallowMultipleComponent]
    public sealed class TimelineSlot : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private RectTransform slotRect;

        public int SlotIndex => slotIndex;
        public string EventId { get; private set; }

        private void Awake()
        {
            if (slotRect == null)
            {
                slotRect = transform as RectTransform;
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera)
        {
            return slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera);
        }

        public void Assign(string eventId)
        {
            EventId = eventId;
        }
    }
}
