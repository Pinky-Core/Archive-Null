using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArchiveNull.Narrative;

namespace ArchiveNull.Evidence
{
    public sealed class CaseScenePolish : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
            Loaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name is not ("F1-House" or "F1-Office")) return;
            if(FindAnyObjectByType<CaseScenePolish>()==null) new GameObject("Case scene details").AddComponent<CaseScenePolish>();
        }
        private IEnumerator Start()
        {
            yield return null;
            foreach(var target in FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include))
            {
                string id=target.EvidenceData.evidenceId;
                if(id is "f1_house_huella-zapato" or "f1_office_Huellas")
                    foreach(var trace in target.GetComponentsInChildren<UvRevealTarget>(true)) trace.MakeVisible();
                if(id=="f1_house_huella-dactilar")
                    foreach(var trace in target.GetComponentsInChildren<UvRevealTarget>(true)) trace.RevealMethod=UvRevealTarget.Method.Powder;
            }
            if(gameObject.scene.name=="F1-House") PlacePinReminder();
        }
        private void PlacePinReminder()
        {
            EvidenceTarget note=null,phone=null;
            foreach(var target in FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include))
            {
                if(target.EvidenceData.evidenceId=="f1_house_cube") note=target;
                if(target.EvidenceData.evidenceId=="f1_house_phone") phone=target;
            }
            if(note==null || phone==null || GameObject.Find("Phone PIN reminder")!=null) return;
            float scale=Camera.main!=null ? Mathf.Max(.5f,Camera.main.transform.lossyScale.y):1;
            var noteRenderers=note.GetComponentsInChildren<Renderer>(true);
            Bounds bounds=noteRenderers.Length>0 ? noteRenderers[0].bounds : new Bounds(note.transform.position,Vector3.one*.1f);
            foreach(var renderer in noteRenderers) renderer.enabled=false;
            Transform sheet=CreatePaper("Julián's handwritten note",bounds.center,new Vector2(.18f,.23f)*scale,Color.white);
            sheet.SetParent(note.transform,true);
            AddText(sheet,"Revisar firma de mamá.\nCopia de Nicolás.\n\nObra Salas: mismo número,\nmontos distintos.\n\nLlamar al Dr. Funes.\nAvisar a Sofía.",Color.black);
            Vector3 pinPosition=phone.transform.position+Vector3.right*.22f*scale;
            if(Physics.Raycast(pinPosition+Vector3.up*.2f*scale,Vector3.down,out var floor,.8f*scale,~(1<<31),QueryTriggerInteraction.Ignore)) pinPosition=floor.point+Vector3.up*.006f*scale;
            Transform reminder=CreatePaper("Phone PIN reminder",pinPosition,new Vector2(.10f,.10f)*scale,new Color(1,.84f,.24f));
            reminder.gameObject.tag="Inspectable";
            reminder.gameObject.AddComponent<InspectableNarration>().Configure("La pegatina junto al teléfono dice: PIN 2530.","The sticker beside the phone reads: PIN 2530.");
            AddText(reminder,"TELÉFONO\nPIN 2530",Color.black);
        }
        private Transform CreatePaper(string name,Vector3 position,Vector2 size,Color color)
        {
            var paper=GameObject.CreatePrimitive(PrimitiveType.Cube);paper.name=name;paper.transform.position=position;
            paper.transform.localScale=new Vector3(size.x,.002f,size.y);
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=color;paper.GetComponent<Renderer>().sharedMaterial=material;
            ownedMaterials.Add(material);return paper.transform;
        }
        private readonly System.Collections.Generic.List<Material> ownedMaterials=new();
        private static void AddText(Transform paper,string value,Color color)
        {
            var obj=new GameObject("Handwriting");obj.transform.SetParent(paper,false);obj.transform.localPosition=new Vector3(0,.55f,0);obj.transform.localRotation=Quaternion.Euler(90,0,0);
            var text=obj.AddComponent<TextMeshPro>();text.text=value;text.color=color;text.fontSize=.45f;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.Normal;
            text.rectTransform.sizeDelta=new Vector2(.9f,.9f);
        }
        private void OnDestroy() {foreach(var material in ownedMaterials) if(material!=null) Destroy(material);}
    }
}
