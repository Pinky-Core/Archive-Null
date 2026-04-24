using System.Collections;
using UnityEngine;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeDissolveTransition : MonoBehaviour
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

        [Header("References")]
        [SerializeField] private GameObject officeRoot;

        [Header("Timing")]
        [SerializeField] private float dissolveDuration = 1.25f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;

        public GameObject OfficeRoot => officeRoot;
        public float DissolveDuration => dissolveDuration;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            CacheRenderers();
            ApplyDissolve(0f);
        }

        public IEnumerator PlayDissolve()
        {
            CacheRenderers();
            Debug.Log("[OfficeDissolveTransition] Starting office dissolve.");

            float duration = Mathf.Max(0.001f, dissolveDuration);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                ApplyDissolve(t);
                yield return null;
            }

            ApplyDissolve(1f);
            Debug.Log("[OfficeDissolveTransition] Office dissolve completed.");
        }

        private void CacheRenderers()
        {
            _renderers = officeRoot != null ? officeRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
        }

        private void ApplyDissolve(float amount)
        {
            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer rendererTarget = _renderers[i];
                if (rendererTarget == null)
                {
                    continue;
                }

                rendererTarget.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(DissolveAmountId, amount);
                rendererTarget.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
