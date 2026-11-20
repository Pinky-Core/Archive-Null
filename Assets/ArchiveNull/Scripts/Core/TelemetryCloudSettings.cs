using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class TelemetryCloudSettings
{
    public bool enabled = true;
    public string endpoint = "", key = "";
    public const string PackagedResource="TelemetryCloudConnection";
    public static string FilePath => Path.Combine(Application.persistentDataPath,"Telemetry","cloud.json");
    public bool IsReady => enabled && ValidEndpoint(endpoint) && !string.IsNullOrWhiteSpace(key);
    public static bool ValidEndpoint(string url) => url!=null && Regex.IsMatch(url,@"\Ahttps://script\.google\.com/macros/s/[A-Za-z0-9_-]+/exec\z");
    public static TelemetryCloudSettings Load()
    {
        try
        {
            string json=File.Exists(FilePath) ? File.ReadAllText(FilePath):Resources.Load<TextAsset>(PackagedResource)?.text;
            return string.IsNullOrEmpty(json) ? new TelemetryCloudSettings():JsonUtility.FromJson<TelemetryCloudSettings>(json) ?? new TelemetryCloudSettings();
        }
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException) {return new TelemetryCloudSettings();}
    }
    public void Save()
    {
        endpoint=(endpoint ?? "").Trim();key=(key ?? "").Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string pending=FilePath+".pending";File.WriteAllText(pending,JsonUtility.ToJson(this,true),new UTF8Encoding(false));
        if(File.Exists(FilePath)) File.Replace(pending,FilePath,null);else File.Move(pending,FilePath);
    }
}
