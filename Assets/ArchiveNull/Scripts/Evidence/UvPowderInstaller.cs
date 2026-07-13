using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.Evidence
{
    public static class UvPowderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.name.Contains("House", System.StringComparison.OrdinalIgnoreCase)) return;
            EvidenceTarget[] targets = Object.FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include);
            for (int i = 0; i < targets.Length; i++)
            {
                EvidenceData data = targets[i].EvidenceData;
                if (data != null && (data.evidenceId ?? string.Empty).Contains("ornamentsmall"))
                {
                    CreatePowderSpots(targets[i].transform);
                    return;
                }
            }
        }

        private static void CreatePowderSpots(Transform parent)
        {
            if (parent.Find("HiddenUvPowder") != null) return;
            Transform root = new GameObject("HiddenUvPowder").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, 0.045f, 0f);

            Vector2[] positions =
            {
                new(-0.034f, -0.022f), new(-0.026f, -0.006f), new(-0.031f, 0.018f),
                new(-0.022f, 0.031f), new(-0.018f, -0.029f), new(-0.014f, 0.011f),
                new(-0.011f, 0.026f), new(-0.007f, -0.014f), new(-0.003f, 0.003f),
                new(0.001f, -0.032f), new(0.004f, 0.019f), new(0.008f, -0.019f),
                new(0.011f, 0.033f), new(0.014f, 0.007f), new(0.017f, -0.008f),
                new(0.021f, -0.027f), new(0.023f, 0.021f), new(0.027f, 0.003f),
                new(0.031f, -0.015f), new(0.034f, 0.029f), new(-0.036f, 0.006f),
                new(-0.002f, 0.036f), new(0.037f, 0.012f), new(0.006f, -0.039f)
            };

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material material = new(shader);
            material.color = new Color(0.72f, 0.92f, 1f, 0.92f);
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                spot.name = "PowderSpot_" + i;
                spot.transform.SetParent(root, false);
                spot.transform.localPosition = new Vector3(positions[i].x, 0f, positions[i].y);
                spot.transform.localRotation = Quaternion.Euler(90f, i * 19f, 0f);
                float size = 0.005f + (i % 4) * 0.0015f;
                spot.transform.localScale = new Vector3(size, size * 0.55f, 1f);
                spot.GetComponent<Renderer>().sharedMaterial = material;

                // A Quad primitive uses a concave MeshCollider, which Unity cannot use as a trigger.
                // A thin box provides the same UV hit area without participating in physical collisions.
                Collider primitiveCollider = spot.GetComponent<Collider>();
                primitiveCollider.enabled = false;
                Object.Destroy(primitiveCollider);

                BoxCollider revealTrigger = spot.AddComponent<BoxCollider>();
                revealTrigger.isTrigger = true;
                revealTrigger.size = new Vector3(1f, 1f, 0.02f);
                spot.AddComponent<UvRevealTarget>();
            }
        }
    }
}
