using System;
using System.Collections.Generic;
using ArchiveNull.InvestigationBoard;
using UnityEngine;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class ConclusionManager : MonoBehaviour
    {
        [SerializeField] private ConclusionData[] conclusions;
        [SerializeField] private BoardConnectionManager connectionManager;
        [SerializeField] private WorldBoardConnectionManager worldConnectionManager;
        [SerializeField] private SimpleMessageUI messageUI;

        private readonly HashSet<string> unlocked = new HashSet<string>();

        public event Action<ConclusionData> OnConclusionUnlocked;

        private void Awake()
        {
            if (connectionManager == null)
            {
                connectionManager = FindAnyObjectByType<BoardConnectionManager>();
            }

            if (worldConnectionManager == null)
            {
                worldConnectionManager = FindAnyObjectByType<WorldBoardConnectionManager>();
            }
        }

        private void OnEnable()
        {
            EvidenceInventory.Instance.OnInventoryChanged += EvaluateConclusions;
            if (connectionManager != null)
            {
                connectionManager.OnConnectionsChanged += EvaluateConclusions;
            }

            if (worldConnectionManager != null)
            {
                worldConnectionManager.OnConnectionsChanged += EvaluateConclusions;
            }
        }

        private void OnDisable()
        {
            if (EvidenceInventory.ExistingInstance != null)
            {
                EvidenceInventory.ExistingInstance.OnInventoryChanged -= EvaluateConclusions;
            }

            if (connectionManager != null)
            {
                connectionManager.OnConnectionsChanged -= EvaluateConclusions;
            }

            if (worldConnectionManager != null)
            {
                worldConnectionManager.OnConnectionsChanged -= EvaluateConclusions;
            }
        }

        public bool HasConclusion(string conclusionId)
        {
            return unlocked.Contains(conclusionId) || BoardSessionState.UnlockedConclusions.Contains(conclusionId);
        }

        public void EvaluateConclusions()
        {
            if (conclusions == null)
            {
                return;
            }

            for (int i = 0; i < conclusions.Length; i++)
            {
                ConclusionData conclusion = conclusions[i];
                if (conclusion == null || string.IsNullOrWhiteSpace(conclusion.conclusionId) || HasConclusion(conclusion.conclusionId))
                {
                    continue;
                }

                if (MeetsRequirements(conclusion))
                {
                    unlocked.Add(conclusion.conclusionId);
                    BoardSessionState.UnlockedConclusions.Add(conclusion.conclusionId);
                    OnConclusionUnlocked?.Invoke(conclusion);
                    if (messageUI != null)
                    {
                        messageUI.ShowMessage("Conclusion desbloqueada: " + conclusion.conclusionName);
                    }
                }
            }
        }

        private bool MeetsRequirements(ConclusionData conclusion)
        {
            if (conclusion.requiredEvidenceIds == null || conclusion.requiredEvidenceIds.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < conclusion.requiredEvidenceIds.Length; i++)
            {
                if (!EvidenceInventory.Instance.HasEvidence(conclusion.requiredEvidenceIds[i]))
                {
                    return false;
                }
            }

            if (connectionManager == null && worldConnectionManager == null)
            {
                return true;
            }

            for (int i = 0; i < conclusion.requiredEvidenceIds.Length - 1; i++)
            {
                string fromId = conclusion.requiredEvidenceIds[i];
                string toId = conclusion.requiredEvidenceIds[i + 1];
                bool connected = connectionManager != null && connectionManager.HasConnection(fromId, toId);
                connected |= worldConnectionManager != null && worldConnectionManager.HasConnection(fromId, toId);
                if (!connected)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
