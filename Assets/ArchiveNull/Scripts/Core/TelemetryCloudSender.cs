using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class TelemetryCloudSender : MonoBehaviour
{
    public static string Status {get;private set;}="Sin iniciar";
    private UnityWebRequest active;
    private readonly TelemetryUploadQueue queue=new TelemetryUploadQueue();
    private IEnumerator Start()
    {
        yield return null;
        double retry=5;
        while(true)
        {
            float delay=30;
            var config=TelemetryCloudSettings.Load();
            if(GameTelemetry.IsTest) Status="Prueba automática: envío remoto desactivado";
            else if(!config.IsReady) Status=config.enabled ? "Falta configurar URL o INGEST_KEY":"Envío desactivado";
            else
            {
                string root=Path.Combine(Application.persistentDataPath,"Telemetry");
                var batch=queue.Next(Path.Combine(root,"playtests"),Path.Combine(root,"CloudCheckpoints"),config.endpoint);
                if(batch==null) Status=queue.LastError ?? "Sin eventos pendientes";
                else
                {
                    Status="Enviando lote de "+batch.events.Length+" eventos";
                    using(var request=CreateRequest(config.endpoint,TelemetryUploadQueue.Body(batch,config.key)))
                    {
                        active=request;
                        yield return request.SendWebRequest();
                        bool accepted=request.result==UnityWebRequest.Result.Success && TelemetryUploadQueue.Accepts(request.responseCode,request.downloadHandler.text,batch.events.Length);
                        active=null;
                        if(accepted)
                        {
                            try {TelemetryUploadQueue.Acknowledge(batch);Status="Lote confirmado; copia local conservada";retry=5;delay=1;}
                            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
                            {Status="Confirmado, pero no se pudo guardar el avance; se reintentará sin duplicar filas";delay=30;}
                        }
                        else
                        {
                            Status="Sin confirmación del receptor; eventos conservados para reintento";
                            delay=(float)retry;retry=Math.Min(300,retry*2);
                        }
                    }
                }
            }
            yield return new WaitForSecondsRealtime(delay);
        }
    }
    public static UnityWebRequest CreateRequest(string endpoint,string body)
    {
        var request=new UnityWebRequest(endpoint,"POST");
        request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.downloadHandler=new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type","application/json; charset=utf-8");request.timeout=20;request.redirectLimit=5;
        return request;
    }
    private void OnDestroy() {if(active!=null){active.Abort();active.Dispose();active=null;}}
}
