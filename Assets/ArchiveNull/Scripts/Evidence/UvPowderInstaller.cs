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
                new(-0.024f, -0.014f), new(-0.012f, 0.018f), new(0.004f, -0.021f),
                new(0.019f, 0.009f), new(0.027f, -0.008f), new(-0.03f, 0.026f),
                new(0.01f, 0.029f), new(-0.004f, 0.004f), new(0.032f, 0.025f)
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
                float size = 0.009f + (i % 3) * 0.003f;
                spot.transform.localScale = new Vector3(size, size * 0.55f, 1f);
                spot.GetComponent<Renderer>().sharedMaterial = material;
                Collider collider = spot.GetComponent<Collider>();
                collider.isTrigger = true;
                spot.AddComponent<UvRevealTarget>();
            }
        }
    }
}
