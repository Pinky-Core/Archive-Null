#if UNITY_EDITOR
using System.Linq;
using ArchiveNull.UI;
using UnityEditor;
using UnityEngine;

public static class OfficeAssetManifestEditor
{
    private const string Path="Assets/ArchiveNull/Resources/OfficeWarmup.asset";
    [MenuItem("Archive NULL/Refresh office asset cache")]
    public static void Refresh()
    {
        var manifest=AssetDatabase.LoadAssetAtPath<OfficeAssetManifest>(Path);
        if(manifest==null) { manifest=ScriptableObject.CreateInstance<OfficeAssetManifest>(); AssetDatabase.CreateAsset(manifest,Path); }
        manifest.assets=AssetDatabase.GetDependencies("Assets/ArchiveNull/Scenes/MainMenu.unity",true)
            .Where(path=>!path.EndsWith(".unity") && !path.EndsWith(".cs"))
            .Select(AssetDatabase.LoadMainAssetAtPath)
            .Where(asset=>asset is Material || asset is Texture || asset is AudioClip || asset is Mesh || asset is GameObject || asset is TMPro.TMP_FontAsset)
            .Distinct().ToArray();
        EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets();
    }
    public static void Ensure() { if(AssetDatabase.LoadAssetAtPath<OfficeAssetManifest>(Path)==null) Refresh(); }
}
#endif
