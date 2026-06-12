using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.Evidence
{
    public sealed class EvidenceInventory : MonoBehaviour
    {
        private static EvidenceInventory instance;
        private readonly List<EvidenceData> registeredEvidence = new List<EvidenceData>();
        private readonly HashSet<string> registeredIds = new HashSet<string>();
        private readonly Dictionary<string, string> notesByEvidenceId = new Dictionary<string, string>();
        private string operatorNotes = string.Empty;

        public static EvidenceInventory Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject host = new GameObject("EvidenceInventory");
                    instance = host.AddComponent<EvidenceInventory>();
                }

                return instance;
            }
        }

        public static EvidenceInventory ExistingInstance => instance;

        public event Action<EvidenceData> OnEvidenceRegistered;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool RegisterEvidence(EvidenceData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.evidenceId))
            {
                return false;
            }

            if (registeredIds.Contains(data.evidenceId))
            {
                return false;
            }

            registeredIds.Add(data.evidenceId);
            registeredEvidence.Add(data);
            OnEvidenceRegistered?.Invoke(data);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool HasEvidence(string evidenceId)
        {
            return !string.IsNullOrWhiteSpace(evidenceId) && registeredIds.Contains(evidenceId);
        }

        public IReadOnlyList<EvidenceData> GetAllEvidence()
        {
            return registeredEvidence;
        }

        public void RestoreEvidence(IEnumerable<EvidenceData> evidence)
        {
            registeredIds.Clear();
            registeredEvidence.Clear();

            if (evidence != null)
            {
                foreach (EvidenceData data in evidence)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.evidenceId) || registeredIds.Contains(data.evidenceId))
                    {
                        continue;
                    }

                    registeredIds.Add(data.evidenceId);
                    registeredEvidence.Add(data);
                }
            }

            OnInventoryChanged?.Invoke();
        }

        public IReadOnlyDictionary<string, string> GetAllNotes()
        {
            return notesByEvidenceId;
        }

        public void RestoreNotes(IDictionary<string, string> notes, string restoredOperatorNotes)
        {
            notesByEvidenceId.Clear();
            if (notes != null)
            {
                foreach (KeyValuePair<string, string> entry in notes)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key))
                    {
                        notesByEvidenceId[entry.Key] = entry.Value ?? string.Empty;
                    }
                }
            }

            operatorNotes = restoredOperatorNotes ?? string.Empty;
            OnInventoryChanged?.Invoke();
        }

        public string GetNote(string evidenceId)
        {
            return !string.IsNullOrWhiteSpace(evidenceId) && notesByEvidenceId.TryGetValue(evidenceId, out string note) ? note : string.Empty;
        }

        public void SetNote(string evidenceId, string note)
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                return;
            }

            notesByEvidenceId[evidenceId] = note ?? string.Empty;
            OnInventoryChanged?.Invoke();
        }

        public string GetOperatorNotes()
        {
            return operatorNotes;
        }

        public void SetOperatorNotes(string notes)
        {
            operatorNotes = notes ?? string.Empty;
            OnInventoryChanged?.Invoke();
        }
    }
}
