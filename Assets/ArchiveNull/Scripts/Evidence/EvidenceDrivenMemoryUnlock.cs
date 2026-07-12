using UnityEngine;

namespace ArchiveNull.Evidence
{
    public sealed class EvidenceDrivenMemoryUnlock : MonoBehaviour
    {
        public const string ContractorOfficeUnlockedPref = "archive.memory.contractor_office.unlocked";
        private const int RequiredEvidence = 6;

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

        private void Evaluate()
        {
            if (EvidenceInventory.Instance.GetAllEvidence().Count < RequiredEvidence || PlayerPrefs.GetInt(ContractorOfficeUnlockedPref, 0) == 1) return;
            PlayerPrefs.SetInt(ContractorOfficeUnlockedPref, 1);
            PlayerPrefs.SetInt("crt.archive.unlocked", Mathf.Max(2, PlayerPrefs.GetInt("crt.archive.unlocked", 1)));
            PlayerPrefs.Save();
            EvidenceGuidanceController.ExistingInstance?.ShowInspectionSubtitle(GameLocalization.Text(
                "Nueva memoria disponible: oficina de Víctor Salas. Las evidencias reunidas justifican revisar sus comprobantes de obra.",
                "New memory available: Víctor Salas's office. The collected evidence justifies examining his construction records."));
        }
    }
}
