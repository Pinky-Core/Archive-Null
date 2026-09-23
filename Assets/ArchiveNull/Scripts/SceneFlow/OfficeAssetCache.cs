using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArchiveNull.UI
{
    // Keep only the office's asset dependencies alive. Scene objects still unload normally,
    // so returning runs the normal scene lifecycle and refreshes the investigation board.
    public sealed class OfficeAssetCache : MonoBehaviour
    {
        private static OfficeAssetCache instance;
        [SerializeField] private Object[] retainedAssets;
        [SerializeField] private OfficeAssetManifest warmAssets;
        public static int RetainedCount => instance != null && instance.retainedAssets != null ? instance.retainedAssets.Length : 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmWhenStartingInsideMemory()
        {
            if(SceneManager.GetActiveScene().name=="MainMenu" || instance!=null) return;
            var host=new GameObject("Office asset cache"); instance=host.AddComponent<OfficeAssetCache>(); DontDestroyOnLoad(host);
            instance.StartCoroutine(instance.Warm());
        }
        private System.Collections.IEnumerator Warm()
        {
            // Direct editor Play in a memory also prepares the office, without loading
            // another live scene, cameras, scripts, or input handlers alongside gameplay.
            ResourceRequest request=Resources.LoadAsync<OfficeAssetManifest>("OfficeWarmup");
            yield return request;
            warmAssets=request.asset as OfficeAssetManifest;
            if(warmAssets!=null && retainedAssets==null) retainedAssets=warmAssets.assets;
        }
        public static void Retain(Scene office)
        {
            if(office.name!="MainMenu") return;
            if(instance==null)
            {
                var host=new GameObject("Office asset cache"); instance=host.AddComponent<OfficeAssetCache>(); DontDestroyOnLoad(host);
            }
            var assets=new HashSet<Object>();
            void MaterialAssets(Material material)
            {
                if(material==null || !assets.Add(material)) return;
                if(material.shader!=null) assets.Add(material.shader);
                foreach(int id in material.GetTexturePropertyNameIDs()) { Texture texture=material.GetTexture(id); if(texture!=null) assets.Add(texture); }
            }
            foreach(GameObject root in office.GetRootGameObjects())
            {
                foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) foreach(Material material in renderer.sharedMaterials) MaterialAssets(material);
                foreach(MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(true)) if(mesh.sharedMesh!=null) assets.Add(mesh.sharedMesh);
                foreach(SkinnedMeshRenderer mesh in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if(mesh.sharedMesh!=null) assets.Add(mesh.sharedMesh);
                foreach(MeshCollider mesh in root.GetComponentsInChildren<MeshCollider>(true)) if(mesh.sharedMesh!=null) assets.Add(mesh.sharedMesh);
                foreach(AudioSource audio in root.GetComponentsInChildren<AudioSource>(true)) if(audio.clip!=null) assets.Add(audio.clip);
                foreach(ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true)) if(probe.texture!=null) assets.Add(probe.texture);
                foreach(TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                { if(text.font!=null) assets.Add(text.font); MaterialAssets(text.fontSharedMaterial); }
                foreach(UnityEngine.UI.Image image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)) if(image.sprite!=null) assets.Add(image.sprite);
                foreach(UnityEngine.UI.RawImage image in root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true)) if(image.texture!=null) assets.Add(image.texture);
            }
            MaterialAssets(RenderSettings.skybox);
            foreach(LightmapData map in LightmapSettings.lightmaps)
            { if(map.lightmapColor!=null) assets.Add(map.lightmapColor); if(map.lightmapDir!=null) assets.Add(map.lightmapDir); if(map.shadowMask!=null) assets.Add(map.shadowMask); }
            instance.retainedAssets=new Object[assets.Count]; assets.CopyTo(instance.retainedAssets);
        }
        private void OnDestroy() { if(instance==this) instance=null; }
    }
}
