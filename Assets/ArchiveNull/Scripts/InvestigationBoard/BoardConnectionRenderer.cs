using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.InvestigationBoard
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class BoardConnectionRenderer : MonoBehaviour
    {
        [SerializeField] private float thickness = 4f;

        private RectTransform rectTransform;
        private Image image;
        private RectTransform from;
        private RectTransform to;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            image.raycastTarget = false;
        }

        public void Bind(RectTransform fromRect, RectTransform toRect)
        {
            from = fromRect;
            to = toRect;
            UpdateVisual();
        }

        public void UpdateVisual()
        {
            if (from == null || to == null || rectTransform == null)
            {
                return;
            }

            Vector3 start = from.position;
            Vector3 end = to.position;
            Vector3 delta = end - start;
            rectTransform.position = start + delta * 0.5f;
            rectTransform.sizeDelta = new Vector2(delta.magnitude, thickness);
            rectTransform.rotation = Quaternion.FromToRotation(Vector3.right, delta);
        }
    }
}
