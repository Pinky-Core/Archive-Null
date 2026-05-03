using System.Collections;
using UnityEngine;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeDissolveTransition : MonoBehaviour
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [Header("References")]
        [SerializeField] private GameObject officeRoot;
        [SerializeField] private Shader fallbackDissolveShader;

        [Header("Timing")]
        [SerializeField] private float dissolveDuration = 1.25f;

        private MaterialPropertyBlock _propertyBlock;
        private RendererState[] _rendererStates;

        public GameObject OfficeRoot => officeRoot;
        public float DissolveDuration => dissolveDuration;

        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public bool UsesRuntimeDissolveMaterials;
        }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            if (fallbackDissolveShader == null)
            {
                fallbackDissolveShader = Shader.Find("ArchiveNull/RoomDissolve");
            }
        }

        public IEnumerator PlayDissolve()
        {
            CacheRendererStates();
            ApplyDissolve(0f);
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

        public IEnumerator PlayRebuild(bool restoreOriginalMaterials = true)
        {
            CacheRendererStates();
            ApplyDissolve(1f);
            Debug.Log("[OfficeDissolveTransition] Starting office rebuild.");

            float duration = Mathf.Max(0.001f, dissolveDuration);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                ApplyDissolve(1f - t);
                yield return null;
            }

            ApplyDissolve(0f);
            if (restoreOriginalMaterials)
            {
                RestoreOriginalMaterials();
            }

            Debug.Log("[OfficeDissolveTransition] Office rebuild completed.");
        }

        private void CacheRendererStates()
        {
            if (_rendererStates != null && _rendererStates.Length > 0)
            {
                return;
            }

            Renderer[] renderers = officeRoot != null ? officeRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
            _rendererStates = new RendererState[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rendererTarget = renderers[i];
                if (rendererTarget == null)
                {
                    continue;
                }

                Material[] originalMaterials = rendererTarget.sharedMaterials;
                bool supportsPropertyBlock = true;
                for (int materialIndex = 0; materialIndex < originalMaterials.Length; materialIndex++)
                {
                    Material material = originalMaterials[materialIndex];
                    if (material == null || !material.HasProperty(DissolveAmountId))
                    {
                        supportsPropertyBlock = false;
                        break;
                    }
                }

                RendererState state = new()
                {
                    Renderer = rendererTarget,
                    OriginalMaterials = originalMaterials
                };

                if (!supportsPropertyBlock && fallbackDissolveShader != null)
                {
                    rendererTarget.sharedMaterials = BuildRuntimeDissolveMaterials(originalMaterials);
                    state.UsesRuntimeDissolveMaterials = true;
                }

                _rendererStates[i] = state;
            }
        }

        private void ApplyDissolve(float amount)
        {
            if (_rendererStates == null)
            {
                return;
            }

            for (int i = 0; i < _rendererStates.Length; i++)
            {
                RendererState state = _rendererStates[i];
                if (state?.Renderer == null)
                {
                    continue;
                }

                state.Renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(DissolveAmountId, amount);
                state.Renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (_rendererStates == null)
            {
                return;
            }

            for (int i = 0; i < _rendererStates.Length; i++)
            {
                RendererState state = _rendererStates[i];
                if (state?.Renderer == null)
                {
                    continue;
                }

                if (state.UsesRuntimeDissolveMaterials && state.OriginalMaterials != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }

                state.Renderer.SetPropertyBlock(null);
            }
        }

        private Material[] BuildRuntimeDissolveMaterials(Material[] sourceMaterials)
        {
            Material[] runtimeMaterials = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                Material runtimeMaterial = new(fallbackDissolveShader)
                {
                    name = source != null ? source.name + " AutoDissolve Runtime" : "AutoDissolve Runtime"
                };

                CopyVisualProperties(source, runtimeMaterial);
                runtimeMaterials[i] = runtimeMaterial;
            }

            return runtimeMaterials;
        }

        private static void CopyVisualProperties(Material source, Material target)
        {
            if (source == null || target == null)
            {
                return;
            }

            Texture mainTexture = GetFirstTexture(source, "_BaseMap", "_MainTex", "_BaseColorMap");
            if (mainTexture != null)
            {
                target.SetTexture(MainTexId, mainTexture);
            }

            Color color = GetFirstColor(source, Color.white, "_BaseColor", "_Color", "_TintColor");
            target.SetColor(ColorId, color);
            target.SetColor(BaseColorId, Color.white);

            if (source.HasProperty("_MainTex"))
            {
                target.SetTextureScale(MainTexId, source.GetTextureScale("_MainTex"));
                target.SetTextureOffset(MainTexId, source.GetTextureOffset("_MainTex"));
            }
            else if (source.HasProperty("_BaseMap"))
            {
                target.SetTextureScale(MainTexId, source.GetTextureScale("_BaseMap"));
                target.SetTextureOffset(MainTexId, source.GetTextureOffset("_BaseMap"));
            }
        }

        private static Texture GetFirstTexture(Material material, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                Texture texture = material.GetTexture(propertyName);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Color GetFirstColor(Material material, Color fallback, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (material.HasProperty(propertyName))
                {
                    return material.GetColor(propertyName);
                }
            }

            return fallback;
        }
    }
}
