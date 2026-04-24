using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class RoomDissolveTransition : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Renderer[] _roomRenderers;
        [SerializeField] private Shader _dissolveShader;
        [SerializeField] private bool _applyDissolveShaderAtRuntime = true;
        [SerializeField] private bool _restoreOriginalMaterialsAfterRebuild = true;
        [SerializeField] private string _dissolveProperty = "_DissolveAmount";

        [Header("Dissolve Look")]
        [SerializeField] private Color _edgeColor = new Color(0.05f, 0.75f, 1f, 1f);
        [SerializeField] private float _pixelScale = 18f;
        [SerializeField] private float _noiseScale = 8f;
        [SerializeField] private float _edgeWidth = 0.07f;
        [SerializeField] private float _edgeEmission = 3f;
        [SerializeField] private float _fragmentJitter = 0.025f;

        [Header("Timing")]
        [SerializeField] private float _dissolveOutDuration = 1.4f;
        [SerializeField] private bool _rebuildOnStart;
        [SerializeField] private float _rebuildDuration = 1.2f;
        [SerializeField] private AnimationCurve _dissolveEase = null;

        private MaterialPropertyBlock _propertyBlock;
        private RendererMaterialState[] _materialStates;

        private sealed class RendererMaterialState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] DissolveMaterials;
        }

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            if (_dissolveEase == null || _dissolveEase.length == 0)
            {
                _dissolveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (_dissolveShader == null)
            {
                _dissolveShader = Shader.Find("ArchiveNull/RoomDissolve");
            }

            if (_applyDissolveShaderAtRuntime)
            {
                BuildRuntimeDissolveMaterials();
            }
        }

        private void Start()
        {
            if (_rebuildOnStart)
            {
                StartCoroutine(RebuildIn());
            }
        }

        public IEnumerator PlayAndLoad(int sceneBuildIndex)
        {
            yield return DissolveOut();

            if (sceneBuildIndex >= 0)
            {
                SceneManager.LoadScene(sceneBuildIndex);
            }
        }

        private IEnumerator DissolveOut()
        {
            UseDissolveMaterials();

            float timer = 0f;
            while (timer < _dissolveOutDuration)
            {
                timer += Time.deltaTime;
                float t = _dissolveEase.Evaluate(Mathf.Clamp01(timer / Mathf.Max(0.001f, _dissolveOutDuration)));
                ApplyDissolve(t);
                yield return null;
            }

            ApplyDissolve(1f);
        }

        private IEnumerator RebuildIn()
        {
            UseDissolveMaterials();
            ApplyDissolve(1f);
            float timer = 0f;
            while (timer < _rebuildDuration)
            {
                timer += Time.deltaTime;
                float t = _dissolveEase.Evaluate(Mathf.Clamp01(timer / Mathf.Max(0.001f, _rebuildDuration)));
                ApplyDissolve(1f - t);
                yield return null;
            }

            ApplyDissolve(0f);

            if (_restoreOriginalMaterialsAfterRebuild)
            {
                RestoreOriginalMaterials();
            }
        }

        private void ApplyDissolve(float value)
        {
            if (_roomRenderers == null)
            {
                return;
            }

            for (int i = 0; i < _roomRenderers.Length; i++)
            {
                Renderer target = _roomRenderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(_dissolveProperty, value);
                _propertyBlock.SetColor("_EdgeColor", _edgeColor);
                _propertyBlock.SetFloat("_PixelScale", _pixelScale);
                _propertyBlock.SetFloat("_NoiseScale", _noiseScale);
                _propertyBlock.SetFloat("_EdgeWidth", _edgeWidth);
                _propertyBlock.SetFloat("_EdgeEmission", _edgeEmission);
                _propertyBlock.SetFloat("_FragmentJitter", _fragmentJitter);
                target.SetPropertyBlock(_propertyBlock);
            }
        }

        private void BuildRuntimeDissolveMaterials()
        {
            if (_roomRenderers == null || _dissolveShader == null)
            {
                return;
            }

            _materialStates = new RendererMaterialState[_roomRenderers.Length];
            for (int i = 0; i < _roomRenderers.Length; i++)
            {
                Renderer target = _roomRenderers[i];
                if (target == null)
                {
                    continue;
                }

                Material[] originalMaterials = target.sharedMaterials;
                Material[] dissolveMaterials = new Material[originalMaterials.Length];

                for (int m = 0; m < originalMaterials.Length; m++)
                {
                    Material original = originalMaterials[m];
                    Material dissolve = new(_dissolveShader)
                    {
                        name = original != null ? original.name + " Dissolve Runtime" : "Dissolve Runtime"
                    };

                    CopyCommonMaterialProperties(original, dissolve);
                    dissolveMaterials[m] = dissolve;
                }

                _materialStates[i] = new RendererMaterialState
                {
                    Renderer = target,
                    OriginalMaterials = originalMaterials,
                    DissolveMaterials = dissolveMaterials
                };
            }
        }

        private void UseDissolveMaterials()
        {
            if (!_applyDissolveShaderAtRuntime || _materialStates == null)
            {
                return;
            }

            for (int i = 0; i < _materialStates.Length; i++)
            {
                RendererMaterialState state = _materialStates[i];
                if (state?.Renderer == null || state.DissolveMaterials == null)
                {
                    continue;
                }

                state.Renderer.sharedMaterials = state.DissolveMaterials;
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (_materialStates == null)
            {
                return;
            }

            for (int i = 0; i < _materialStates.Length; i++)
            {
                RendererMaterialState state = _materialStates[i];
                if (state?.Renderer == null || state.OriginalMaterials == null)
                {
                    continue;
                }

                state.Renderer.sharedMaterials = state.OriginalMaterials;
            }
        }

        private static void CopyCommonMaterialProperties(Material source, Material target)
        {
            if (source == null || target == null)
            {
                return;
            }

            Texture mainTexture = GetFirstTexture(source, "_BaseMap", "_MainTex", "_BaseColorMap");
            if (mainTexture != null)
            {
                target.SetTexture("_MainTex", mainTexture);
            }

            Color color = GetFirstColor(source, Color.white, "_BaseColor", "_Color", "_TintColor");
            target.SetColor("_Color", color);
            target.SetColor("_BaseColor", Color.white);

            if (source.HasProperty("_MainTex"))
            {
                target.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
                target.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
            }
            else if (source.HasProperty("_BaseMap"))
            {
                target.SetTextureScale("_MainTex", source.GetTextureScale("_BaseMap"));
                target.SetTextureOffset("_MainTex", source.GetTextureOffset("_BaseMap"));
            }
        }

        private static Texture GetFirstTexture(Material material, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null)
                    {
                        return texture;
                    }
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
