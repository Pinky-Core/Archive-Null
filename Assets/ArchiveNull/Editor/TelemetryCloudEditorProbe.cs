using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TelemetryCloudEditorProbe
{
    private static readonly List<string> results=new List<string>();
    [MenuItem("Archive NULL/Telemetría/Probar cola local sin enviar datos")]
    public static void Run()
    {
        results.Clear();
        string root=Path.GetFullPath(".utmp/CloudQueueValidation/"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);string input=Path.Combine(root,"playtests"),acks=Path.Combine(root,"acks");Directory.CreateDirectory(input);
        const string destination="https://script.google.com/macros/s/synthetic_test/exec";
        try
        {
            var queue=new TelemetryUploadQueue();
            Check(queue.Next(input,acks,destination)==null,"empty queue does not invent events");
            string source=Path.Combine(input,"session.jsonl");
            File.WriteAllLines(source,Enumerable.Range(1,105).Select(Line),new UTF8Encoding(false));
            var first=queue.Next(input,acks,destination);
            Check(first.events.Length==100 && first.events[0].label=="Nota de Julián" && first.events[99].sequence==100,"batch limit and UTF-8 event contents are preserved");
            var same=queue.Next(input,acks,destination);
            Check(same.start==0 && same.end==first.end,"preparing a request never advances its durable cursor");
            Check(!TelemetryUploadQueue.Accepts(200,"{\"ok\":false,\"error\":\"Clave incorrecta\"}",100) && !TelemetryUploadQueue.Accepts(503,"{\"ok\":true,\"guardados\":100}",100),"authentication and HTTP failures are not acknowledgements");
            Check(!TelemetryUploadQueue.Accepts(200,"<html>Login</html>",100) && !TelemetryUploadQueue.Accepts(200,"{\"ok\":true,\"servicio\":\"Health\"}",100) && !TelemetryUploadQueue.Accepts(200,"{\"ok\":true,\"guardados\":101}",100) && !TelemetryUploadQueue.Accepts(200,"{\"ok\":true,\"guardados\":0.5}",100),"HTML, health replies and invalid saved counts cannot discard events");
            Check(TelemetryUploadQueue.Accepts(200,"{\"ok\":true,\"guardados\":100}",100) && TelemetryUploadQueue.Accepts(200,"{\"ok\":true,\"guardados\":0}",100),"new rows and server-deduplicated retries are accepted");
            TelemetryUploadQueue.Acknowledge(first);
            var resumed=new TelemetryUploadQueue().Next(input,acks,destination);
            Check(resumed.events.Length==5 && resumed.events[0].sequence==101,"a new queue instance resumes only unacknowledged events");
            TelemetryUploadQueue.Acknowledge(resumed);
            Check(queue.Next(input,acks,destination)==null && File.ReadAllLines(source).Length==105,"confirmed delivery retains the complete local journal");
            Check(queue.Next(input,acks,destination+"2").events[0].sequence==1,"changing the destination uses an independent cursor");
            var payload=TelemetryUploadQueue.Body(first,"synthetic-key");
            Check(payload.Contains("\"key\":\"synthetic-key\"") && payload.Contains("\"events\":[") && first.events.All(e=>!e.test),"request envelope matches the Apps Script receiver without test events");
            File.AppendAllText(source,Line(106),new UTF8Encoding(false));
            Check(queue.Next(input,acks,destination)==null,"unfinished final line is never uploaded or acknowledged");
            File.AppendAllText(source,"\n",new UTF8Encoding(false));
            var finished=queue.Next(input,acks,destination);
            Check(finished.events.Length==1 && finished.events[0].sequence==106,"completed final line becomes available without losing bytes");TelemetryUploadQueue.Acknowledge(finished);
            File.AppendAllText(source,"{broken}\n",new UTF8Encoding(false));
            Check(queue.Next(input,acks,destination)==null && queue.LastError!=null,"invalid records remain pending rather than being silently discarded");
            string tests=Path.Combine(root,"test-only");Directory.CreateDirectory(tests);
            File.WriteAllText(Path.Combine(tests,"test.jsonl"),JsonUtility.ToJson(new GameTelemetry.Event{sequence=1,sessionId="test",runId="test",type="test",test=true})+"\n");
            Check(queue.Next(tests,acks,destination)==null,"test records cannot enter the normal upload queue");
            Check(TelemetryCloudSettings.ValidEndpoint(destination) && !TelemetryCloudSettings.ValidEndpoint("http://script.google.com/macros/s/x/exec") && !TelemetryCloudSettings.ValidEndpoint(destination+"?key=secret") && !TelemetryCloudSettings.ValidEndpoint("https://script.google.com.evil.example/macros/s/x/exec"),"configuration accepts only the intended HTTPS deployment format");
            using(var request=TelemetryCloudSender.CreateRequest(destination,payload))
                Check(request.method=="POST" && request.timeout==20 && request.redirectLimit==5 && request.GetRequestHeader("Content-Type").StartsWith("application/json"),"Unity request has JSON POST, timeout and redirect handling");
            File.WriteAllLines(".utmp/cloud-queue-passed.txt",results);
        }
        catch(Exception error)
        {results.Add("FAIL "+error.GetType().Name+": "+error.Message);throw;}
        finally
        {
            Directory.CreateDirectory("Artifacts/CloudTelemetryValidation");
            File.WriteAllLines("Artifacts/CloudTelemetryValidation/results.txt",results,new UTF8Encoding(false));
        }
    }
    // CLI entry point used with -archive-feedback-test: queue tests, then the real Play Mode flow.
    public static void Start()
    {
        try {Run();FeedbackEditorProbe.Start();}
        catch {if(Environment.GetCommandLineArgs().Contains("-archive-feedback-test")) EditorApplication.Exit(1);else throw;}
    }
    private static string Line(int sequence)=>JsonUtility.ToJson(new GameTelemetry.Event{schema=1,sequence=sequence,sessionId="session",runId="run",type="evidence_collected",id="f1_house_cube",label="Nota de Julián"});
    private static void Check(bool success,string description)
    {if(!success) throw new InvalidOperationException(description);results.Add("PASS "+description);}
}
