#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveNull.Evidence;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayableCaseValidation
{
    private const string RequestPath = "Temp/ArchiveValidation/request.txt";
    static PlayableCaseValidation() { EditorApplication.update += PollRequest; }

    private static void PollRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
        string request = File.ReadAllText(RequestPath).Trim();
        if (request == "build" || request == "audit" || request == "lab" || request == "editor-test")
        {
            File.WriteAllText(RequestPath, request + "-ready");
            SessionState.SetFloat("archive.validation.ready", (float)EditorApplication.timeSinceStartup + 5f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return;
        }
        if (EditorApplication.timeSinceStartup < SessionState.GetFloat("archive.validation.ready", 0f)) return;
        File.Delete(RequestPath);
        try
        {
            if (request == "audit-ready") Audit();
            else if (request == "build-ready") Build();
            else if (request == "lab-ready") MechanicsLabEditor.CreateLab();
            else if (request == "editor-test-ready") MechanicsLabEditor.TestInEditor();
        }
        catch (Exception exception) { File.WriteAllText("Temp/ArchiveValidation/failure.txt", exception.ToString()); }
    }

    [MenuItem("Archive NULL/Validate playable case")]
    public static void Audit()
    {
        var result = new StringBuilder();
        var allIds = new HashSet<string>();
        foreach (EditorBuildSettingsScene setting in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenPreviewScene(setting.path);
            try
            {
                result.AppendLine(setting.path);
                var ids = new HashSet<string>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                            result.AppendLine("FAIL missing script: " + child.name);
                    foreach (EvidenceTarget target in root.GetComponentsInChildren<EvidenceTarget>(true))
                    {
                        var serialized = new SerializedObject(target);
                        string id = serialized.FindProperty("evidenceId").stringValue;
                        result.AppendLine($"  {id}: {target.name} world={target.transform.position} active={target.gameObject.activeInHierarchy} colliders={target.GetComponentsInChildren<Collider>(true).Length}");
                        if (!ids.Add(id)) result.AppendLine("FAIL duplicate ID: " + id);
                        allIds.Add(id);
                    }
                    foreach (FirstPersonMovement player in root.GetComponentsInChildren<FirstPersonMovement>(true))
                        result.AppendLine($"  PLAYER {player.name} world={player.transform.position}");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        allIds.Add("phone_messages"); allIds.Add("phone_call_log");
        foreach (string[] chain in CaseEvidenceCatalog.ArgumentLinks)
            foreach (string pair in chain)
                foreach (string id in pair.Split('|'))
                    if (!allIds.Contains(id)) result.AppendLine("FAIL argument references missing evidence: " + id);
        result.AppendLine("Scene and argument audit complete.");
        Directory.CreateDirectory("Temp/ArchiveValidation");
        File.WriteAllText("Temp/ArchiveValidation/audit.txt", result.ToString());
    }

    [MenuItem("Archive NULL/Build playable Windows version")]
    public static void Build()
    {
        Audit();
        if (File.ReadAllText("Temp/ArchiveValidation/audit.txt").Contains("FAIL")) throw new InvalidOperationException("Scene validation failed; see audit.txt.");
        Directory.CreateDirectory("Builds/Windows");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/Windows/Archive NULL.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        File.WriteAllText("Temp/ArchiveValidation/build.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nTime: {report.summary.totalTime}");
    }
}
#endif
