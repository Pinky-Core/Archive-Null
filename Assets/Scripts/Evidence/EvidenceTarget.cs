using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceTarget : MonoBehaviour
    {
        [Header("Optional Shared Asset")]
        [Tooltip("Opcional. Dejalo vacio para configurar esta evidencia directamente en este objeto.")]
        [SerializeField] private EvidenceData evidenceData;

        [Header("Evidence")]
        [Tooltip("Id unico. Si queda vacio se genera desde la escena y el nombre del objeto.")]
        [SerializeField] private string evidenceId;
        [SerializeField] private string evidenceName;
        [TextArea(3, 8)]
        [SerializeField] private string description;
        [SerializeField] private EvidenceCategory category = EvidenceCategory.Other;
        [SerializeField] private Sprite photoSprite;
        [SerializeField] private bool validEvidence = true;
        [SerializeField] private string invalidMessage = "No hay evidencia util en este objetivo.";

        private EvidenceData runtimeEvidenceData;

        public EvidenceData EvidenceData => evidenceData != null ? evidenceData : GetOrCreateRuntimeData();
        public string InvalidMessage => invalidMessage;

        public bool CanRegister(out string message)
        {
            EvidenceData data = EvidenceData;
            if (!validEvidence || data == null)
            {
                message = invalidMessage;
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.evidenceId))
            {
                message = "Esta evidencia no tiene id.";
                return false;
            }

            if (EvidenceInventory.Instance.HasEvidence(data.evidenceId))
            {
                message = "Evidencia ya registrada.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (evidenceData != null)
            {
                return;
            }

            EnsureInlineId();
            if (string.IsNullOrWhiteSpace(evidenceName))
            {
                evidenceName = gameObject.name;
            }

            SyncRuntimeData();
        }

        private EvidenceData GetOrCreateRuntimeData()
        {
            EnsureInlineId();
            if (runtimeEvidenceData == null)
            {
                runtimeEvidenceData = ScriptableObject.CreateInstance<EvidenceData>();
                runtimeEvidenceData.name = evidenceId + "_RuntimeEvidence";
            }

            SyncRuntimeData();
            return runtimeEvidenceData;
        }

        private void SyncRuntimeData()
        {
            if (runtimeEvidenceData == null)
            {
                return;
            }

            runtimeEvidenceData.evidenceId = evidenceId;
            runtimeEvidenceData.evidenceName = string.IsNullOrWhiteSpace(evidenceName) ? gameObject.name : evidenceName;
            runtimeEvidenceData.description = description;
            runtimeEvidenceData.category = category;
            runtimeEvidenceData.photoSprite = photoSprite;
            runtimeEvidenceData.sourceSceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
        }

        private void EnsureInlineId()
        {
            if (!string.IsNullOrWhiteSpace(evidenceId))
            {
                return;
            }

            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
            evidenceId = SanitizeId(sceneName + "_" + gameObject.name);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "evidence";
            }

            char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
