using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// Cursors are scoped to the destination, never to the access key. Rotating a key
// continues delivery; changing the deployment starts a separate delivery history.
public sealed class TelemetryUploadQueue
{
    [Serializable] private sealed class Cursor {public long offset;}
    [Serializable] private sealed class Request {public string key;public GameTelemetry.Event[] events;}
    [Serializable] private sealed class Reply {public bool ok;public int guardados;}
    public sealed class Batch
    {
        public string source, checkpoint;
        public long start, end;
        public GameTelemetry.Event[] events;
    }
    public string LastError {get;private set;}
    public static string DestinationId(string endpoint)
    {using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(endpoint))).Replace("-","").ToLowerInvariant();}
    public Batch Next(string sourceDirectory,string checkpointDirectory,string endpoint,int maximum=100)
    {
        LastError=null;
        if(!Directory.Exists(sourceDirectory)) return null;
        maximum=Math.Max(1,Math.Min(100,maximum));
        foreach(string file in Directory.GetFiles(sourceDirectory,"*.jsonl").OrderBy(File.GetCreationTimeUtc))
        {
            try
            {
                string checkpoint=Path.Combine(checkpointDirectory,DestinationId(endpoint),Path.GetFileName(file)+".json");
                long offset=0;
                if(File.Exists(checkpoint))
                {
                    try {offset=JsonUtility.FromJson<Cursor>(File.ReadAllText(checkpoint))?.offset ?? 0;}
                    catch(ArgumentException) {offset=0;}
                }
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                {
                    if(offset<0 || offset>stream.Length) offset=0;
                    stream.Position=offset;
                    var events=new List<GameTelemetry.Event>();long end=offset;
                    using(var line=new MemoryStream())
                    {
                        while(events.Count<maximum)
                        {
                            int next=stream.ReadByte();
                            if(next<0) break; // An unfinished line remains pending until its newline arrives.
                            if(next!=10) {line.WriteByte((byte)next);if(line.Length>65536) throw new InvalidDataException();continue;}
                            string json=Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r');line.SetLength(0);line.Position=0;
                            var record=JsonUtility.FromJson<GameTelemetry.Event>(json);
                            if(record==null || record.schema!=1 || record.sequence<1 || string.IsNullOrEmpty(record.sessionId) || string.IsNullOrEmpty(record.runId) || string.IsNullOrEmpty(record.type) || record.test)
                                throw new InvalidDataException();
                            events.Add(record);end=stream.Position;
                        }
                    }
                    if(events.Count>0) return new Batch{source=file,checkpoint=checkpoint,start=offset,end=end,events=events.ToArray()};
                }
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {LastError="Hay un diario que no se pudo leer; se conserva pendiente.";}
        }
        return null;
    }
    public static string Body(Batch batch,string key)=>JsonUtility.ToJson(new Request{key=key,events=batch.events});
    public static bool Accepts(long status,string body,int count)
    {
        if(status<200 || status>=300 || string.IsNullOrEmpty(body) || !body.TrimStart().StartsWith("{")) return false;
        try
        {
            var reply=JsonUtility.FromJson<Reply>(body);
            return reply!=null && reply.ok && Regex.IsMatch(body,"\"guardados\"\\s*:\\s*(0|[1-9][0-9]*)\\s*[,}]") && reply.guardados>=0 && reply.guardados<=count;
        }
        catch(ArgumentException) {return false;}
    }
    public static void Acknowledge(Batch batch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(batch.checkpoint));
        string pending=batch.checkpoint+".pending";
        File.WriteAllText(pending,JsonUtility.ToJson(new Cursor{offset=batch.end}),new UTF8Encoding(false));
        if(File.Exists(batch.checkpoint)) File.Replace(pending,batch.checkpoint,null);else File.Move(pending,batch.checkpoint);
    }
}
