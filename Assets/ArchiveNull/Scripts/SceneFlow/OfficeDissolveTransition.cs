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
        [Header("Memory exploration boundary")]
        [SerializeField] private bool limitMemoryRadius;
        [SerializeField, Min(1f)] private float maximumMemoryRadius = 15f;
        [SerializeField] private Transform memoryOrigin;
        [SerializeField] private Vector3 memoryCenter;
        [SerializeField, Min(0.05f)] private float boundaryWidth = 0.65f;

        [SerializeField] private float dissolveDuration = 4.5f;
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
        private Vector3 waveOrigin;
        private float waveDistance;
        private Vector3 reconstructionOrigin;
        private bool retracting;
        public bool IsReconstructing { get; private set; }
        private static readonly int WaveOriginId = Shader.PropertyToID("_WaveOrigin");
        private static readonly int WaveRadiusId = Shader.PropertyToID("_WaveRadius");
        private static readonly int WaveWidthId = Shader.PropertyToID("_WaveWidth");

        public GameObject OfficeRoot => officeRoot;
        public float DissolveDuration => Mathf.Max(4.5f, dissolveDuration);
        public float RetractionDuration => DissolveDuration * .65f;
        public static OfficeDissolveTransition ActiveMemory { get; private set; }
        public Vector3 MemoryCenter => waveOrigin;
        public float MaximumMemoryRadius => maximumMemoryRadius;
        private MemoryBoundaryPixels boundaryPixels;

        public Vector3 ConstrainPosition(Vector3 position, float clearance = 0.35f)
        {
            if (!limitMemoryRadius) return position;
            Vector2 offset = new(position.x - waveOrigin.x, position.z - waveOrigin.z);
            offset = Vector2.ClampMagnitude(offset, Mathf.Max(0f, maximumMemoryRadius - clearance));
            return new Vector3(waveOrigin.x + offset.x, position.y, waveOrigin.z + offset.y);
        }

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
            maximumMemoryRadius = Mathf.Max(1f, maximumMemoryRadius);
            // A scene anchor, never the current player/save position.
            waveOrigin = memoryOrigin != null ? memoryOrigin.position : memoryCenter;
            if (limitMemoryRadius) ActiveMemory = this;
            restoreOriginalMaterialsAfterRebuild = true;
            _propertyBlock = new MaterialPropertyBlock();
            if (fallbackDissolveShader == null)
            {
                fallbackDissolveShader = Shader.Find("ArchiveNull/RoomDissolve");
            }
        }

        private void Start()
        {
            if (MemoryLoadingScreen.IsBusy) { PlayerPrefs.SetInt(PendingOfficeRebuildPref, 0); return; }
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

            if (fadeOverlay != null)
            {
                if (postRebuildBlackHold > 0f)
                {
                    yield return new WaitForSecondsRealtime(postRebuildBlackHold);
                }

                yield return FadeCanvasGroup(fadeOverlay, 1f, 0f, returnFadeDuration);
                Destroy(fadeOverlay.gameObject);
            }

            // The office is real: only the headset fades on.
        }

        public IEnumerator PlayDissolve()
        {
            CacheRendererStates();
            retracting = true;
            RenderSettings.skybox = null;
            if (Camera.main != null) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = Color.black; }
            reconstructionOrigin = Camera.main != null ? Camera.main.transform.position : waveOrigin;
            waveDistance = 1f;
            foreach (RendererState state in _rendererStates)
                if (state?.Renderer != null) waveDistance = Mathf.Max(waveDistance,
                    Vector3.Distance(reconstructionOrigin, state.Renderer.bounds.center) + state.Renderer.bounds.extents.magnitude);
            waveDistance += 1f;
            if (limitMemoryRadius) waveDistance = maximumMemoryRadius + Vector3.Distance(reconstructionOrigin,waveOrigin) + 5f;
            EnsureDissolveMaterialsApplied();
            ApplyDissolve(0f);
            Debug.Log("[OfficeDissolveTransition] Starting office dissolve.");

            float duration = RetractionDuration;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                ApplyDissolve(t);
                yield return null;
            }

            ApplyDissolve(1f);
            Debug.Log("[OfficeDissolveTransition] Office dissolve completed.");
        }

        public IEnumerator PlayRebuild(bool restoreOriginalMaterials = false)
        {
            IsReconstructing = true;
            CacheRendererStates();
            retracting = false;
            reconstructionOrigin = waveOrigin;
            if (limitMemoryRadius) waveDistance = maximumMemoryRadius;
            EnsureDissolveMaterialsApplied();
            ApplyDissolve(1f);
            Debug.Log("[OfficeDissolveTransition] Starting office rebuild.");

            float duration = Mathf.Max(4.5f, dissolveDuration);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
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
            IsReconstructing = false;
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
            if (!limitMemoryRadius) waveOrigin = Camera.main != null ? Camera.main.transform.position : transform.position;
            waveDistance = 1f;
            foreach (Renderer candidate in renderers)
                if (candidate != null)
                    waveDistance = Mathf.Max(waveDistance, Vector3.Distance(waveOrigin, candidate.bounds.center) + candidate.bounds.extents.magnitude);
            waveDistance += 1f;
            if (limitMemoryRadius) waveDistance = maximumMemoryRadius;
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
                    if (material == null || !material.HasProperty(WaveRadiusId))
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
            if (limitMemoryRadius && boundaryPixels == null)
            {
                boundaryPixels = new GameObject("Memory boundary pixels").AddComponent<MemoryBoundaryPixels>();
                boundaryPixels.Initialize(waveOrigin, maximumMemoryRadius);
            }
        }

        private Renderer[] CollectRenderers()
        {
            if (officeRoot != null && !limitMemoryRadius)
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

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.GetComponentInParent<FirstPersonMovement>() == null &&
                        renderer.GetComponentInParent<Camera>() == null &&
                        renderer.GetComponentInParent<Canvas>() == null &&
                        renderer is not ParticleSystemRenderer)
                        renderers.Add(renderer);
            }

            return renderers.ToArray();
        }

        private void ApplyDissolve(float amount)
        {
            if (boundaryPixels != null) boundaryPixels.SetProgress(1f - amount);
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
                _propertyBlock.SetVector(WaveOriginId, waveOrigin);
                _propertyBlock.SetVector("_ReconstructionOrigin", retracting ? reconstructionOrigin : waveOrigin);
                _propertyBlock.SetFloat(WaveRadiusId, Mathf.Lerp(-1f, waveDistance, 1f - amount));
                _propertyBlock.SetFloat(WaveWidthId, 0.45f);
                _propertyBlock.SetFloat("_MemoryRadius", limitMemoryRadius ? maximumMemoryRadius : 0f);
                _propertyBlock.SetFloat("_BoundaryWidth", boundaryWidth);
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
                    // Keep clipping on geometry touching or crossing the fixed border.
                    // Interior objects retain their authored lighting and materials.
                    Bounds bounds = state.Renderer.bounds;
                    Vector2 farthest = new(Mathf.Abs(bounds.center.x - waveOrigin.x) + bounds.extents.x,
                        Mathf.Abs(bounds.center.z - waveOrigin.z) + bounds.extents.z);
                    if (limitMemoryRadius && farthest.magnitude >= maximumMemoryRadius - boundaryWidth)
                        continue;
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }

                state.Renderer.SetPropertyBlock(null);
            }

            _originalMaterialsRestored = true;
        }

        private void OnDestroy()
        {
            if (ActiveMemory == this) ActiveMemory = null;
            if (boundaryPixels != null) Destroy(boundaryPixels.gameObject);
            if (_rendererStates == null) return;
            foreach (RendererState state in _rendererStates)
                if (state?.RuntimeDissolveMaterials != null)
                    foreach (Material material in state.RuntimeDissolveMaterials) if (material != null) Destroy(material);
        }

        private void OnDrawGizmosSelected()
        {
            if (!limitMemoryRadius) return;
            Vector3 center = Application.isPlaying ? waveOrigin : memoryOrigin != null ? memoryOrigin.position : memoryCenter;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2f / 96f, b = (i + 1) * Mathf.PI * 2f / 96f;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * maximumMemoryRadius,
                    center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * maximumMemoryRadius);
            }
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
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
