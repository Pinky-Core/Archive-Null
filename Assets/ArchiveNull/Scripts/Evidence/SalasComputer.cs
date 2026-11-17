using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.Evidence
{
    public sealed class SalasComputer : MonoBehaviour
    {
        public static bool IsOpen {get;private set;}
        private static int closeFrame=-1;
        public static bool BlocksInput=>IsOpen || closeFrame==Time.frameCount;
        private GameObject screen;
        private TMP_Text document,prompt;
        private FirstPersonMovement movement;
        private FirstPersonLook look;
        private bool movementEnabled,lookEnabled;
        private Renderer monitor;
        public bool HasMonitor=>monitor!=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() {SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;Loaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);}
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="F1-Office" && FindAnyObjectByType<SalasComputer>()==null) new GameObject("Salas computer access").AddComponent<SalasComputer>();
        }
        private void Start()
        {
            int best=0;
            foreach(var renderer in FindObjectsByType<MeshRenderer>())
            {
                if(renderer.gameObject.layer is 30 or 31 || renderer.GetComponentInParent<FirstPersonMovement>()!=null) continue;
                string names=renderer.name;
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh!=null && mesh.sharedMesh!=null) names+=" "+mesh.sharedMesh.name;
                for(Transform parent=renderer.transform.parent;parent!=null;parent=parent.parent) names+=" "+parent.name;
                foreach(var material in renderer.sharedMaterials) if(material!=null) names+=" "+material.name;
                names=names.ToLowerInvariant();
                int score=names.Contains("monitor") ? 4 : names.Contains("computer") || names.Contains("laptop") || names.Contains("ordenador") ? 3 : names.Contains("screen") || names.Contains("pantalla") ? 2 : names.Contains("pc_") ? 1:0;
                if(score>best) {best=score;monitor=renderer;}
            }
            if(monitor==null) return;
            var host=new GameObject("Computer prompt",typeof(Canvas),typeof(CanvasScaler));host.transform.SetParent(transform,false);host.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            prompt=Text(host.transform,"",24,new Vector2(0,-340),new Vector2(900,60));prompt.alignment=TextAlignmentOptions.Center;prompt.outlineWidth=.2f;
        }
        private bool IsAimedAt()
        {
            var eyes=Camera.main;if(eyes==null || monitor==null) return false;
            Vector3 center=monitor.bounds.center;float scale=Mathf.Max(.5f,eyes.transform.lossyScale.y);
            if(Vector3.Distance(eyes.transform.position,center)>3.2f*scale || Vector3.Angle(eyes.transform.forward,center-eyes.transform.position)>22) return false;
            if(Physics.Raycast(eyes.transform.position,(center-eyes.transform.position).normalized,out var hit,Vector3.Distance(eyes.transform.position,center),~((1<<30)|(1<<31)),QueryTriggerInteraction.Ignore))
                return hit.transform==monitor.transform || hit.transform.IsChildOf(monitor.transform.parent) || Vector3.Distance(hit.point,center)<monitor.bounds.size.magnitude*.7f;
            return true;
        }
        private void Update()
        {
            if(prompt==null) return;
            if(IsOpen) {prompt.text="";if(GlobalInputBindings.WasPressed(GameInputAction.Pause)) Close();return;}
            bool blocked=GlobalPauseMenu.IsPaused || EvidenceNotebookUI.IsAnyNotebookOpen || PhoneEvidenceReader.IsAnyOpen || InspectObject.IsAnyInspecting || RuntimeConfirmationDialog.IsOpen || ArchiveNull.UI.MemoryLoadingScreen.IsBusy || ArchiveNull.Narrative.EchoMemory.BlocksInput;
            bool aimed=!blocked && IsAimedAt();prompt.text=aimed ? GlobalInputBindings.GetDisplayName(GameInputAction.Interact)+"  Usar el ordenador de Salas":"";
            if(aimed && GlobalInputBindings.WasPressed(GameInputAction.Interact)) Open();
        }
        public void Open()
        {
            if(IsOpen) return;
            movement=FindAnyObjectByType<FirstPersonMovement>();look=FindAnyObjectByType<FirstPersonLook>();movementEnabled=movement!=null && movement.enabled;lookEnabled=look!=null && look.enabled;
            if(movement!=null) movement.enabled=false;if(look!=null) look.enabled=false;IsOpen=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            screen=new GameObject("Salas workstation",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));screen.transform.SetParent(transform,false);
            var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=23000;
            var scaler=screen.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("Documents",typeof(RectTransform),typeof(Image));panel.transform.SetParent(screen.transform,false);var rect=(RectTransform)panel.transform;rect.sizeDelta=new Vector2(1260,760);panel.GetComponent<Image>().color=new Color(.025f,.045f,.065f,.98f);
            Text(rect,"OBRA SALAS · DOCUMENTOS",32,new Vector2(0,305),new Vector2(1100,70));
            Button(rect,"Comprobantes",new Vector2(-430,195),()=>ShowDocument(false));Button(rect,"Agenda de reformas",new Vector2(-430,95),()=>ShowDocument(true));
            Button(rect,"Registrar comprobantes",new Vector2(210,-265),RegisterReceipts);Button(rect,"Cerrar",new Vector2(440,305),Close);
            document=Text(rect,"",25,new Vector2(200,0),new Vector2(730,470));ShowDocument(false);
        }
        private void ShowDocument(bool agenda)
        {
            document.text=agenda ? "CASA HERRERA\n\nRevisión del acceso lateral y ventana.\nConservar la copia de la llave con los útiles de nivelación.\n\nVisita del 14 de junio: coordinar con Julián." : "COMPROBANTES DE OBRA\n\nSe conservan dos versiones con el mismo número y distintos importes.\n\nJulián solicitó revisar los originales antes de llevar la documentación al abogado.\n\nCompará estas facturas con su nota.";
        }
        private void RegisterReceipts()
        {
            foreach(var target in FindObjectsByType<EvidenceTarget>(FindObjectsInactive.Include))
                if(target.EvidenceData.evidenceId=="f1_ofice_factura1") {EvidenceInventory.Instance.RegisterEvidence(target.EvidenceData);GameSaveSystem.SaveNow();document.text="Comprobantes incorporados al expediente.\n\nPodés contrastarlos con la nota de Julián en el pizarrón.";return;}
        }
        public void Close()
        {
            if(!IsOpen) return;IsOpen=false;closeFrame=Time.frameCount;
            if(screen!=null) {screen.SetActive(false);Destroy(screen);}
            if(movement!=null) movement.enabled=movementEnabled;if(look!=null) look.enabled=lookEnabled;
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        private static TMP_Text Text(Transform parent,string value,float size,Vector2 position,Vector2 dimensions)
        {
            var obj=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);var text=obj.GetComponent<TMP_Text>();text.text=value;text.fontSize=size;text.raycastTarget=false;text.color=new Color(.9f,.94f,1);text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=dimensions;return text;
        }
        private static void Button(Transform parent,string value,Vector2 position,UnityEngine.Events.UnityAction action)
        {
            var obj=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.sizeDelta=new Vector2(310,65);rect.anchoredPosition=position;obj.GetComponent<Image>().color=new Color(.12f,.21f,.32f);obj.GetComponent<Button>().onClick.AddListener(action);Text(rect,value,22,Vector2.zero,new Vector2(285,55)).alignment=TextAlignmentOptions.Center;
        }
        private void OnDisable()=>Close();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() {IsOpen=false;closeFrame=-1;}
    }
}
