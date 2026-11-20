using System.IO;
using UnityEditor;
using UnityEngine;

public static class TelemetryMenu
{
    [MenuItem("Archive NULL/Telemetría/Abrir registros locales")]
    private static void OpenRecords()
    {
        string path=Path.Combine(Application.persistentDataPath,"Telemetry");Directory.CreateDirectory(path);EditorUtility.RevealInFinder(path);
    }
    [MenuItem("Archive NULL/Telemetría/Abrir informe local")]
    private static void OpenReport()
    {
        string path=Path.GetFullPath("Artifacts/TelemetryReport/index.html");
        if(File.Exists(path)) Application.OpenURL(new System.Uri(path).AbsoluteUri);
        else EditorUtility.DisplayDialog("Telemetría", "Generá el informe con Tools/Export-Telemetry.ps1. Los registros se guardan al jugar; las pruebas automáticas se excluyen por defecto.","Entendido");
    }
}
