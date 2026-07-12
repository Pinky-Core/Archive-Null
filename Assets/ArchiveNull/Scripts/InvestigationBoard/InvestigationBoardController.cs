using System.Collections.Generic;
using ArchiveNull.Evidence;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class InvestigationBoardController : MonoBehaviour
    {
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private EvidenceCardUI cardPrefab;
        [SerializeField] private BoardZoneManager zoneManager;
        [SerializeField] private BoardConnectionManager connectionManager;
        [SerializeField] private Vector2 firstCardPosition = new Vector2(-360f, 180f);
        [SerializeField] private Vector2 cardSpacing = new Vector2(230f, -150f);
        [SerializeField] private int cardsPerRow = 4;

        private readonly Dictionary<string, EvidenceCardUI> cardsById = new Dictionary<string, EvidenceCardUI>();

        private void Awake()
        {
            if (cardContainer == null)
            {
                cardContainer = transform as RectTransform;
            }

            if (zoneManager == null)
            {
                zoneManager = GetComponentInChildren<BoardZoneManager>(true);
            }

            if (connectionManager == null)
            {
                connectionManager = GetComponentInChildren<BoardConnectionManager>(true);
            }
        }

        private void OnEnable()
        {
            EvidenceInventory.Instance.OnEvidenceRegistered += HandleEvidenceRegistered;
            EvidenceInventory.Instance.OnInventoryChanged += RefreshFromInventory;
            RefreshFromInventory();
        }

        private void OnDisable()
        {
            if (EvidenceInventory.ExistingInstance != null)
            {
                EvidenceInventory.ExistingInstance.OnEvidenceRegistered -= HandleEvidenceRegistered;
                EvidenceInventory.ExistingInstance.OnInventoryChanged -= RefreshFromInventory;
            }
        }

        private void RefreshFromInventory()
        {
            IReadOnlyList<EvidenceData> evidence = EvidenceInventory.Instance.GetAllEvidence();
            for (int i = 0; i < evidence.Count; i++)
            {
                SpawnCardIfNeeded(evidence[i], i);
            }

            if (connectionManager != null)
            {
                connectionManager.RefreshConnections();
            }
        }

        private void HandleEvidenceRegistered(EvidenceData data)
        {
            SpawnCardIfNeeded(data, cardsById.Count);
        }

        private void SpawnCardIfNeeded(EvidenceData data, int index)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.evidenceId) || cardsById.ContainsKey(data.evidenceId) || cardPrefab == null)
            {
                return;
            }

            EvidenceCardUI card = Instantiate(cardPrefab, cardContainer);
            card.Bind(data);
            cardsById.Add(data.evidenceId, card);

            RectTransform rect = card.transform as RectTransform;
            if (rect != null)
            {
                if (BoardSessionState.CardPositions.TryGetValue(data.evidenceId, out Vector2 savedPosition))
                {
                    rect.anchoredPosition = savedPosition;
                }
                else
                {
                    int row = index / Mathf.Max(1, cardsPerRow);
                    int column = index % Mathf.Max(1, cardsPerRow);
                    rect.anchoredPosition = firstCardPosition + new Vector2(cardSpacing.x * column, cardSpacing.y * row);
                }
            }

            DraggableBoardItem draggable = card.GetComponent<DraggableBoardItem>();
            if (draggable != null)
            {
                draggable.OnDragged += _ => connectionManager?.UpdateConnectionVisuals();
                draggable.OnDragFinished += item =>
                {
                    if (rect != null)
                    {
                        BoardSessionState.CardPositions[data.evidenceId] = rect.anchoredPosition;
                    }

                    Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, rect.position);
                    zoneManager?.AssignCardToZone(card, screenPosition);
                    connectionManager?.UpdateConnectionVisuals();
                };
            }

            if (connectionManager != null)
            {
                connectionManager.RegisterCard(card);
            }
        }

        public EvidenceCardUI GetCard(string evidenceId)
        {
            return !string.IsNullOrWhiteSpace(evidenceId) && cardsById.TryGetValue(evidenceId, out EvidenceCardUI card) ? card : null;
        }
    }
}
