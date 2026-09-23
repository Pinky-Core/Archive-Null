#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using ArchiveNull.Evidence;
using ArchiveNull.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class MechanicsEditorProbe
{
    private static double deadline;
    private static int phase;
    private static bool capturedRebuild;
    private static readonly List<string> results=new();
    static MechanicsEditorProbe()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("archive.mechanics.test",false))
            { results.Clear(); capturedRebuild=false; Application.runInBackground=true; deadline=EditorApplication.timeSinceStartup+9; phase=0; Application.logMessageReceived+=Log; }
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("archive.mechanics.restore",false))
            {
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("archive.mechanics.oldStart","")); SessionState.SetBool("archive.mechanics.restore",false);
                if(Environment.GetCommandLineArgs().Contains("-archive-editor-test")) EditorApplication.delayCall+=()=>EditorApplication.Exit(SessionState.GetBool("archive.mechanics.failed",false)?1:0);
            }
        };
    }
    private static void Log(string message,string stack,LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) results.Add("FAIL runtime: "+message+"\n"+stack); }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying || !SessionState.GetBool("archive.mechanics.test",false) || EditorApplication.timeSinceStartup<deadline) return;
        try
        {
            var tools=UnityEngine.Object.FindAnyObjectByType<TestingToolsController>();
            var camera=UnityEngine.Object.FindAnyObjectByType<EvidenceCameraController>();
            Camera eyes=Camera.main;
            if(phase==0)
            {
                Check(tools!=null && camera!=null,"laboratory installs player, tools and photographic camera");
                Check(eyes.GetComponentInParent<FirstPersonHeadBob>()!=null && eyes.transform.parent.name=="Head bob pivot","independent head bob pivot installed");
                Check(OfficeDissolveTransition.ActiveMemory!=null && !OfficeDissolveTransition.ActiveMemory.IsReconstructing,"memory rebuild completed");
                var controller=tools.GetComponent<CharacterController>(); controller.enabled=false; tools.transform.position=new Vector3(0,.2f,-5); controller.enabled=true; Physics.SyncTransforms();
                eyes.transform.rotation=Quaternion.LookRotation(new Vector3(0,0,-2)-eyes.transform.position);
                tools.BeginPlacement(); Call(tools,"UpdatePlacement");
                Check((bool)Field(tools,"valid"),"tripod can be placed on supported free floor; eye="+eyes.transform.position);
                Capture(eyes,"tripod-hologram.png");
                typeof(TestingToolsController).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(tools,75f); Call(tools,"UpdatePlacement"); tools.ConfirmPlacement();
                var tripod=(GameObject)Field(tools,"tripod"); Check(tripod!=null && Mathf.Abs(Mathf.DeltaAngle(tripod.transform.eulerAngles.y,75))<.1f,"tripod keeps chosen rotation");
                Vector3 position=tripod.transform.position; tools.BeginPlacement(); tools.CancelPlacement();
                Check(tripod.activeSelf && tripod.transform.position==position,"cancel moving restores original tripod");
                var markers=UnityEngine.Object.FindAnyObjectByType<EvidenceMarkerPlacement>();
                foreach(var existing in UnityEngine.Object.FindObjectsByType<EvidenceMarker>(FindObjectsInactive.Exclude)) markers.Remove(existing);
                for(int i=0;i<3;i++)
                {
                    eyes.transform.rotation=Quaternion.LookRotation(new Vector3(1.6f+i*.4f,0,-2)-eyes.transform.position);
                    markers.Begin(); Call(markers,"UpdatePreview"); Check(markers.Confirm(),"place evidence marker "+(i+1));
                }
                var numbered=UnityEngine.Object.FindObjectsByType<EvidenceMarker>(FindObjectsInactive.Exclude).OrderBy(marker=>marker.Number).ToArray();
                var third=numbered[2]; markers.Remove(numbered[1]);
                Check(markers.Count==2 && third.Number==2,"collecting marker 2 renumbers former marker 3 to 2");
                Capture(eyes,"evidence-markers.png");
                Check(EvidenceMarkerPlacement.CaptureRecords().Count(record=>record.scene=="MechanicsLab")==2,"placed marker records are available to save system");
                Check(tripod.GetComponentsInChildren<Camera>(true).Length==0 && tripod.GetComponentsInChildren<Light>(true).Length==0,"new tripod is empty");
                foreach(var tool in new[]{EvidenceCameraController.ToolSlot.Brush,EvidenceCameraController.ToolSlot.Flashlight,EvidenceCameraController.ToolSlot.UvLight})
                {
                    Check(camera.SelectTool(tool),"wheel selects "+tool);
                    GameObject held=camera.GetToolObject(tool);
                    Check(camera.MountEquipped(tools.TripodMount) && camera.MountedObject==held,"mount transfers same object: "+tool);
                    Check(!camera.SelectTool(tool) && camera.EquippedTool==EvidenceCameraController.ToolSlot.Hand,"mounted tool unavailable in hand: "+tool);
                    Check(camera.RetrieveMounted() && camera.MountedObject==null && camera.EquippedTool==tool,"retrieving restores tool: "+tool);
                }
                var slot=typeof(EvidenceCameraController).GetNestedType("ToolSlot",BindingFlags.Public);
                typeof(EvidenceCameraController).GetField("hoveredTool",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(camera,Enum.Parse(slot,"Camera"));
                Call(camera,"EquipHoveredTool",true); Call(camera,"SetCameraMode",true);
                eyes.transform.rotation=Quaternion.LookRotation(new Vector3(-3,1,0)-eyes.transform.position);
                phase=1; deadline=EditorApplication.timeSinceStartup+2;
            }
            else if(phase==1)
            {
                var physical=UnityEngine.Object.FindAnyObjectByType<PhysicalEvidenceCamera>();
                Check(physical!=null && physical.LensCamera.targetTexture!=null && physical.LensCamera.enabled,"held camera renders live image on LCD (physical="+(physical!=null)+", mode="+EvidenceCameraController.IsAnyCameraModeActive+", paused="+GlobalPauseMenu.IsPaused+", notebook="+EvidenceNotebookUI.IsAnyNotebookOpen+")");
                Check(physical.LensCamera.transform.parent==physical.transform && Vector3.Distance(physical.LensCamera.transform.position,eyes.transform.position)>.2f,"handheld feed originates at physical lens");
                float eyeFov=eyes.fieldOfView; physical.LensCamera.fieldOfView=25;
                Check(eyes.fieldOfView==eyeFov,"lens zoom does not change player view");
                Capture(eyes,"camera-in-hands.png"); Capture(physical.LensCamera,"camera-lcd.png");
                camera.CaptureFrom(physical.LensCamera); Check(PhotoAlbum.Photos.Count>0,"photograph reaches discard gallery");
                Call(camera,"SetCameraMode",false);
                Call(physical,"LateUpdate");
                Check(physical.DisplayIsBlack && !physical.LensCamera.enabled,"LCD turns black when camera mode ends");
                Check(camera.MountEquipped(tools.TripodMount),"camera mounts on empty tripod");
                Call(physical,"LateUpdate"); Vector3 lensPosition=physical.LensCamera.transform.position;
                eyes.transform.position+=Vector3.right; Call(physical,"LateUpdate");
                Check(physical.LensCamera.transform.position==lensPosition && Vector3.Distance(lensPosition,eyes.transform.position)>.1f,"mounted lens stays on camera when player moves");
                Check(!camera.SelectTool(EvidenceCameraController.ToolSlot.Camera),"mounted camera cannot be duplicated in the wheel");
                Check(camera.RetrieveMounted(),"camera can be retrieved and equipped again");
                var pause=UnityEngine.Object.FindAnyObjectByType<GlobalPauseMenu>(); pause.Pause(); CaptureCanvas(pause.GetComponentInChildren<Canvas>(true),"pause-ui.png"); pause.Resume();
                var notebook=UnityEngine.Object.FindAnyObjectByType<EvidenceNotebookUI>(); Call(notebook,"SetVisible",true); CaptureCanvas(((CanvasGroup)Field(notebook,"rootGroup")).GetComponent<Canvas>(),"notebook-ui.png"); Call(notebook,"SetVisible",false);
                camera.StowForExternalTool();
                var hints=UnityEngine.Object.FindAnyObjectByType<ContextControlHints>();
                Check((string)Call(hints,"GetText")=="","empty hand has no persistent controls panel");
                camera.SelectTool(EvidenceCameraController.ToolSlot.Tags);
                if(PlayerAssistanceSettings.ShouldShowHelp) Check(((string)Call(hints,"GetText")).Contains("MARCADORES"),"equipping markers shows their specific controls");
                phase=2; deadline=EditorApplication.timeSinceStartup+1.2;
                EditorApplication.update+=Reveal;
            }
            else if(phase==2)
            {
                EditorApplication.update-=Reveal;
                var trace=GameObject.Find("Reagent sample"); Check(trace.GetComponent<Renderer>().enabled,"reagent exposure reveals hidden trace");
                var wave=OfficeDissolveTransition.ActiveMemory;
                Vector3 center=wave.MemoryCenter;
                eyes.transform.position=center+new Vector3(16,1.8f,0); eyes.transform.rotation=Quaternion.LookRotation(Vector3.right);
                Capture(eyes,"boundary-editor.png");
                Check(wave.MemoryCenter==center,"walking near boundary never moves memory origin");
                Check(MemoryLoadingScreen.Load("F1-House"),"asynchronous memory loading starts");
                phase=3; deadline=EditorApplication.timeSinceStartup+.1;
            }
            else if(phase==3)
            {
                if(!MemoryLoadingScreen.IsBusy && SceneRebuildTransition.IsBusy && OfficeDissolveTransition.ActiveMemory!=null && OfficeDissolveTransition.ActiveMemory.IsReconstructing && !capturedRebuild)
                {
                    var rebuild=UnityEngine.Object.FindAnyObjectByType<SceneRebuildTransition>();
                    var overlay=(CanvasGroup)Field(rebuild,"fadeFromBlack");
                    Check(overlay==null || overlay.alpha<.01f,"house wave runs with loading and scene blackouts removed");
                    Capture(eyes,"house-reconstruction-start.png"); capturedRebuild=true;
                }
                if(MemoryLoadingScreen.IsBusy || SceneRebuildTransition.IsBusy) return;
                Check(capturedRebuild,"house reconstruction was visible before completion");
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="F1-House","loading screen completes before entering house");
                Check(MemoryLoadingScreen.BlackBeforeLoadSeconds>=.4f,"assets wait until blackout has been displayed");
                Check(OfficeDissolveTransition.ActiveMemory.DissolveDuration>=4.5f,"reconstruction wave uses slower playback");
                Check(OfficeDissolveTransition.ActiveMemory.MaximumMemoryRadius==60,"house uses expanded radius");
                CheckScenePlacement(tools,eyes);
                var sample=UnityEngine.Object.FindAnyObjectByType<EvidenceTarget>();
                Check(sample.GetComponentInChildren<Renderer>().sharedMaterial.shader.name!="ArchiveNull/RoomDissolve","authored materials restored after reconstruction");
                var wave=OfficeDissolveTransition.ActiveMemory; Vector3 exitOrigin=Camera.main.transform.position;
                wave.StartCoroutine(ReturnToOffice(wave));
                Check((Vector3)Field(wave,"reconstructionOrigin")==exitOrigin,"exit wave retracts toward player position");
                phase=4; deadline=EditorApplication.timeSinceStartup+2;
            }
            else if(phase==4)
            {
                if(MemoryLoadingScreen.IsBusy || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MainMenu") return;
                Check(OfficeDissolveTransition.ActiveMemory==null,"real office has no memory boundary or reconstruction wave");
                Capture(Camera.main,"office-return.png");
                var visor=UnityEngine.Object.FindAnyObjectByType<VRHeadsetArchiveStarter>();
                Check(visor!=null,"office installs headset session UI");
                var visorUi=(GameObject)Field(visor,"sessionUi"); visorUi.SetActive(true); visorUi.GetComponent<CanvasGroup>().alpha=1;
                CaptureCanvas(visorUi.GetComponent<Canvas>(),"headset-session.png"); visorUi.SetActive(false);
                RuntimeConfirmationDialog.Show("SALIR DE LA MEMORIA","Las evidencias, fotografías y notas se conservan.","VOLVER A LA OFICINA","CONTINUAR",()=>{});
                CaptureCanvas(GameObject.Find("ConfirmationDialog").GetComponent<Canvas>(),"session-confirmation.png"); UnityEngine.Object.Destroy(GameObject.Find("ConfirmationDialog"));
                MemoryLoadingScreen.Load("F1-Office"); phase=5;
            }
            else if(phase==5)
            {
                if(MemoryLoadingScreen.IsBusy || SceneRebuildTransition.IsBusy) return;
                Check(OfficeAssetCache.RetainedCount>0,"office dependencies retained for return journey");
                Check(OfficeDissolveTransition.ActiveMemory.MaximumMemoryRadius==40,"contractor memory retains its fixed boundary");
                CheckScenePlacement(tools,eyes);
                MemoryLoadingScreen.Load("MainMenu",OfficeDissolveTransition.ActiveMemory); phase=6;
            }
            else if(phase==6)
            {
                if(MemoryLoadingScreen.IsBusy) return;
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","complete office-memory-office round trip");
                results.Add("MEASURE warm office assets: "+MemoryLoadingScreen.LastAssetLoadSeconds.ToString("F2")+" seconds");
                Finish();
            }
        }
        catch(Exception exception) { results.Add("FAIL "+exception); Finish(); }
    }
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static void CheckScenePlacement(TestingToolsController tools,Camera eyes)
    {
        Quaternion original=eyes.transform.rotation;
        float scale=(float)Field(tools,"sceneScale"); bool valid=false;
        tools.BeginPlacement();
        for(int radius=1;radius<=3 && !valid;radius++) for(int angle=0;angle<360 && !valid;angle+=30)
        {
            Vector3 floor=tools.transform.position+Quaternion.Euler(0,angle,0)*Vector3.forward*(radius+.5f)*scale;
            eyes.transform.rotation=Quaternion.LookRotation(floor-eyes.transform.position);
            Call(tools,"UpdatePlacement"); valid=(bool)Field(tools,"valid");
        }
        if(valid) tools.ConfirmPlacement(); else tools.CancelPlacement();
        eyes.transform.rotation=original;
        Check(valid,"scaled tripod finds supported free floor in "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" (scale "+scale.ToString("F2")+")");
    }
    private static void Reveal() { var sample=GameObject.Find("Reagent sample"); if(sample!=null) UnityEngine.Object.FindAnyObjectByType<TestingToolsController>().ApplyReagent(new Ray(sample.transform.position+Vector3.up,Vector3.down)); }
    private static IEnumerator ReturnToOffice(OfficeDissolveTransition wave) { yield return wave.PlayDissolve(); MemoryLoadingScreen.Load("MainMenu"); }
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); results.Add("PASS "+message); }
    private static void Finish()
    {
        SessionState.SetBool("archive.mechanics.failed",results.Any(line=>line.StartsWith("FAIL")));
        Directory.CreateDirectory("Artifacts/EditorValidation"); File.WriteAllLines("Artifacts/EditorValidation/editor-mechanics.txt",results);
        Application.logMessageReceived-=Log; SessionState.SetBool("archive.mechanics.test",false); SessionState.SetBool("archive.mechanics.restore",true); EditorApplication.ExitPlaymode();
        EditorApplication.update-=Reveal;
        if(Environment.GetCommandLineArgs().Contains("-archive-editor-test"))
        {
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("archive.mechanics.oldStart",""));
            SessionState.SetBool("archive.mechanics.restore",false);
            EditorApplication.Exit(SessionState.GetBool("archive.mechanics.failed",false)?1:0);
        }
    }
    private static void CaptureCanvas(Canvas canvas,string file)
    {
        var host=new GameObject("UI verification camera",typeof(Camera)); var camera=host.GetComponent<Camera>(); camera.enabled=false; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.nearClipPlane=.1f; camera.farClipPlane=10; camera.transform.position=Vector3.one*10000; host.AddComponent<UniversalAdditionalCameraData>();
        RenderMode mode=canvas.renderMode; Camera previous=canvas.worldCamera; float distance=canvas.planeDistance;
        try { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; Canvas.ForceUpdateCanvases(); Capture(camera,file); }
        finally { canvas.renderMode=mode; canvas.worldCamera=previous; canvas.planeDistance=distance; UnityEngine.Object.Destroy(host); }
    }
    private static void Capture(Camera camera,string file)
    {
        Directory.CreateDirectory("Artifacts/EditorValidation");
        var target=RenderTexture.GetTemporary(1280,720,24); var previous=RenderTexture.active;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target; texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply(); File.WriteAllBytes("Artifacts/EditorValidation/"+file,texture.EncodeToPNG());
        }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture); }
    }
}
#endif
