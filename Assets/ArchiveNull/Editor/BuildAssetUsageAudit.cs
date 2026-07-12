using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArchiveNull.EditorTools
{
    /// <summary>
    /// Audits project assets against enabled build scenes without modifying the project.
    /// Dynamic loads cannot always be inferred, so the report produces candidates rather than deleting files.
    /// </summary>
    public static class BuildAssetUsageAudit
    {
        private const string ReportPath = "Logs/BuildAssetUsageAudit.csv";
        private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".dll", ".asmdef", ".asmref", ".rsp", ".meta"
        };

        [MenuItem("Archive Null/Tools/Audit Build Asset Usage")]
        public static void Run()
        {
            string[] buildScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            HashSet<string> usedAssets = new(
                AssetDatabase.GetDependencies(buildScenes, true),
                StringComparer.OrdinalIgnoreCase);

            string[] allAssets = AssetDatabase.GetAllAssetPaths()
                .Where(IsAuditableProjectAsset)
                .ToArray();

            List<AuditEntry> entries = new(allAssets.Length);
            foreach (string assetPath in allAssets)
            {
                string fullPath = Path.GetFullPath(assetPath);
                long bytes = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0L;
                bool alwaysIncluded = IsAlwaysIncluded(assetPath);
                bool usedByBuildScenes = usedAssets.Contains(assetPath);
                entries.Add(new AuditEntry(assetPath, bytes, usedByBuildScenes, alwaysIncluded));
            }

            WriteReport(entries, buildScenes);

            long candidateBytes = entries
                .Where(entry => entry.Status == AssetStatus.Candidate)
                .Sum(entry => entry.Bytes);
            Debug.Log(
                $"[BuildAssetUsageAudit] {entries.Count} assets audited. " +
                $"{entries.Count(entry => entry.Status == AssetStatus.Candidate)} candidates " +
                $"({FormatBytes(candidateBytes)} on disk). Report: {ReportPath}");
        }

        private static bool IsAuditableProjectAsset(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsValidFolder(assetPath))
            {
                return false;
            }

            return !IgnoredExtensions.Contains(Path.GetExtension(assetPath));
        }

        private static bool IsAlwaysIncluded(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            return normalized.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("/StreamingAssets/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteReport(IEnumerable<AuditEntry> entries, IEnumerable<string> buildScenes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Logs");
            StringBuilder csv = new();
            csv.AppendLine("Status,Bytes,Megabytes,Extension,AssetPath");

            foreach (AuditEntry entry in entries.OrderByDescending(entry => entry.Bytes))
            {
                csv.Append(entry.Status).Append(',')
                    .Append(entry.Bytes).Append(',')
                    .Append((entry.Bytes / 1048576d).ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(Path.GetExtension(entry.Path))).Append(',')
                    .AppendLine(Escape(entry.Path));
            }

            csv.AppendLine();
            csv.AppendLine("Enabled build scenes");
            foreach (string scene in buildScenes)
            {
                csv.AppendLine(Escape(scene));
            }

            File.WriteAllText(ReportPath, csv.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string FormatBytes(long bytes)
        {
            return $"{bytes / 1048576d:0.0} MB";
        }

        private enum AssetStatus
        {
            Used,
            AlwaysIncluded,
            Candidate
        }

        private readonly struct AuditEntry
        {
            public AuditEntry(string path, long bytes, bool used, bool alwaysIncluded)
            {
                Path = path;
                Bytes = bytes;
                Status = used ? AssetStatus.Used : alwaysIncluded ? AssetStatus.AlwaysIncluded : AssetStatus.Candidate;
            }

            public string Path { get; }
            public long Bytes { get; }
            public AssetStatus Status { get; }
        }
    }
}
