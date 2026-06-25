using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class OfficeDissolveTransition : MonoBehaviour
    {
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [Header("References")]
        [SerializeField] private GameObject officeRoot;
        [SerializeField] private Shader fallbackDissolveShader;

        [Header("Timing")]
        [SerializeField] private float dissolveDuration = 1.25f;
        [SerializeField] private bool rebuildOnMainMenuReturn = true;
        [SerializeField] private bool restoreOriginalMaterialsAfterRebuild = true;
        [SerializeField] private bool fadeFromBlackOnMainMenuReturn = true;
        [SerializeField] private float returnFadeDuration = 0.55f;
        [SerializeField] private float postRebuildBlackHold = 0.08f;
        [SerializeField] private bool smoothMaterialRestore = true;
        [SerializeField] private float materialRestoreBlendDuration = 0.16f;
        [SerializeField] private float materialRestoreOverlayAlpha = 0.16f;

        public const string PendingOfficeRebuildPref = "archive.office.rebuild.pending";

        private MaterialPropertyBlock _propertyBlock;
        private RendererState[] _rendererStates;
        private bool _originalMaterialsRestored;

        public GameObject OfficeRoot => officeRoot;
        public float DissolveDuration => dissolveDuration;

        public void DisablePendingReturnRebuildCheck()
        {
            rebuildOnMainMenuReturn = false;
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public Material[] RuntimeDissolveMaterials;
            public bool UsesRuntimeDissolveMaterials;
        }

        private void Awake()
        {
            restoreOriginalMaterialsAfterRebuild = true;
            _propertyBlock = new MaterialPropertyBlock();
            if (fallbackDissolveShader == null)
            {
                fallbackDissolveShader = Shader.Find("ArchiveNull/RoomDissolve");
            }
        }

        private void Start()
        {
            if (!rebuildOnMainMenuReturn || PlayerPrefs.GetInt(PendingOfficeRebuildPref, 0) != 1)
            {
                return;
            }

            PlayerPrefs.SetInt(PendingOfficeRebuildPref, 0);
            PlayerPrefs.Save();
            StartCoroutine(PlayPendingReturnRebuild());
        }

        private IEnumerator PlayPendingReturnRebuild()
        {
            CanvasGroup fadeOverlay = fadeFromBlackOnMainMenuReturn ? CreateBlackFadeOverlay() : null;
            PrepareRebuildStart();

            if (fadeOverlay != null)
            {
                if (postRebuildBlackHold > 0f)
                {
                    yield return new WaitForSeconds(postRebuildBlackHold);
                }

                yield return FadeCanvasGroup(fadeOverlay, 1f, 0f, returnFadeDuration);
                Destroy(fadeOverlay.gameObject);
            }

            yield return PlayRebuild(restoreOriginalMaterialsAfterRebuild);
        }

        public IEnumerator PlayDissolve()
        {
            CacheRendererStates();
            EnsureDissolveMaterialsApplied();
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

        public IEnumerator PlayRebuild(bool restoreOriginalMaterials = false)
        {
            CacheRendererStates();
            EnsureDissolveMaterialsApplied();
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
                if (smoothMaterialRestore)
                {
                    yield return SmoothRestoreOriginalMaterials();
                }
                else
                {
                    RestoreOriginalMaterials();
                }
            }

            Debug.Log("[OfficeDissolveTransition] Office rebuild completed.");
        }

        public void PrepareRebuildStart()
        {
            CacheRendererStates();
            EnsureDissolveMaterialsApplied();
            ApplyDissolve(1f);
        }

        private void CacheRendererStates()
        {
            if (_rendererStates != null && _rendererStates.Length > 0)
            {
                return;
            }

            Renderer[] renderers = CollectRenderers();
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
                    state.RuntimeDissolveMaterials = BuildRuntimeDissolveMaterials(originalMaterials);
                    rendererTarget.sharedMaterials = state.RuntimeDissolveMaterials;
                    state.UsesRuntimeDissolveMaterials = true;
                }

                _rendererStates[i] = state;
            }
        }

        private Renderer[] CollectRenderers()
        {
            if (officeRoot != null)
            {
                return officeRoot.GetComponentsInChildren<Renderer>(true);
            }

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            System.Collections.Generic.List<Renderer> renderers = new();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == gameObject || root.transform.IsChildOf(transform))
                {
                    continue;
                }

                renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }

            return renderers.ToArray();
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

        private void EnsureDissolveMaterialsApplied()
        {
            if (!_originalMaterialsRestored || _rendererStates == null)
            {
                return;
            }

            for (int i = 0; i < _rendererStates.Length; i++)
            {
                RendererState state = _rendererStates[i];
                if (state?.Renderer == null || !state.UsesRuntimeDissolveMaterials || state.RuntimeDissolveMaterials == null)
                {
                    continue;
                }

                state.Renderer.sharedMaterials = state.RuntimeDissolveMaterials;
            }

            _originalMaterialsRestored = false;
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

                if (state.OriginalMaterials != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }

                state.Renderer.SetPropertyBlock(null);
            }

            _originalMaterialsRestored = true;
        }

        private IEnumerator SmoothRestoreOriginalMaterials()
        {
            CanvasGroup overlay = CreateMaterialRestoreOverlay();
            float halfDuration = Mathf.Max(0.01f, materialRestoreBlendDuration * 0.5f);
            float alpha = Mathf.Clamp01(materialRestoreOverlayAlpha);

            yield return FadeCanvasGroup(overlay, 0f, alpha, halfDuration);
            RestoreOriginalMaterials();
            yield return null;
            yield return FadeCanvasGroup(overlay, alpha, 0f, halfDuration);

            if (overlay != null)
            {
                Destroy(overlay.gameObject);
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
                SetTextureIfTargetHas(target, MainTexId, mainTexture);
                SetTextureIfTargetHas(target, BaseMapId, mainTexture);
            }

            Color color = GetFirstColor(source, Color.white, "_BaseColor", "_Color", "_TintColor");
            SetColorIfTargetHas(target, ColorId, color);
            SetColorIfTargetHas(target, BaseColorId, color);

            CopyTextureIfPresent(source, target, "_EmissionMap", EmissionMapId);
            CopyTextureIfPresent(source, target, "_BumpMap", BumpMapId);
            CopyTextureIfPresent(source, target, "_MetallicGlossMap", MetallicGlossMapId);
            CopyTextureIfPresent(source, target, "_OcclusionMap", OcclusionMapId);
            CopyColorIfPresent(source, target, "_EmissionColor", EmissionColorId);
            CopyFloatIfPresent(source, target, "_Metallic", MetallicId);
            CopyFloatIfPresent(source, target, "_Glossiness", GlossinessId);
            CopyFloatIfPresent(source, target, "_Smoothness", SmoothnessId);

            if (source.HasProperty("_MainTex"))
            {
                SetTextureTransformIfTargetHas(target, MainTexId, source.GetTextureScale("_MainTex"), source.GetTextureOffset("_MainTex"));
                SetTextureTransformIfTargetHas(target, BaseMapId, source.GetTextureScale("_MainTex"), source.GetTextureOffset("_MainTex"));
            }
            else if (source.HasProperty("_BaseMap"))
            {
                SetTextureTransformIfTargetHas(target, MainTexId, source.GetTextureScale("_BaseMap"), source.GetTextureOffset("_BaseMap"));
                SetTextureTransformIfTargetHas(target, BaseMapId, source.GetTextureScale("_BaseMap"), source.GetTextureOffset("_BaseMap"));
            }
        }

        private static void CopyTextureIfPresent(Material source, Material target, string sourceProperty, int targetProperty)
        {
            if (source.HasProperty(sourceProperty) && target.HasProperty(targetProperty))
            {
                Texture texture = source.GetTexture(sourceProperty);
                if (texture != null)
                {
                    target.SetTexture(targetProperty, texture);
                    target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
                    target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
                }
            }
        }

        private static void CopyColorIfPresent(Material source, Material target, string sourceProperty, int targetProperty)
        {
            if (source.HasProperty(sourceProperty) && target.HasProperty(targetProperty))
            {
                target.SetColor(targetProperty, source.GetColor(sourceProperty));
            }
        }

        private static void CopyFloatIfPresent(Material source, Material target, string sourceProperty, int targetProperty)
        {
            if (source.HasProperty(sourceProperty) && target.HasProperty(targetProperty))
            {
                target.SetFloat(targetProperty, source.GetFloat(sourceProperty));
            }
        }

        private static void SetTextureIfTargetHas(Material target, int propertyId, Texture texture)
        {
            if (target.HasProperty(propertyId))
            {
                target.SetTexture(propertyId, texture);
            }
        }

        private static void SetColorIfTargetHas(Material target, int propertyId, Color color)
        {
            if (target.HasProperty(propertyId))
            {
                target.SetColor(propertyId, color);
            }
        }

        private static void SetTextureTransformIfTargetHas(Material target, int propertyId, Vector2 scale, Vector2 offset)
        {
            if (!target.HasProperty(propertyId))
            {
                return;
            }

            target.SetTextureScale(propertyId, scale);
            target.SetTextureOffset(propertyId, offset);
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

        private static CanvasGroup CreateBlackFadeOverlay()
        {
            GameObject canvasObject = new("OfficeReturnFade", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject imageObject = new("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = true;
            return group;
        }

        private static CanvasGroup CreateMaterialRestoreOverlay()
        {
            GameObject canvasObject = new("MaterialRestoreBlend", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject imageObject = new("Blend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = new Color(0.015f, 0.045f, 0.04f, 1f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            group.alpha = from;
            if (duration <= 0f)
            {
                group.alpha = to;
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
