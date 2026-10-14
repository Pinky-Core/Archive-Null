#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ArchiveNull.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class OfficeAssetManifestEditor
{
    private const string Path="Assets/ArchiveNull/Resources/OfficeWarmup.asset";
    private const string ValidationRequest=".utmp/office-asset-cache-validation.request";

    [InitializeOnLoadMethod]
    private static void ScheduleRepair()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Ensure();
            if (!File.Exists(ValidationRequest)) return;
            File.Delete(ValidationRequest);
            Validate();
        };
    }

    private static bool IsRuntimePath(string path)
    {
        string normalized=path.Replace('\\','/');
        return !string.IsNullOrEmpty(normalized)
            && !normalized.Contains("/Editor/",StringComparison.OrdinalIgnoreCase)
            && !normalized.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)
            && !normalized.EndsWith(".cs",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeAsset(UnityEngine.Object asset)
    {
        return asset!=null && EditorUtility.IsPersistent(asset)
            && IsRuntimePath(AssetDatabase.GetAssetPath(asset))
            && (asset.hideFlags & HideFlags.DontSaveInBuild)==0
            && (asset is Material || asset is Texture || asset is AudioClip || asset is Mesh || asset is GameObject || asset is TMPro.TMP_FontAsset);
    }

    [MenuItem("Archive NULL/Refresh office asset cache")]
    public static void Refresh()
    {
        var manifest=AssetDatabase.LoadAssetAtPath<OfficeAssetManifest>(Path);
        if(manifest==null) { manifest=ScriptableObject.CreateInstance<OfficeAssetManifest>(); AssetDatabase.CreateAsset(manifest,Path); }
        manifest.assets=AssetDatabase.GetDependencies("Assets/ArchiveNull/Scenes/MainMenu.unity",true)
            .Where(IsRuntimePath)
            .Select(AssetDatabase.LoadMainAssetAtPath)
            .Where(IsRuntimeAsset)
            .Distinct().ToArray();
        EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets();
    }
    public static void Ensure()
    {
        var manifest=AssetDatabase.LoadAssetAtPath<OfficeAssetManifest>(Path);
        if(manifest==null || manifest.assets==null || manifest.assets.Any(asset=>!IsRuntimeAsset(asset))) Refresh();
    }

    [MenuItem("Archive NULL/Validate office asset cache")]
    public static void Validate()
    {
        var manifest=AssetDatabase.LoadAssetAtPath<OfficeAssetManifest>(Path);
        if(manifest==null || manifest.assets==null || manifest.assets.Length==0 || manifest.assets.Any(asset=>!IsRuntimeAsset(asset)))
            throw new BuildFailedException("La precarga de oficina contiene referencias ausentes o no válidas para el jugador.");
        if(IsRuntimePath("Packages/com.unity.render-pipelines.core/Editor/Icons/Processed/d_Volume Icon.asset"))
            throw new InvalidOperationException("El filtro no rechaza iconos del editor.");
        var temporary=new Texture2D(1,1) { hideFlags=HideFlags.DontSaveInBuild };
        try { if(IsRuntimeAsset(temporary)) throw new InvalidOperationException("El filtro acepta recursos temporales."); }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
        Directory.CreateDirectory("Artifacts/EditorValidation");
        File.WriteAllLines("Artifacts/EditorValidation/office-asset-cache.txt",new[]
        {
            "PASS office warmup retains "+manifest.assets.Length+" persistent runtime assets",
            "PASS office warmup contains no Editor paths or DontSaveInBuild objects",
            "PASS filter rejects the editor Volume icon path",
            "PASS filter rejects temporary textures",
            "LIMIT player build was not executed; native shared-memory allocator failure is not validated by this check"
        });
        Debug.Log("[OfficeAssetManifest] Validación aprobada: "+manifest.assets.Length+" recursos de precarga; sin referencias de editor. No se generó un build.");
    }
}

public sealed class OfficeAssetManifestBuildPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        OfficeAssetManifestEditor.Refresh();
        OfficeAssetManifestEditor.Validate();
    }
}
#endif
