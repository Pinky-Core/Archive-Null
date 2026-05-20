using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.InvestigationBoard
{
    [DisallowMultipleComponent]
    public sealed class BoardConnectionManager : MonoBehaviour
    {
        [SerializeField] private RectTransform connectionContainer;
        [SerializeField] private BoardConnectionRenderer connectionPrefab;

        private readonly Dictionary<string, EvidenceCardUI> cardsById = new Dictionary<string, EvidenceCardUI>();
        private readonly Dictionary<string, BoardConnectionRenderer> renderersByKey = new Dictionary<string, BoardConnectionRenderer>();
        private EvidenceCardUI pendingCard;

        public event Action<string, string> OnConnectionCreated;
        public event Action OnConnectionsChanged;

        private void Awake()
        {
            if (connectionContainer == null)
            {
                connectionContainer = transform as RectTransform;
            }
        }

        public void RegisterCard(EvidenceCardUI card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.EvidenceId))
            {
                return;
            }

            cardsById[card.EvidenceId] = card;
            card.OnSelected -= HandleCardSelected;
            card.OnSelected += HandleCardSelected;
            RefreshConnections();
        }

        private void HandleCardSelected(EvidenceCardUI card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.EvidenceId))
            {
                return;
            }

            if (pendingCard == null)
            {
                pendingCard = card;
                return;
            }

            if (pendingCard == card)
            {
                pendingCard = null;
                return;
            }

            TryCreateConnection(pendingCard, card);
            pendingCard = null;
        }

        public bool TryCreateConnection(EvidenceCardUI a, EvidenceCardUI b)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }

            return TryCreateConnection(a.EvidenceId, b.EvidenceId);
        }

        public bool TryCreateConnection(string evidenceA, string evidenceB)
        {
            if (string.IsNullOrWhiteSpace(evidenceA) || string.IsNullOrWhiteSpace(evidenceB) || evidenceA == evidenceB)
            {
                return false;
            }

            string key = BoardSessionState.GetConnectionKey(evidenceA, evidenceB);
            if (!BoardSessionState.Connections.Add(key))
            {
                return false;
            }

            CreateRendererForConnection(key, evidenceA, evidenceB);
            OnConnectionCreated?.Invoke(evidenceA, evidenceB);
            OnConnectionsChanged?.Invoke();
            return true;
        }

        public bool HasConnection(string evidenceA, string evidenceB)
        {
            return BoardSessionState.Connections.Contains(BoardSessionState.GetConnectionKey(evidenceA, evidenceB));
        }

        public void RefreshConnections()
        {
            foreach (string key in BoardSessionState.Connections)
            {
                if (renderersByKey.ContainsKey(key))
                {
                    continue;
                }

                string[] parts = key.Split('|');
                if (parts.Length == 2)
                {
                    CreateRendererForConnection(key, parts[0], parts[1]);
                }
            }

            UpdateConnectionVisuals();
        }

        public void UpdateConnectionVisuals()
        {
            foreach (BoardConnectionRenderer renderer in renderersByKey.Values)
            {
                if (renderer != null)
                {
                    renderer.UpdateVisual();
                }
            }
        }

        private void CreateRendererForConnection(string key, string evidenceA, string evidenceB)
        {
            if (connectionPrefab == null || !cardsById.TryGetValue(evidenceA, out EvidenceCardUI cardA) || !cardsById.TryGetValue(evidenceB, out EvidenceCardUI cardB))
            {
                return;
            }

            BoardConnectionRenderer renderer = Instantiate(connectionPrefab, connectionContainer);
            renderer.transform.SetAsFirstSibling();
            renderer.Bind(cardA.transform as RectTransform, cardB.transform as RectTransform);
            renderersByKey[key] = renderer;
        }
    }
}
