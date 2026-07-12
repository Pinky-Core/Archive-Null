using TMPro;
using UnityEngine;

namespace ArchiveNull.Timeline
{
    [DisallowMultipleComponent]
    public sealed class TimelineEventCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;

        public TimelineEventData Data { get; private set; }

        public void Bind(TimelineEventData data)
        {
            Data = data;
            if (titleText != null)
            {
                titleText.text = data != null ? data.title : "Event";
            }

            if (descriptionText != null)
            {
                descriptionText.text = data != null ? data.description : string.Empty;
            }
        }
    }
}
