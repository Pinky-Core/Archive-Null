#if UNITY_EDITOR
using System.IO;
using System.Linq;
using ArchiveNull.Evidence;
using ArchiveNull.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MechanicsLabEditor
{
    public const string LabPath = "Assets/ArchiveNull/Scenes/MechanicsLab.unity";
    [MenuItem("Archive NULL/Create mechanics laboratory")]
    public static void CreateLab()
    {
        if(File.Exists(LabPath)) { NormalizeLabRig(); return; }
        Directory.CreateDirectory("Assets/ArchiveNull/Materials");
        AssetDatabase.Refresh();
        Scene previous=SceneManager.GetActiveScene();
        Scene source=EditorSceneManager.OpenPreviewScene("Assets/ArchiveNull/Scenes/F1-House.unity");
        Scene lab=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(lab);
            var sourcePlayer=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FirstPersonMovement>(true)).First(p=>p.GetComponent<Rigidbody>()!=null);
            var player=Object.Instantiate(sourcePlayer.gameObject); player.name="Player"; player.transform.position=new Vector3(0,.2f,-5); player.transform.rotation=Quaternion.identity;
            NormalizePlayer(player);
            Material material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=new Color(.22f,.26f,.28f);
            const string materialPath="Assets/ArchiveNull/Materials/MechanicsLab.mat";
            AssetDatabase.CreateAsset(material,materialPath);
            Make("Floor",new Vector3(0,-.2f,0),new Vector3(60,.4f,60),material);
            Make("Occlusion wall",new Vector3(0,1.5f,8),new Vector3(8,3,.2f),material);
            Make("Worktable",new Vector3(-3,.8f,0),new Vector3(2,.15f,1.2f),material);
            Make("Obstacle for invalid placement",new Vector3(3,.5f,1),Vector3.one,material);
            var target=Make("Sample evidence",new Vector3(-3,1,0),new Vector3(.35f,.2f,.25f),material); target.tag="Inspectable";
            var evidence=target.AddComponent<EvidenceTarget>(); var data=new SerializedObject(evidence);
            data.FindProperty("evidenceId").stringValue="lab_sample"; data.FindProperty("evidenceName").stringValue="Muestra de laboratorio"; data.FindProperty("description").stringValue="Fotografía, inspecciona y anota esta muestra de prueba."; data.ApplyModifiedPropertiesWithoutUndo();
            var trace=Make("Reagent sample",new Vector3(-2.55f,.89f,0),new Vector3(.22f,.01f,.22f),material);
            var traceMaterial=new Material(Resources.Load<Shader>("ArchiveTrace")); traceMaterial.color=Color.cyan;
            AssetDatabase.CreateAsset(traceMaterial,"Assets/ArchiveNull/Materials/MechanicsLabTrace.mat"); trace.GetComponent<Renderer>().sharedMaterial=traceMaterial; trace.AddComponent<UvRevealTarget>();
            var sun=new GameObject("Laboratory light",typeof(Light)); sun.transform.rotation=Quaternion.Euler(45,-30,0); var light=sun.GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.3f,.35f,.4f);
            var transition=new GameObject("Memory reconstruction"); var wave=transition.AddComponent<OfficeDissolveTransition>(); var waveData=new SerializedObject(wave);
            waveData.FindProperty("limitMemoryRadius").boolValue=true; waveData.FindProperty("maximumMemoryRadius").floatValue=18;
            waveData.FindProperty("fallbackDissolveShader").objectReferenceValue=Shader.Find("ArchiveNull/RoomDissolve"); waveData.FindProperty("rebuildOnMainMenuReturn").boolValue=false; waveData.ApplyModifiedPropertiesWithoutUndo();
            var rebuild=transition.AddComponent<SceneRebuildTransition>(); var rebuildData=new SerializedObject(rebuild); rebuildData.FindProperty("officeDissolveTransition").objectReferenceValue=wave; rebuildData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(lab,LabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(source); EditorSceneManager.CloseScene(lab,true); if(previous.IsValid()) SceneManager.SetActiveScene(previous); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp/ArchiveValidation"); File.WriteAllText("Temp/ArchiveValidation/lab.txt","Created " + LabPath);
    }
    private static GameObject Make(string name,Vector3 position,Vector3 size,Material material)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.position=position; obj.transform.localScale=size; obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
    }
    private static void NormalizePlayer(GameObject player)
    {
        player.transform.localScale=Vector3.one;
        Camera camera=player.GetComponentInChildren<Camera>(true);
        camera.transform.position=player.transform.position+Vector3.up*1.65f;
        camera.fieldOfView=65;
    }
    private static void NormalizeLabRig()
    {
        Scene previous=SceneManager.GetActiveScene(); Scene lab=EditorSceneManager.OpenScene(LabPath,OpenSceneMode.Additive);
        try { var player=lab.GetRootGameObjects().First(r=>r.GetComponent<FirstPersonMovement>()!=null); NormalizePlayer(player); EditorSceneManager.SaveScene(lab); }
        finally { EditorSceneManager.CloseScene(lab,true); if(previous.IsValid()) SceneManager.SetActiveScene(previous); }
    }
    [MenuItem("Archive NULL/Test mechanics in editor")]
    public static void TestInEditor()
    {
        OfficeAssetManifestEditor.Ensure();
        CreateLab();
        SessionState.SetBool("archive.mechanics.test",true);
        SessionState.SetString("archive.mechanics.oldStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(LabPath);
        EditorApplication.EnterPlaymode();
    }
}
#endif
