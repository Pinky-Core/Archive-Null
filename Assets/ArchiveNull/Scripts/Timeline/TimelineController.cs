using System.Collections.Generic;
using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Timeline
{
    [DisallowMultipleComponent]
    public sealed class TimelineController : MonoBehaviour
    {
        [SerializeField] private TimelineEventData[] events;
        [SerializeField] private TimelineEventCardUI eventCardPrefab;
        [SerializeField] private RectTransform eventCardContainer;
        [SerializeField] private TimelineSlot[] slots;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Button validateButton;
        [SerializeField] private ConclusionData finalConclusion;
        [SerializeField] private SimpleMessageUI messageUI;

        private readonly Dictionary<string, TimelineEventCardUI> cardsById = new Dictionary<string, TimelineEventCardUI>();

        private void Awake()
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (eventCardContainer == null)
            {
                eventCardContainer = transform as RectTransform;
            }

            if (slots == null || slots.Length == 0)
            {
                slots = GetComponentsInChildren<TimelineSlot>(true);
            }

            if (validateButton != null)
            {
                validateButton.onClick.AddListener(ValidateTimeline);
            }
        }

        private void Start()
        {
            SpawnEvents();
        }

        private void SpawnEvents()
        {
            if (events == null || eventCardPrefab == null)
            {
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                TimelineEventData data = events[i];
                if (data == null || cardsById.ContainsKey(data.eventId))
                {
                    continue;
                }

                TimelineEventCardUI card = Instantiate(eventCardPrefab, eventCardContainer);
                card.Bind(data);
                cardsById.Add(data.eventId, card);

                RectTransform rect = card.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(-420f + i * 170f, -260f);
                }

                DraggableBoardItem draggable = card.GetComponent<DraggableBoardItem>();
                if (draggable != null)
                {
                    draggable.OnDragFinished += item => AssignToSlot(card, RectTransformUtility.WorldToScreenPoint(null, rect.position));
                }
            }
        }

        private void AssignToSlot(TimelineEventCardUI card, Vector2 screenPosition)
        {
            if (card == null || card.Data == null)
            {
                return;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].ContainsScreenPoint(screenPosition, eventCamera))
                {
                    slots[i].Assign(card.Data.eventId);
                    BoardSessionState.TimelineSlots[card.Data.eventId] = slots[i].SlotIndex;
                    return;
                }
            }
        }

        public void ValidateTimeline()
        {
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Length; i++)
            {
                TimelineEventData data = events[i];
                if (data == null)
                {
                    continue;
                }

                if (!BoardSessionState.TimelineSlots.TryGetValue(data.eventId, out int slotIndex) || slotIndex != data.correctOrderIndex)
                {
                    ShowMessage("Evento fuera de lugar: " + data.title);
                    return;
                }
            }

            if (finalConclusion != null)
            {
                BoardSessionState.UnlockedConclusions.Add(finalConclusion.conclusionId);
            }

            ShowMessage("Linea temporal validada.");
        }

        private void ShowMessage(string message)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(message);
            }
            else
            {
                Debug.Log("[Timeline] " + message);
            }
        }
    }
}
