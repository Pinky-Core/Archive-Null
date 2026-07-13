using System.Collections.Generic;
using UnityEngine;

namespace ArchiveNull.Evidence
{
    [DisallowMultipleComponent]
    public sealed class UvRevealTarget : MonoBehaviour
    {
        private static readonly HashSet<UvRevealTarget> activeTargets = new();

        [SerializeField] private Renderer[] revealRenderers;
        [SerializeField] private CanvasGroup optionalCanvasGroup;
        [SerializeField] private Transform revealPoint;
        [SerializeField] private Collider revealCollider;
        [SerializeField] private float revealSeconds = 0.85f;
        [SerializeField] private float hideDelay = 0.45f;
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool preserveRevealedAmount = true;

        private float visibility;
        private float exposure;
        private float lastRevealTime = -999f;
        private MaterialPropertyBlock propertyBlock;
        private Color[] baseColors;

        public static IReadOnlyCollection<UvRevealTarget> ActiveTargets => activeTargets;

        public Vector3 RevealPosition
        {
            get
            {
                if (revealPoint != null)
                {
                    return revealPoint.position;
                }

                if (revealCollider != null)
                {
                    return revealCollider.bounds.center;
                }

                if (revealRenderers != null && revealRenderers.Length > 0 && revealRenderers[0] != null)
                {
                    return revealRenderers[0].bounds.center;
                }

                return transform.position;
            }
        }

        private void Awake()
        {
            if (revealRenderers == null || revealRenderers.Length == 0)
            {
                revealRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (revealCollider == null)
            {
                revealCollider = GetComponentInChildren<Collider>(true);
            }

            CacheRendererColors();

            if (hideOnStart)
            {
                visibility = 0f;
                exposure = 0f;
                ApplyVisibility(true);
            }
            else
            {
                visibility = 1f;
                exposure = 1f;
                lastRevealTime = Time.time;
                ApplyVisibility(true);
            }
        }

        private void OnEnable()
        {
            activeTargets.Add(this);
        }

        private void OnDisable()
        {
            activeTargets.Remove(this);
        }

        private void Update()
        {
            if (!hideOnStart)
            {
                return;
            }

            if (!preserveRevealedAmount && Time.time - lastRevealTime > hideDelay)
            {
                exposure = Mathf.MoveTowards(exposure, 0f, Time.deltaTime / Mathf.Max(0.05f, revealSeconds));
            }

            visibility = Mathf.MoveTowards(visibility, exposure, fadeSpeed * Time.deltaTime);
            ApplyVisibility(false);
        }

        public void RevealFromUv()
        {
            ReceiveUvIllumination(1f);
        }

        public void ReceiveUvIllumination(float strength)
        {
            lastRevealTime = Time.time;
            exposure = Mathf.Clamp01(exposure + Mathf.Clamp01(strength) * Time.deltaTime / Mathf.Max(0.05f, revealSeconds));
        }

        private void ApplyVisibility(bool immediate)
        {
            bool visible = immediate ? visibility > 0.001f : visibility > 0.01f;
            if (revealRenderers != null)
            {
                for (int i = 0; i < revealRenderers.Length; i++)
                {
                    if (revealRenderers[i] != null)
                    {
                        revealRenderers[i].enabled = visible;
                        ApplyRendererAlpha(revealRenderers[i], i);
                    }
                }
            }

            if (optionalCanvasGroup != null)
            {
                optionalCanvasGroup.alpha = visibility;
                optionalCanvasGroup.interactable = false;
                optionalCanvasGroup.blocksRaycasts = false;
            }
        }

        private void CacheRendererColors()
        {
            if (revealRenderers == null)
            {
                return;
            }

            propertyBlock = new MaterialPropertyBlock();
            baseColors = new Color[revealRenderers.Length];
            for (int i = 0; i < revealRenderers.Length; i++)
            {
                Renderer renderer = revealRenderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    baseColors[i] = Color.white;
                    continue;
                }

                Material material = renderer.sharedMaterial;
                if (material.HasProperty("_BaseColor"))
                {
                    baseColors[i] = material.GetColor("_BaseColor");
                }
                else if (material.HasProperty("_Color"))
                {
                    baseColors[i] = material.GetColor("_Color");
                }
                else
                {
                    baseColors[i] = Color.white;
                }
            }
        }

        private void ApplyRendererAlpha(Renderer renderer, int index)
        {
            if (renderer == null || renderer.sharedMaterial == null || propertyBlock == null)
            {
                return;
            }

            Color color = index >= 0 && baseColors != null && index < baseColors.Length ? baseColors[index] : Color.white;
            color.a *= Mathf.Clamp01(visibility);

            renderer.GetPropertyBlock(propertyBlock);
            Material material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
            {
                propertyBlock.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", color);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
