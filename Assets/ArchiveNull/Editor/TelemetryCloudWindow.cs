using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class TelemetryCloudWindow : EditorWindow
{
    private TelemetryCloudSettings settings;
    private UnityWebRequest probe;
    private string message="";
    [MenuItem("Archive NULL/Telemetría/Configurar Google Sheets")]
    public static void Open()=>GetWindow<TelemetryCloudWindow>("Google Sheets");
    private void OnEnable() {settings=TelemetryCloudSettings.Load();EditorApplication.update+=Poll;}
    private void OnDisable() {EditorApplication.update-=Poll;probe?.Abort();probe?.Dispose();probe=null;}
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Conexión de telemetría",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configuración local, fuera del repositorio. Las partidas normales se envían por lotes; las regresiones automáticas quedan excluidas.",MessageType.Info);
        settings.enabled=EditorGUILayout.Toggle("Activar envío",settings.enabled);
        settings.endpoint=EditorGUILayout.TextField("URL /exec",settings.endpoint);
        settings.key=EditorGUILayout.PasswordField("INGEST_KEY",settings.key);
        if(GUILayout.Button("Guardar configuración local")) Save();
        using(new EditorGUI.DisabledScope(probe!=null || !settings.IsReady))
            if(GUILayout.Button("Enviar prueba a la pestaña Pruebas")) SendProbe();
        EditorGUILayout.HelpBox("La prueba crea un solo evento identificado como prueba. Para enviar partidas, guardá la configuración y jugá desde MainMenu. Las copias locales no se eliminan.",MessageType.None);
        EditorGUILayout.LabelField("Juego: "+TelemetryCloudSender.Status,EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(message,EditorStyles.wordWrappedLabel);
    }
    private bool Save()
    {
        try {settings.Save();message="Configuración guardada. No se añade a Git.";return true;}
        catch(Exception) {message="No se pudo guardar la configuración local.";return false;}
    }
    private void SendProbe()
    {
        if(!Save()) return;
        var entry=new GameTelemetry.Event{schema=1,sequence=1,test=true,sessionId="connection_"+Guid.NewGuid().ToString("N"),runId="connection_test",utc=DateTime.UtcNow.ToString("O"),type="connection_test",version=Application.version};
        var batch=new TelemetryUploadQueue.Batch{events=new[]{entry}};
        probe=TelemetryCloudSender.CreateRequest(settings.endpoint,TelemetryUploadQueue.Body(batch,settings.key));
        probe.SendWebRequest();message="Comprobando recepción…";
    }
    private void Poll()
    {
        if(probe==null || !probe.isDone) return;
        message=probe.result==UnityWebRequest.Result.Success && TelemetryUploadQueue.Accepts(probe.responseCode,probe.downloadHandler.text,1)
            ? "Recepción confirmada. Revisá la pestaña Pruebas de Google Sheets."
            : "No se confirmó la recepción. Revisá INGEST_KEY, la implementación y Ejecuciones en Apps Script.";
        probe.Dispose();probe=null;Repaint();
    }
}
