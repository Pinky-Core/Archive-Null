using System;
using System.IO;
using ArchiveNull.Evidence;
using ArchiveNull.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Anonymous local journal. Optional cloud delivery is handled separately.
public sealed class GameTelemetry : MonoBehaviour
{
    [Serializable] public sealed class Progress
    {
        public string runId;
        public double activeSeconds, elapsedSeconds;
        public bool partial, completed;
    }
    [Serializable] public sealed class Event
    {
        public int schema = 1, sequence, value;
        public string utc, sessionId, runId, type, scene, id, relatedId, label, context, version;
        public bool test, partial;
        public double sessionActive, sessionElapsed, runActive, runElapsed;
        public float x, y, z, fps, maxFrameMs, managedMemoryMb;
    }
    private static GameTelemetry instance;
    private static Progress restored;
    private Progress progress;
    private string sessionId, scene, context;
    private StreamWriter journal;
    private double sessionActive, sessionElapsed;
    private float sampleAt, lastInput;
    private double lastTick, sampleStart;
    private int sampleFrames;
    private float worstFrame;
    private int sequence;
    private bool ready, failed, focused = true, suspended, ended;
    public static string DirectoryPath { get; private set; }
    public static bool IsTest { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance=null;restored=null;DirectoryPath=null;IsTest=false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if(instance==null) { var host=new GameObject("Local playtest telemetry");instance=host.AddComponent<GameTelemetry>();DontDestroyOnLoad(host); }
    }
    private void Start()
    {
        IsTest=Array.Exists(Environment.GetCommandLineArgs(),arg=>arg is "-archive-feedback-test" or "-archive-editor-test" or "-archive-smoke");
#if UNITY_EDITOR
        IsTest |= UnityEditor.SessionState.GetBool("archive.feedback.test",false) || UnityEditor.SessionState.GetBool("archive.mechanics.test",false);
#endif
        string initial=SceneManager.GetActiveScene().name;
        IsTest |= initial.IndexOf("Test",StringComparison.OrdinalIgnoreCase)>=0 || initial.IndexOf("Lab",StringComparison.OrdinalIgnoreCase)>=0;
        DirectoryPath=Path.Combine(Application.persistentDataPath,"Telemetry",IsTest ? "tests":"playtests");
        sessionId=Guid.NewGuid().ToString("N");
        progress=restored ?? NewProgress();
        if(restored==null) progress.partial=EvidenceInventory.Instance.GetAllEvidence().Count>0 || PlayerPrefs.GetInt(ArchiveNull.Accusation.AccusationPanel.CaseCompletedPref,0)==1 || initial!="MainMenu";
        scene=initial;focused=Application.isFocused;lastInput=Time.realtimeSinceStartup;
        lastTick=sampleStart=Time.realtimeSinceStartupAsDouble;
        progress.completed |= PlayerPrefs.GetInt(ArchiveNull.Accusation.AccusationPanel.CaseCompletedPref,0)==1;
        ready=true;
        Record("session_started",context:Application.isEditor ? "editor":"player");
        Record("run_resumed",context:progress.completed ? "already_completed":(restored!=null ? "saved_progress":"new_tracking"));
        foreach(var entry in CaseEvidenceCatalog.Entries)
            Record("catalog",entry.Id,label:entry.Name,context:entry.Number<=13 ? "F1-House":"F1-Office");
        foreach(var evidence in EvidenceInventory.Instance.GetAllEvidence()) Record("evidence_baseline",evidence.evidenceId);
        Record("scene_enter",scene);
        SceneManager.activeSceneChanged+=SceneChanged;
        EvidenceInventory.Instance.OnEvidenceRegistered+=EvidenceRegistered;
        gameObject.AddComponent<TelemetryCloudSender>();
    }
    private static Progress NewProgress()=>new Progress{runId=Guid.NewGuid().ToString("N")};
    public static void RestoreProgress(Progress value)
    {
        if(value==null || string.IsNullOrEmpty(value.runId)) return;
        restored=value;
        if(instance!=null && instance.ready) instance.progress=value;
    }
    public static Progress CaptureProgress()=>instance!=null && instance.ready ? instance.progress:restored;
    public static void NewRun()
    {
        if(instance==null || !instance.ready) {restored=null;return;}
        Record("run_reset");instance.progress=NewProgress();restored=instance.progress;
        Record("run_started");Record("scene_enter",SceneManager.GetActiveScene().name);
    }
    public static void Report(int score,string failedRows)
    {
        Record("report_submitted",relatedId:failedRows,value:score);
        if(instance!=null && instance.ready && score==100 && !instance.progress.completed)
        { instance.progress.completed=true;Record("case_completed"); }
    }
    public static void Record(string type,string id="",string relatedId="",string label="",string context="",int value=0)
    {
        if(instance==null || !instance.ready || instance.failed) return;
        var self=instance;
        var row=new Event{sequence=++self.sequence,utc=DateTime.UtcNow.ToString("O"),sessionId=self.sessionId,runId=self.progress.runId,
            type=type,scene=self.scene,id=id,relatedId=relatedId,label=label,context=context,value=value,version=Application.version,
            test=IsTest,partial=self.progress.partial,sessionActive=self.sessionActive,sessionElapsed=self.sessionElapsed,
            runActive=self.progress.activeSeconds,runElapsed=self.progress.elapsedSeconds};
        if(type=="heartbeat" && Camera.main!=null)
        { Vector3 p=Camera.main.transform.position;row.x=Mathf.Round(p.x*2)/2;row.y=Mathf.Round(p.y*2)/2;row.z=Mathf.Round(p.z*2)/2; }
        if(type=="heartbeat")
        { row.fps=(float)(self.sampleFrames/Math.Max(.001,Time.realtimeSinceStartupAsDouble-self.sampleStart));row.maxFrameMs=self.worstFrame;row.managedMemoryMb=GC.GetTotalMemory(false)/(1024f*1024f); }
        try
        {
            if(self.journal==null)
            { Directory.CreateDirectory(DirectoryPath);self.journal=new StreamWriter(Path.Combine(DirectoryPath,self.sessionId+".jsonl"),true,new System.Text.UTF8Encoding(false)){AutoFlush=true}; }
            self.journal.WriteLine(JsonUtility.ToJson(row));
        }
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
        { self.failed=true;self.journal?.Dispose();self.journal=null;Debug.LogWarning("No se pudo escribir la telemetría local: "+e.GetType().Name); }
    }
    private void EvidenceRegistered(EvidenceData data)=>Record("evidence_collected",data.evidenceId,label:CaseEvidenceCatalog.Find(data.evidenceId)?.Name ?? data.evidenceName,context:data.sourceSceneName);
    private void SceneChanged(Scene from,Scene to)
    { Record("scene_exit",from.name);scene=to.name;Record("scene_enter",to.name); }
    private void Update()
    {
        if(!ready) return;
        bool input=(Keyboard.current!=null && Keyboard.current.anyKey.isPressed) || (Mouse.current!=null && (Mouse.current.delta.ReadValue().sqrMagnitude>.01f || Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.scroll.ReadValue().sqrMagnitude>0)) || (Gamepad.current!=null && (Gamepad.current.leftStick.ReadValue().sqrMagnitude>.01f || Gamepad.current.rightStick.ReadValue().sqrMagnitude>.01f || Gamepad.current.buttonSouth.isPressed));
        if(input) lastInput=Time.realtimeSinceStartup;
        bool loading=MemoryLoadingScreen.IsBusy;
        string next=!focused || suspended ? "unfocused":loading ? "loading":GlobalPauseMenu.IsPaused ? "paused":Time.realtimeSinceStartup-lastInput>120 ? "idle":"active";
        double dt=Math.Max(0,Time.realtimeSinceStartupAsDouble-lastTick);lastTick=Time.realtimeSinceStartupAsDouble;
        sessionElapsed+=dt;progress.elapsedSeconds+=dt;
        // Long main-thread stalls count as elapsed time, not as active investigation.
        if(context=="active" && next=="active") {double active=Math.Min(dt,1);sessionActive+=active;progress.activeSeconds+=active;}
        sampleFrames++;worstFrame=Mathf.Max(worstFrame,(float)dt*1000);
        if(next!=context) {context=next;Record("activity",context:next);}
        if(Time.realtimeSinceStartup>=sampleAt) {sampleAt=Time.realtimeSinceStartup+10;Record("heartbeat",context:context);sampleFrames=0;sampleStart=Time.realtimeSinceStartupAsDouble;worstFrame=0;}
    }
    private void OnApplicationFocus(bool value) {focused=value;if(ready) Record("focus",value:value ? 1:0);}
    private void OnApplicationPause(bool value) {suspended=value;}
    private void End()
    { if(!ready || ended) return;ended=true;Record("session_ended");journal?.Dispose();journal=null; }
    private void OnApplicationQuit()=>End();
    private void OnDestroy()
    {
        End();SceneManager.activeSceneChanged-=SceneChanged;
        if(EvidenceInventory.ExistingInstance!=null) EvidenceInventory.ExistingInstance.OnEvidenceRegistered-=EvidenceRegistered;
        if(instance==this) instance=null;
    }
}
