using UnityEngine;
using UnityEngine.EventSystems;

namespace ArchiveNull.InvestigationBoard
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DraggableBoardItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Canvas parentCanvas;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 dragOffset;

        public RectTransform RectTransform => rectTransform;
        public event System.Action<DraggableBoardItem> OnDragFinished;
        public event System.Action<DraggableBoardItem> OnDragged;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out dragOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform parent = rectTransform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventCamera, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint - dragOffset;
                OnDragged?.Invoke(this);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            OnDragFinished?.Invoke(this);
        }
    }
}
