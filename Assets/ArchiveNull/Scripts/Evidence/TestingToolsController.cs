using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Slot = ArchiveNull.Evidence.EvidenceCameraController.ToolSlot;

namespace ArchiveNull.Evidence
{
    [DefaultExecutionOrder(-50)]
    public sealed class TestingToolsController : MonoBehaviour
    {
        private static bool tripodPlacing;
        public static bool IsPlacing => tripodPlacing || EvidenceMarkerPlacement.IsPlacing;
        public static void NotifyPlacementCancelled() { cancelFrame=Time.frameCount; }
        public static bool BrushActive { get; private set; }
        private static int cancelFrame = -1;
        public static bool CancelledThisFrame => cancelFrame == Time.frameCount;
        private Camera view;
        private EvidenceCameraController equipment;
        private GameObject preview, tripod, brush, flashlight, foldedTripod;
        private Transform mount;
        private Material hologram, solid, bristles;
        private float yaw, sceneScale=1;
        public static float PlayerScale(Transform player,Transform eyes) => Mathf.Clamp(Mathf.Abs(eyes.position.y-player.position.y)/1.65f,.25f,12f);
        private bool valid, flashlightOn;
        private TMP_Text hint;
        private Light whiteLight;
        private Canvas canvas;
        private readonly Collider[] overlaps = new Collider[32];
        public Transform TripodMount => mount;
        public bool OwnsTripod(Collider collider) => tripod!=null && collider!=null && collider.transform.IsChildOf(tripod.transform);
        public string ContextHint => hint!=null ? hint.text : "";
        public bool HasTripod => tripod != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            tripodPlacing=BrushActive=false;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneLoaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;
            FirstPersonMovement player = FindAnyObjectByType<FirstPersonMovement>();
            if (player != null && player.GetComponent<TestingToolsController>() == null) player.gameObject.AddComponent<TestingToolsController>();
        }
        private void Start()
        {
            if(GetComponent<EvidenceMarkerPlacement>()==null) gameObject.AddComponent<EvidenceMarkerPlacement>();
            view=GetComponentInChildren<Camera>();
            sceneScale=PlayerScale(transform,view.transform);
            equipment=GetComponentInChildren<EvidenceCameraController>();
            if(equipment==null) equipment=FindAnyObjectByType<EvidenceCameraController>();
            solid = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Resources.Load<Shader>("ArchiveTrace")); solid.color=new Color(.055f,.065f,.07f);
            bristles=new Material(solid); bristles.color=new Color(.7f,.66f,.55f);
            hologram = new Material(Resources.Load<Shader>("PlacementHologram"));
            var ui = new GameObject("Tool placement instructions",typeof(Canvas),typeof(CanvasScaler)); canvas=ui.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=830;
            ui.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; ui.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920,1080);
            ui.GetComponent<CanvasScaler>().screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var label = new GameObject("Hint",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(ui.transform,false);
            hint=label.GetComponent<TMP_Text>(); hint.fontSize=20; hint.color=new Color(.8f,.91f,.97f); hint.alignment=TextAlignmentOptions.BottomLeft; hint.raycastTarget=false;
            hint.rectTransform.anchorMin=new Vector2(.04f,.04f); hint.rectTransform.anchorMax=new Vector2(.65f,.14f); hint.rectTransform.offsetMin=hint.rectTransform.offsetMax=Vector2.zero;
            brush=Held("Powder and forensic brush",new Vector3(.22f,-.2f,.45f));
            Piece(brush.transform,Vector3.zero,new Vector3(.025f,.19f,.025f),solid);
            Piece(brush.transform,new Vector3(0,.12f,0),new Vector3(.065f,.08f,.03f),bristles);
            Piece(brush.transform,new Vector3(-.08f,0,0),new Vector3(.055f,.10f,.055f),solid);
            Piece(brush.transform,new Vector3(-.08f,.058f,0),new Vector3(.058f,.02f,.058f),bristles);
            flashlight=Held("White flashlight",new Vector3(.24f,-.18f,.42f));
            Piece(flashlight.transform,Vector3.zero,new Vector3(.065f,.065f,.22f),solid);
            Piece(flashlight.transform,new Vector3(0,0,.13f),new Vector3(.10f,.10f,.07f),solid);
            Piece(flashlight.transform,new Vector3(0,0,.17f),new Vector3(.075f,.075f,.005f),bristles);
            var lamp=new GameObject("White beam"); lamp.transform.SetParent(flashlight.transform,false); lamp.transform.localPosition=new Vector3(0,0,.19f);
            whiteLight=lamp.AddComponent<Light>(); whiteLight.type=LightType.Spot; whiteLight.color=new Color(1,.96f,.86f); whiteLight.range=22; whiteLight.spotAngle=55; whiteLight.innerSpotAngle=30; whiteLight.intensity=14; whiteLight.shadows=LightShadows.Soft; whiteLight.enabled=false;
            foldedTripod=Held("Folded tripod",new Vector3(.22f,-.28f,.5f));
            for(int i=0;i<3;i++) Piece(foldedTripod.transform,new Vector3((i-1)*.045f,0,0),new Vector3(.028f,.42f,.028f),solid);
            foreach(GameObject tool in new[]{brush,flashlight,foldedTripod})
            { foreach(Transform child in tool.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31; tool.SetActive(false); }
            equipment.RegisterTool(Slot.Brush,brush); equipment.RegisterTool(Slot.Flashlight,flashlight); equipment.RegisterTool(Slot.Tripod,foldedTripod);
        }
        private GameObject Held(string name,Vector3 position)
        {
            var item=new GameObject(name); item.transform.SetParent(view.transform,false);
            Vector3 scale=view.transform.lossyScale;
            item.transform.localScale=new Vector3(sceneScale/Mathf.Abs(scale.x),sceneScale/Mathf.Abs(scale.y),sceneScale/Mathf.Abs(scale.z));
            item.transform.localPosition=Vector3.Scale(position,item.transform.localScale);
            return item;
        }
        public void RefreshEquippedTools()
        {
            if(equipment==null) return;
            BrushActive=equipment.EquippedTool==Slot.Brush && equipment.MountedTool!=Slot.Brush;
            if(whiteLight!=null && equipment.MountedTool!=Slot.Flashlight) whiteLight.enabled=equipment.EquippedTool==Slot.Flashlight && flashlightOn;
        }
        private bool Modal => GlobalPauseMenu.IsPaused || EvidenceNotebookUI.IsAnyNotebookOpen || RuntimeConfirmationDialog.IsOpen || InspectObject.IsAnyInspecting || PhoneEvidenceReader.IsAnyOpen || ArchiveNull.Accusation.AccusationPanel.IsAnyOpen || ArchiveNull.UI.MemoryLoadingScreen.IsBusy || ArchiveNull.UI.SceneRebuildTransition.IsBusy;
        private bool AimingAtTripod()
        {
            return tripod!=null && PlacementSurface.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),3f*sceneScale,transform,out var hit) && hit.transform.IsChildOf(tripod.transform);
        }
        private void Update()
        {
            if(view==null || equipment==null || Keyboard.current==null) return;
            canvas.enabled=false; // All equipped controls share ContextControlHints.
            if(Modal || EvidenceMarkerPlacement.IsPlacing || EvidenceMarkerPlacement.HandledInputThisFrame) return;
            equipment.TickMountedTool();
            RefreshEquippedTools();
            var keys=Keyboard.current; var mouse=Mouse.current;
            if(EvidenceCameraController.IsAnyRadialMenuOpen) return;
            if (keys.gKey.wasPressedThisFrame && tripodPlacing) { CancelPlacement(); return; }
            bool atTripod=AimingAtTripod();
            if(!tripodPlacing && !EvidenceCameraController.IsAnyCameraModeActive)
            {
                // Keyboard shortcuts select the very same wheel slots; there is no second inventory.
                if(keys.bKey.wasPressedThisFrame) equipment.SelectTool(equipment.EquippedTool==Slot.Brush ? Slot.Hand : Slot.Brush);
                if(keys.tKey.wasPressedThisFrame) { equipment.SelectTool(Slot.Tripod); if(tripod==null || atTripod) BeginPlacement(); }
                if(atTripod && keys.eKey.wasPressedThisFrame)
                {
                    if(equipment.MountedObject!=null) equipment.RetrieveMounted(); else equipment.MountEquipped(mount);
                    return;
                }
                if(atTripod && equipment.MountedObject!=null && keys.lKey.wasPressedThisFrame) equipment.SetMountedPower(!equipment.MountedPowered);
                if(atTripod && equipment.MountedTool==Slot.Camera && keys.pKey.wasPressedThisFrame)
                    equipment.CaptureFrom(equipment.MountedObject.GetComponent<PhysicalEvidenceCamera>().LensCamera);
                if(equipment.EquippedTool==Slot.Flashlight && GlobalInputBindings.WasPressed(GameInputAction.Camera)) { flashlightOn=!flashlightOn; RefreshEquippedTools(); }
                if(equipment.EquippedTool==Slot.Tripod && mouse!=null && mouse.leftButton.wasPressedThisFrame && (tripod==null || atTripod)) BeginPlacement();
            }
            if(tripodPlacing)
            {
                if(keys.escapeKey.wasPressedThisFrame || mouse!=null && mouse.rightButton.wasPressedThisFrame) { CancelPlacement(); return; }
                yaw+=((keys.rKey.isPressed ? 1:0)-(keys.qKey.isPressed ? 1:0))*90f*Time.deltaTime;
                if(mouse!=null) yaw+=mouse.scroll.ReadValue().y/120f*15f;
                UpdatePlacement();
                if(valid && mouse!=null && mouse.leftButton.wasPressedThisFrame && placementFrame!=Time.frameCount) ConfirmPlacement();
                hint.text=(valid ? "COLOCAR TRÍPODE" : "BUSCÁ SUELO FIRME Y ESPACIO LIBRE")+"\nClic: colocar   ·   Q/R o rueda: rotar   ·   clic derecho/Esc: cancelar";
            }
            else if(atTripod)
                hint.text=equipment.MountedObject!=null ? "E  RECUPERAR HERRAMIENTA   ·   T  MOVER TRÍPODE\nL  encender / apagar"+(equipment.MountedTool==Slot.Camera ? "   ·   P  fotografiar" : "") : "TRÍPODE VACÍO\nEquipá una herramienta con G y pulsá E para montarla   ·   T  mover";
            else if(BrushActive)
            {
                hint.text="PINCEL + POLVO REVELADOR\nMantené clic sobre la superficie   ·   G  cambiar herramienta";
                bool applying=mouse!=null && mouse.leftButton.isPressed;
                if(brush!=equipment.MountedObject) brush.transform.localRotation=Quaternion.Euler(0,0,applying ? Mathf.Sin(Time.time*22)*12 : 0);
                if(applying) ApplyReagent(view.ViewportPointToRay(new Vector3(.5f,.5f,0)));
            }
            else if(equipment.EquippedTool==Slot.Tripod) hint.text=tripod==null ? "TRÍPODE\nClic  colocar   ·   G  guardar" : "TRÍPODE DESPLEGADO\nAcercate y apuntá al trípode para moverlo con T";
            else if(equipment.EquippedTool==Slot.Flashlight) hint.text="LINTERNA\nF  encender / apagar   ·   E sobre el trípode  montar";
            else hint.text=SceneManager.GetActiveScene().name=="MechanicsLab" ? "LABORATORIO\nG  rueda de herramientas   ·   TAB  libreta y fotos" : "";
        }
        private int placementFrame;
        public void BeginPlacement()
        {
            if(tripodPlacing || !equipment.IsToolAvailable(Slot.Tripod)) return;
            equipment.StowForExternalTool();
            BrushActive=false; tripodPlacing=true; placementFrame=Time.frameCount; yaw=tripod!=null ? tripod.transform.eulerAngles.y : view.transform.eulerAngles.y;
            preview=CreateTripod(hologram);
            if(tripod!=null) tripod.SetActive(false);
        }
        public void ApplyReagent(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,2.7f*sceneScale,~(1<<31),QueryTriggerInteraction.Ignore)) return;
            foreach(var trace in UvRevealTarget.ActiveTargets) if(Vector3.Distance(trace.RevealPosition,hit.point)<.45f*sceneScale) trace.ReceiveUvIllumination(1);
        }
        private void UpdatePlacement()
        {
            valid=false;
            if(!PlacementSurface.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),5f*sceneScale,transform,out var hit)) { preview.SetActive(false); return; }
            preview.SetActive(true); preview.transform.SetPositionAndRotation(hit.point+Vector3.up*.02f*sceneScale,Quaternion.Euler(0,yaw,0));
            valid=hit.normal.y>.9f;
            if(ArchiveNull.UI.OfficeDissolveTransition.ActiveMemory!=null && Vector3.Distance(ArchiveNull.UI.OfficeDissolveTransition.ActiveMemory.ConstrainPosition(hit.point,.6f*sceneScale),hit.point)>.01f) valid=false;
            if(!PlacementSurface.Clear(hit.point+Vector3.up*.83f*sceneScale,new Vector3(.43f,.75f,.43f)*sceneScale,preview.transform.rotation,transform,tripod!=null ? tripod.transform : null)) valid=false;
            for(int i=0;i<3;i++)
            {
                Vector3 foot=hit.point+Quaternion.Euler(0,yaw+i*120,0)*new Vector3(0,.2f,.38f)*sceneScale;
                if(!PlacementSurface.Raycast(new Ray(foot,Vector3.down),.35f*sceneScale,transform,out var support) || support.normal.y<.9f) valid=false;
            }
            hologram.color=valid ? new Color(.1f,.95f,1,.38f):new Color(1,.15f,.1f,.4f);
        }
        public void ConfirmPlacement()
        {
            if(!valid || preview==null) return;
            if(tripod==null)
            {
                tripod=CreateTripod(solid); var collider=tripod.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.76f,0); collider.size=new Vector3(.65f,1.52f,.65f);
                mount=new GameObject("Empty tool mount").transform; mount.SetParent(tripod.transform,false); mount.localPosition=new Vector3(0,1.62f,0);
            }
            tripod.transform.SetPositionAndRotation(preview.transform.position,preview.transform.rotation); tripod.SetActive(true);
            preview.SetActive(false); Destroy(preview); tripodPlacing=false;
        }
        public void CancelPlacement() { if(preview!=null) { preview.SetActive(false); Destroy(preview); } if(tripod!=null) tripod.SetActive(true); tripodPlacing=false; cancelFrame=Time.frameCount; }
        private GameObject CreateTripod(Material material)
        {
            var root=new GameObject("Tripod"); root.transform.localScale=Vector3.one*sceneScale;
            Piece(root.transform,new Vector3(0,.95f,0),new Vector3(.055f,1.1f,.055f),material);
            Piece(root.transform,new Vector3(0,1.5f,0),new Vector3(.15f,.03f,.12f),material);
            for(int i=0;i<3;i++)
            {
                Vector3 bottom=Quaternion.Euler(0,i*120,0)*new Vector3(0,0,.4f); Vector3 top=new Vector3(0,.9f,0);
                var leg=Piece(root.transform,(bottom+top)*.5f,new Vector3(.035f,(top-bottom).magnitude,.035f),material); leg.transform.localRotation=Quaternion.FromToRotation(Vector3.up,top-bottom);
            }
            if(material==hologram)
            {
                Piece(root.transform,new Vector3(0,.03f,.55f),new Vector3(.06f,.02f,.35f),material);
                for(int i=-1;i<=1;i+=2) { var arrow=Piece(root.transform,new Vector3(i*.07f,.03f,.65f),new Vector3(.035f,.02f,.20f),material); arrow.transform.localRotation=Quaternion.Euler(0,i*45,0); }
            }
            return root;
        }
        public static GameObject Piece(Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent,false); part.transform.localPosition=position; part.transform.localScale=size;
            part.GetComponent<Collider>().enabled=false; Destroy(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial=material; return part;
        }
        private void OnDestroy()
        {
            tripodPlacing=BrushActive=false;
            if(preview!=null) Destroy(preview); if(tripod!=null) Destroy(tripod); if(canvas!=null) Destroy(canvas.gameObject);
            if(solid!=null) Destroy(solid); if(hologram!=null) Destroy(hologram); if(bristles!=null) Destroy(bristles);
        }
    }
}
