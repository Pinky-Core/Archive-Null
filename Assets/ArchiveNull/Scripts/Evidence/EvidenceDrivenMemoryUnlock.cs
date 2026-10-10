using UnityEngine;

namespace ArchiveNull.Evidence
{
    public sealed class EvidenceDrivenMemoryUnlock : MonoBehaviour
    {
        public const string ContractorOfficeUnlockedPref = "archive.memory.contractor_office.unlocked";


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (FindAnyObjectByType<EvidenceDrivenMemoryUnlock>() == null)
                new GameObject("EvidenceDrivenMemoryUnlock").AddComponent<EvidenceDrivenMemoryUnlock>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EvidenceInventory.Instance.OnInventoryChanged += Evaluate;
            Evaluate();
        }

        private void OnDestroy()
        {
            if (EvidenceInventory.ExistingInstance != null) EvidenceInventory.ExistingInstance.OnInventoryChanged -= Evaluate;
        }

        private void Evaluate() => RefreshProgress();
        public static void RefreshProgress()
        {
            bool alreadyUnlocked = PlayerPrefs.GetInt(ContractorOfficeUnlockedPref, 0) == 1;
            if (!CaseEvidenceCatalog.CanVisitContractor && !alreadyUnlocked) return;
            PlayerPrefs.SetInt(ContractorOfficeUnlockedPref, 1);
            PlayerPrefs.SetInt("crt.archive.unlocked", Mathf.Max(2, PlayerPrefs.GetInt("crt.archive.unlocked", 1)));
            PlayerPrefs.Save();
            if (alreadyUnlocked) return;
            EvidenceGuidanceController.ExistingInstance?.ShowInspectionSubtitle(GameLocalization.Text(
                "Nueva memoria disponible: oficina de Víctor Salas. Las evidencias reunidas justifican revisar sus comprobantes de obra.",
                "New memory available: Víctor Salas's office. The collected evidence justifies examining his construction records."));
        }
    }
}
