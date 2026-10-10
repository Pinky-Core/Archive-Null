using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Slot=ArchiveNull.Evidence.EvidenceCameraController.ToolSlot;

namespace ArchiveNull.Evidence
{
    public sealed class EvidenceMarker : MonoBehaviour
    {
        public EvidenceMarkerPlacement.Record Record;
        public int Number { get; private set; }
        public void SetNumber(int number) { Number=number; foreach(TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) { text.text=number.ToString(); text.ForceMeshUpdate(); } }
    }

    [DefaultExecutionOrder(-70)]
    public sealed class EvidenceMarkerPlacement : MonoBehaviour
    {
        [Serializable] public sealed class Record { public string scene; public Vector3 position; public float yaw; }
        private static List<Record> records=new();
        public static bool IsPlacing { get; private set; }
        public static bool HandledInputThisFrame => handledFrame==Time.frameCount;
        private static int handledFrame=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() { records=new(); IsPlacing=false; handledFrame=-1; }
        public static List<Record> CaptureRecords() => new(records);
        public static void RestoreRecords(List<Record> saved) { records=saved!=null ? new(saved) : new(); }
        private readonly List<EvidenceMarker> placed=new();
        private readonly Collider[] overlaps=new Collider[16];
        private Camera view;
        private EvidenceCameraController equipment;
        private Material yellow,hologram;
        private GameObject preview;
        private bool valid;
        private float yaw,scale;
        private int startedFrame;
        public int Count => placed.Count;
        public string Controls => IsPlacing ? (valid ? "COLOCAR MARCADOR " + (Count+1) : "BUSCÁ UNA SUPERFICIE LIBRE")+"\nClic  colocar   ·   Q/R o rueda  rotar   ·   Esc  cancelar" : "MARCADORES DE EVIDENCIA\nClic  colocar   ·   E sobre marcador  recoger   ·   G  guardar";
        private bool Modal => GlobalPauseMenu.IsPaused || EvidenceNotebookUI.IsAnyNotebookOpen || RuntimeConfirmationDialog.IsOpen || InspectObject.IsAnyInspecting || PhoneEvidenceReader.IsAnyOpen || ArchiveNull.Accusation.AccusationPanel.IsAnyOpen || ArchiveNull.UI.MemoryLoadingScreen.IsBusy || ArchiveNull.UI.SceneRebuildTransition.IsBusy;
        private void Start()
        {
            view=GetComponentInChildren<Camera>(); equipment=GetComponentInChildren<EvidenceCameraController>();
            if(equipment==null) equipment=FindAnyObjectByType<EvidenceCameraController>();
            scale=TestingToolsController.PlayerScale(transform,view.transform);
            yellow=new Material(Shader.Find("Universal Render Pipeline/Lit")); yellow.color=new Color(1,.79f,.025f);
            hologram=new Material(Resources.Load<Shader>("PlacementHologram"));
            foreach(Record record in records) if(record.scene==gameObject.scene.name) Spawn(record);
            Renumber();
            GameObject held=CreateTag(yellow,Count+1); held.name="Held evidence markers"; held.transform.SetParent(view.transform,false);
            Vector3 parentScale=view.transform.lossyScale; held.transform.localScale=new Vector3(scale/parentScale.x,scale/parentScale.y,scale/parentScale.z);
            held.transform.localPosition=Vector3.Scale(new Vector3(.24f,-.28f,.46f),held.transform.localScale);
            foreach(Transform child in held.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
            equipment.RegisterTool(Slot.Tags,held);
        }
        private void Update()
        {
            if(view==null || equipment==null || Keyboard.current==null || Modal) return;
            var keys=Keyboard.current; var mouse=Mouse.current;
            if(IsPlacing)
            {
                if(keys.escapeKey.wasPressedThisFrame || keys.gKey.wasPressedThisFrame || mouse!=null && mouse.rightButton.wasPressedThisFrame) { Cancel(); return; }
                yaw+=((keys.rKey.isPressed ? 1:0)-(keys.qKey.isPressed ? 1:0))*90*Time.deltaTime;
                if(mouse!=null) yaw+=mouse.scroll.ReadValue().y/120*15;
                UpdatePreview();
                if(valid && Time.frameCount!=startedFrame && mouse!=null && mouse.leftButton.wasPressedThisFrame) Confirm();
                return;
            }
            if(EvidenceCameraController.IsAnyRadialMenuOpen || EvidenceCameraController.IsAnyCameraModeActive || TestingToolsController.IsPlacing) return;
            if(GlobalInputBindings.WasPressed(GameInputAction.Interact) && PlacementSurface.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),3*scale,transform,out var hit))
            {
                EvidenceMarker marker=hit.collider.GetComponentInParent<EvidenceMarker>();
                if(marker!=null) { handledFrame=Time.frameCount; Remove(marker); return; }
            }
            if(equipment.EquippedTool==Slot.Tags && mouse!=null && mouse.leftButton.wasPressedThisFrame) Begin();
        }
        public void Begin()
        {
            if(TestingToolsController.IsPlacing || Count>=99) return;
            equipment.StowForExternalTool(); IsPlacing=true; startedFrame=Time.frameCount; yaw=view.transform.eulerAngles.y;
            preview=CreateTag(hologram,Count+1); preview.transform.localScale=Vector3.one*scale; UpdatePreview();
        }
        private void UpdatePreview()
        {
            valid=false;
            if(!PlacementSurface.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),5*scale,transform,out var hit)) { preview.SetActive(false); return; }
            preview.SetActive(true); preview.transform.SetPositionAndRotation(hit.point+Vector3.up*.008f*scale,Quaternion.Euler(0,yaw,0));
            valid=hit.normal.y>.92f;
            var boundary=ArchiveNull.UI.OfficeDissolveTransition.ActiveMemory;
            if(boundary!=null && Vector3.Distance(boundary.ConstrainPosition(hit.point,.15f*scale),hit.point)>.01f) valid=false;
            if(!PlacementSurface.Clear(preview.transform.position+Vector3.up*.13f*scale,new Vector3(.11f,.115f,.095f)*scale,preview.transform.rotation,transform)) valid=false;
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            {
                Vector3 foot=hit.point+preview.transform.rotation*new Vector3(x*.09f,.04f,z*.07f)*scale;
                if(!PlacementSurface.Raycast(new Ray(foot,Vector3.down),.065f*scale,transform,out var support) || support.normal.y<.92f) valid=false;
            }
            hologram.color=valid ? new Color(.1f,.95f,1,.6f) : new Color(1,.3f,.15f,.65f);
        }
        public bool Confirm()
        {
            if(!valid || preview==null) return false;
            var record=new Record{scene=gameObject.scene.name,position=preview.transform.position,yaw=yaw}; records.Add(record); Spawn(record); Renumber();
            preview.SetActive(false); Destroy(preview); IsPlacing=false; equipment.SelectTool(Slot.Tags); GameSaveSystem.SaveNow(); return true;
        }
        public void Remove(EvidenceMarker marker)
        {
            if(marker==null || !placed.Remove(marker)) return;
            records.Remove(marker.Record); marker.gameObject.SetActive(false); Destroy(marker.gameObject); Renumber(); GameSaveSystem.SaveNow();
        }
        public void Cancel() { if(preview!=null) { preview.SetActive(false); Destroy(preview); } IsPlacing=false; TestingToolsController.NotifyPlacementCancelled(); equipment.SelectTool(Slot.Tags); }
        private void Spawn(Record record)
        {
            GameObject tag=CreateTag(yellow,placed.Count+1); tag.transform.SetPositionAndRotation(record.position,Quaternion.Euler(0,record.yaw,0)); tag.transform.localScale=Vector3.one*scale;
            var collider=tag.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.12f,0); collider.size=new Vector3(.22f,.24f,.18f);
            var marker=tag.AddComponent<EvidenceMarker>(); marker.Record=record; placed.Add(marker);
        }
        private void Renumber()
        {
            for(int i=0;i<placed.Count;i++) placed[i].SetNumber(i+1);
            GameObject held=equipment!=null ? equipment.GetToolObject(Slot.Tags) : null;
            if(held!=null) foreach(TMP_Text text in held.GetComponentsInChildren<TMP_Text>(true)) text.text=(Count+1).ToString();
        }
        private GameObject CreateTag(Material material,int number)
        {
            var tag=new GameObject("Evidence marker "+number);
            for(int side=-1;side<=1;side+=2)
            {
                var plate=TestingToolsController.Piece(tag.transform,new Vector3(0,.12f,side*.036f),new Vector3(.20f,.25f,.007f),material);
                plate.transform.localRotation=Quaternion.Euler(side*-17,0,0);
                var label=new GameObject("Number",typeof(TextMeshPro)); label.transform.SetParent(plate.transform,false); label.transform.localPosition=new Vector3(0,0,side*.65f);
                label.transform.localRotation=Quaternion.Euler(0,side>0 ? 180:0,0); label.transform.localScale=new Vector3(1/.20f,1/.25f,1/.007f);
                TMP_Text text=label.GetComponent<TMP_Text>(); text.text=number.ToString(); text.fontSize=1.4f; text.fontStyle=FontStyles.Bold; text.alignment=TextAlignmentOptions.Center; text.color=material==yellow ? Color.black : new Color(.5f,1,1); text.rectTransform.sizeDelta=new Vector2(.17f,.22f); text.enableAutoSizing=false;
            }
            return tag;
        }
        private void OnDestroy()
        {
            IsPlacing=false;
            if(preview!=null) Destroy(preview); foreach(var marker in placed) if(marker!=null) Destroy(marker.gameObject);
            if(yellow!=null) Destroy(yellow); if(hologram!=null) Destroy(hologram);
        }
    }
}
