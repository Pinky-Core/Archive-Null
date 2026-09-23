using UnityEngine;

namespace ArchiveNull.UI
{
    // A single draw call; pixels animate in the shader, without per-frame allocations.
    public sealed class MemoryBoundaryPixels : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        public void Initialize(Vector3 center, float radius)
        {
            Shader shader = Resources.Load<Shader>("MemoryBoundaryPixels");
            if (shader == null) { Debug.LogError("Missing memory boundary pixel shader."); return; }
            transform.position = center;
            const int count = 1536;
            var positions = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var seeds = new Vector2[count * 4];
            var indices = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996323f;
                // Use a sequence independent of the angular golden ratio; otherwise
                // all particles at a given bearing line up at the same height.
                float seed = Mathf.Repeat(i * 0.754877666f, 1f);
                Vector3 point = new(Mathf.Cos(angle) * (radius - 0.18f), 0.3f + seed * 3.5f, Mathf.Sin(angle) * (radius - 0.18f));
                int v = i * 4, t = i * 6;
                for (int k = 0; k < 4; k++) { positions[v + k] = point; seeds[v + k] = new Vector2(seed, i); }
                uv[v] = new(-1, -1); uv[v + 1] = new(-1, 1); uv[v + 2] = new(1, 1); uv[v + 3] = new(1, -1);
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "Memory pixel ring", vertices = positions, uv = uv, uv2 = seeds, triangles = indices };
            mesh.bounds = new Bounds(new Vector3(0, 2f, 0), new Vector3(radius * 2 + 1, 6, radius * 2 + 1));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader);
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        public void SetProgress(float progress) { if (material != null) material.SetFloat("_Progress", Mathf.Clamp01(progress)); }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}
