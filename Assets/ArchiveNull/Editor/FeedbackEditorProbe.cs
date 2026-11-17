#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArchiveNull.Evidence;
using ArchiveNull.Narrative;
using ArchiveNull.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FeedbackEditorProbe
{
    public const string Flag="archive.feedback.test";
    private static readonly List<string> results=new();
    private static double started;
    static FeedbackEditorProbe()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag,false))
            {
                results.Clear();started=EditorApplication.timeSinceStartup;Application.runInBackground=true;Application.logMessageReceived+=Log;
                var host=new GameObject("Feedback regression");Object.DontDestroyOnLoad(host);host.AddComponent<FeedbackTestRunner>().StartCoroutine(RunSafe());
            }
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("archive.feedback.restore",false))
            {
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("archive.feedback.oldStart",""));
                SessionState.SetBool("archive.feedback.restore",false);
            }
        };
    }
    [MenuItem("Archive NULL/Test feedback changes in editor")]
    public static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Flag,true);SessionState.SetBool("archive.mechanics.test",true);
        SessionState.SetString("archive.feedback.run",Guid.NewGuid().ToString("N"));
        SessionState.SetString("archive.feedback.oldStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/ArchiveNull/Scenes/MainMenu.unity");EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        const string request=".utmp/feedback-test.request";
        if(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(request)) {File.Delete(request);Start();}
        if(EditorApplication.isPlaying && SessionState.GetBool(Flag,false) && EditorApplication.timeSinceStartup-started>360) {results.Add("FAIL timed out");Finish();}
    }
    private static void Log(string value,string stack,LogType type) {if(type is LogType.Error or LogType.Exception or LogType.Assert) results.Add("FAIL runtime: "+value+"\n"+stack);}
    private static IEnumerator RunSafe()
    {
        IEnumerator run=Run();
        while(true)
        {
            object next;
            try {if(!run.MoveNext()) break;next=run.Current;}
            catch(Exception error) {results.Add("FAIL "+error);break;}
            yield return next;
        }
        Finish();
    }
    private static IEnumerator Run()
    {
        Directory.CreateDirectory("Artifacts/FeedbackValidation");Directory.CreateDirectory(".utmp/FeedbackFrames");
        yield return new WaitForSecondsRealtime(6);
        PhotoAlbum.Restore(Array.Empty<EvidenceData>());
        Check(SceneManager.GetActiveScene().name=="MainMenu","starts in real office");
        var focus=Object.FindAnyObjectByType<CRTMenuCameraFocus>();
        Check(focus.IsInStandPose && Mathf.Abs(Camera.main.transform.position.y-focus.StandingEyeHeight)<.025f,"arrival and standing height include office eye lift");
        Check(Object.FindAnyObjectByType<GlobalPauseMenu>()==null,"office has no pause overlay");
        PlayerPrefs.SetInt("archive.office.casefile.discovered",1);PlayerPrefs.SetInt(OfficeSpeakerTutorial.CompletedPref,1);
        focus.FocusComputer();yield return new WaitForSecondsRealtime(2);
        var desktop=Object.FindAnyObjectByType<ModernOfficeDesktop>();Call(desktop,"OpenStart");
        yield return new WaitForSecondsRealtime(.4f);
        Check(desktop.MonitorCanvas.GetComponentsInChildren<Button>().Any(b=>b.name=="Cerrar sesión y salir"),"Archive OS offers confirmed sign out");
        for(int frame=0;frame<16;frame++) {Capture("desktop",frame);yield return new WaitForSecondsRealtime(.125f);}
        Capture("desktop");yield return new WaitForEndOfFrame();focus.MoveToStandPose();yield return new WaitForSecondsRealtime(.8f);
        var notebook=Object.FindAnyObjectByType<EvidenceNotebookUI>();Call(notebook,"SetVisible",true);
        Click(FindButton(notebook,"ReconstructionTab"));
        Check(((CanvasGroup)Field(notebook,"reconstructionGroup")).gameObject.activeSelf,"raycast click opens reconstruction tab");
        Capture("reconstruction");yield return new WaitForEndOfFrame();
        var fixture=new Texture2D(4,4);fixture.SetPixels(Enumerable.Repeat(Color.gray,16).ToArray());fixture.Apply();PhotoAlbum.Add(Sprite.Create(fixture,new Rect(0,0,4,4),Vector2.one*.5f),"MainMenu","Foto de prueba");
        Click(FindButton(notebook,"PhotosTab"));yield return new WaitForEndOfFrame();yield return new WaitForSecondsRealtime(.2f);
        Check(PhotoAlbum.Photos.Count==1,"photo fixture starts with one photograph");
        var delete=FindButton(notebook,"DeletePhoto");
        results.Add("STATE delete active="+delete.gameObject.activeInHierarchy+", interactable="+delete.IsInteractable()+", raycast="+delete.GetComponent<Image>().raycastTarget+", depth="+delete.GetComponent<Image>().depth+", cull="+delete.GetComponent<Image>().canvasRenderer.cull+", tab="+Field(notebook,"activeTab")+", groups="+string.Join(";",delete.GetComponentsInParent<CanvasGroup>(true).Select(g=>g.name+":"+g.blocksRaycasts+":"+g.alpha)));
        Click(delete);
        Check(RuntimeConfirmationDialog.IsOpen,"raycast click on delete photo opens confirmation");
        yield return null;
        var confirm=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="ELIMINAR");Click(confirm);
        Check(PhotoAlbum.Photos.Count==0,"confirmation deletes the selected photograph");Call(notebook,"SetVisible",false);
        MemoryLoadingScreen.Load("F1-House");
        int waveFrame=0;
        while(MemoryLoadingScreen.IsBusy || SceneRebuildTransition.IsBusy || OfficeDissolveTransition.ActiveMemory==null || OfficeDissolveTransition.ActiveMemory.IsReconstructing)
        {
            if(!MemoryLoadingScreen.IsBusy && OfficeDissolveTransition.ActiveMemory!=null && waveFrame<64) Capture("reconstruction-wave",waveFrame++);
            yield return new WaitForSecondsRealtime(.125f);
        }
        yield return new WaitForSecondsRealtime(2);
        Check(GameObject.Find("Phone PIN reminder")!=null,"PIN sticker is separate from Julian's note");
        Check(!CaseEvidenceCatalog.Find("f1_house_cube").Description.Contains("2530"),"evidence 2 contains investigation notes rather than phone PIN");
        var foot=Object.FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include).First(t=>t.EvidenceData.evidenceId=="f1_house_huella-zapato");
        Check(foot.GetComponentsInChildren<UvRevealTarget>(true).All(t=>t.AlwaysVisible),"door footprint needs no revealing tool");
        Check(EvidenceTextLocalization.ReadableObjectName("Book-1")=="Libro" && EvidenceTextLocalization.ReadableObjectName("Lens")=="Lente","technical object names become readable Spanish names");
        results.Add("STATE house camera "+Camera.main.transform.position+" rotation "+Camera.main.transform.eulerAngles+" mask "+Camera.main.cullingMask+" far "+Camera.main.farClipPlane+" player "+Object.FindAnyObjectByType<FirstPersonMovement>().transform.position);
        Capture("house");yield return new WaitForEndOfFrame();
        var movement=Object.FindAnyObjectByType<FirstPersonMovement>();var crouch=movement.GetComponentInChildren<Crouch>();var controller=movement.GetComponentInParent<CharacterController>();
        Check(crouch!=null && crouch.enabled,"active player has crouch component");
        Vector3 playerPosition=movement.transform.position;movement.enabled=false;controller.enabled=false;movement.transform.position=new Vector3(100,80,100);controller.enabled=true;
        float standing=controller.height;float head=Camera.main.transform.position.y;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(GlobalInputBindings.GetKey(GameInputAction.Crouch)));yield return new WaitForSecondsRealtime(.5f);
        Check(crouch.IsCrouched && controller.height<standing*.7f,"real crouch input reduces active character capsule");
        Check(Camera.main.transform.position.y<head-.1f,"crouching lowers viewpoint smoothly");
        var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Crouch ceiling regression";ceiling.transform.position=controller.transform.TransformPoint(new Vector3(0,standing*.92f,0));ceiling.transform.localScale=new Vector3(3,.1f,3);Physics.SyncTransforms();
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return new WaitForSecondsRealtime(.3f);
        Check(crouch.IsCrouched,"ceiling prevents standing into geometry");Object.Destroy(ceiling);yield return new WaitForSecondsRealtime(.5f);
        Check(!crouch.IsCrouched && Mathf.Abs(controller.height-standing)<.01f,"removing ceiling restores standing capsule");
        controller.enabled=false;movement.transform.position=playerPosition;controller.enabled=true;movement.enabled=true;
        var latent=new GameObject("Latent fingerprint test");var trace=latent.AddComponent<UvRevealTarget>();trace.RevealMethod=UvRevealTarget.Method.Powder;
        for(int i=0;i<12;i++) {trace.ReceiveUvIllumination(1);yield return new WaitForSecondsRealtime(.1f);}
        Check(!trace.IsFullyRevealed,"UV does not reveal a powder-only fingerprint");
        for(int i=0;i<100;i++) {trace.ReceivePowder(1);yield return null;}
        Check(trace.IsFullyRevealed,"powder permanently reveals a latent fingerprint");Object.Destroy(latent);
        var inspect=Object.FindObjectsByType<InspectObject>().First(controller=>controller.enabled);var sample=GameObject.Find("Phone PIN reminder");
        Call(inspect,"TryPickObject",sample);yield return new WaitForSecondsRealtime(.4f);
        Check(sample.layer==30 && inspect.GetComponent<InspectionForeground>().IsActive,"inspection uses a separate foreground camera and layer");
        Capture("pin-inspection");yield return new WaitForEndOfFrame();
        Call(inspect,"RestoreObject");Check(sample.layer==0 && !inspect.GetComponent<InspectionForeground>().IsActive,"returning object restores its original layer");
        var phone=Object.FindAnyObjectByType<PhoneEvidenceReader>();phone.Collect();Object.FindAnyObjectByType<EvidenceCameraController>().SelectTool(EvidenceCameraController.ToolSlot.InventoryItem);yield return new WaitForSecondsRealtime(.6f);Call(phone,"Unlock");
        var screenType=typeof(PhoneEvidenceReader).GetNestedType("PhoneScreen",BindingFlags.NonPublic);Call(phone,"OpenScreen",Enum.Parse(screenType,"Chat"));yield return new WaitForSecondsRealtime(.3f);
        var voiceButton=Object.FindObjectsByType<VoiceMessageButton>(FindObjectsInactive.Include).FirstOrDefault(b=>b.gameObject.activeInHierarchy);
        Check(voiceButton!=null,"phone chat presents a playable voice message");
        voiceButton.GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        Check(CaseVoicePlayer.IsPlaying(voiceButton.Resource),"voice message plays a real imported Spanish clip");Capture("phone-voice");yield return new WaitForEndOfFrame();Object.FindAnyObjectByType<EvidenceCameraController>().SelectTool(EvidenceCameraController.ToolSlot.Hand);CaseVoicePlayer.Stop();
        var echo=new GameObject("Echo regression").AddComponent<EchoMemory>();echo.Play(Camera.main.transform.position+Camera.main.transform.forward,"echo_cup","El té estaba caliente. Escuché pasos en el lateral.");
        yield return new WaitForSecondsRealtime(.4f);Check(EchoMemory.IsPlaying && GameObject.Find("Echo third-person camera")!=null,"echo opens third-person recollection with shadow figures");
        Check(CaseVoicePlayer.IsPlaying("echo_cup"),"echo plays narrated recollection");
        for(int frame=0;frame<48;frame++) {Capture("echo",frame);yield return new WaitForSecondsRealtime(.125f);}
        Capture("echo");yield return new WaitForEndOfFrame();Object.Destroy(echo.gameObject);yield return null;Check(!EchoMemory.IsPlaying,"closing echo restores input state");
        MemoryLoadingScreen.Load("F1-Office");while(MemoryLoadingScreen.IsBusy || SceneManager.GetActiveScene().name!="F1-Office") yield return null;
        yield return new WaitForSecondsRealtime(6);
        var terminal=Object.FindAnyObjectByType<SalasComputer>();Check(terminal!=null && terminal.HasMonitor,"Salas computer locates a physical monitor in the scene");terminal.Open();yield return new WaitForSecondsRealtime(.3f);
        Check(SalasComputer.IsOpen && !Object.FindAnyObjectByType<FirstPersonMovement>().enabled,"Salas documents open and capture player input");Capture("salas-computer");
        var register=terminal.GetComponentsInChildren<Button>().First(b=>b.name=="Registrar comprobantes");Click(register);
        Check(EvidenceInventory.Instance.HasEvidence("f1_ofice_factura1"),"Salas computer adds receipts to evidence inventory");terminal.Close();yield return null;
        Check(!SalasComputer.IsOpen && Object.FindAnyObjectByType<FirstPersonMovement>().enabled,"closing Salas computer restores movement");
        var pause=Object.FindAnyObjectByType<GlobalPauseMenu>();Call(pause,"Pause");Call(pause,"ShowOptions");yield return new WaitForSecondsRealtime(.3f);Capture("pause-options");yield return new WaitForEndOfFrame();pause.Resume();
        MemoryLoadingScreen.Load("MainMenu");while(MemoryLoadingScreen.IsBusy || SceneManager.GetActiveScene().name!="MainMenu") yield return null;
        yield return new WaitForSecondsRealtime(2);Check(Object.FindAnyObjectByType<CRTMenuCameraFocus>().IsInFarPose,"return journey still ends seated in office");Capture("office-return");yield return new WaitForEndOfFrame();
        var board=Object.FindAnyObjectByType<ArchiveNull.InvestigationBoard.WorldEvidenceBoardController>();
        Check(board!=null,"office retains physical evidence board");
        var template=(ArchiveNull.InvestigationBoard.WorldEvidencePhoto)Field(board,"photoPrefab");
        var photo=Object.FindObjectsByType<ArchiveNull.InvestigationBoard.WorldEvidencePhoto>().First(p=>p!=template && !string.IsNullOrEmpty(p.EvidenceId));
        Check(photo.transform.localScale.magnitude>template.transform.localScale.magnitude*1.4f,"spawned board photographs are larger than the template");
        Object.FindAnyObjectByType<CRTMenuCameraFocus>().enabled=false;Object.FindAnyObjectByType<OfficeFreeLookController>().enabled=false;
        var surface=(Transform)Field(board,"boardSurface");var collider=(Collider)Field(board,"boardCollider");
        Vector3 normal=(Vector3)Call(board,"GetBoardNormal");Camera.main.transform.position=collider.bounds.center+normal*1.8f;Camera.main.transform.LookAt(collider.bounds.center);Mouse.current.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));
        var connections=(ArchiveNull.InvestigationBoard.WorldBoardConnectionManager)Field(board,"connectionManager");connections.HandlePhotoClicked(photo);
        yield return null;Call(board,"UpdateBoardHelp");
        Check(((TMPro.TMP_Text)Field(board,"boardHelp")).text.Contains("2."),"board tutorial advances to choosing a second photograph");
        Vector3 photoPoint=photo.InteractionCollider.bounds.center;
        Camera.main.transform.position=photoPoint+normal*1.8f;Camera.main.transform.LookAt(photoPoint);Cursor.lockState=CursorLockMode.None;
        Mouse.current.WarpCursorPosition(new Vector2(Screen.width*.5f,Screen.height*.5f));yield return null;Call(board,"UpdateBoardHelp");
        Check(((RectTransform)Field(board,"photoPreview")).gameObject.activeSelf,"pointing at a board photograph opens its enlarged preview");
        Check(((Image)Field(board,"previewImage")).sprite!=null,"enlarged preview displays the photograph sprite");
        Capture("board");yield return new WaitForEndOfFrame();
    }
    private static void Check(bool ok,string value) {if(!ok) throw new InvalidOperationException(value);results.Add("PASS "+value);}
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(target,args);
    private static Button FindButton(Component root,string name)=>root.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"pointer reaches "+button.name+" (first hit: "+(hits.Count>0 ? hits[0].gameObject.name:"none")+")");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    private static void Capture(string name,int frame=-1)
    {
        string path=frame<0 ? "Artifacts/FeedbackValidation/"+name+".png":".utmp/FeedbackFrames/"+name+"-"+frame.ToString("D3")+".png";
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
    }
    private static void Finish()
    {
        if(Keyboard.current!=null) InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        Directory.CreateDirectory("Artifacts/FeedbackValidation");File.WriteAllLines("Artifacts/FeedbackValidation/results.txt",results);
        Application.logMessageReceived-=Log;SessionState.SetBool(Flag,false);SessionState.SetBool("archive.mechanics.test",false);SessionState.SetBool("archive.feedback.restore",true);EditorApplication.ExitPlaymode();
    }
}
public sealed class FeedbackTestRunner : MonoBehaviour {}
#endif
