using System.Collections;
using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace ArchiveNull.UI
{
    public sealed class ModernOfficeDesktop : MonoBehaviour
    {
        private CRTMainMenuController owner;
        private Canvas canvas;
        private RectTransform desktop, window, content, boot;
        private TMP_Text title, status, clock;
        private bool ready;
        private string currentApp;
        private readonly Color ink=new(.86f,.91f,.98f), muted=new(.53f,.63f,.76f), blue=new(.17f,.48f,.94f);
        public bool IsOpen => canvas != null && canvas.enabled;
        public bool IsReady => ready;
        public void Initialize(CRTMainMenuController controller)
        {
            owner=controller;
            var go=new GameObject("Archive OS",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            // The authored monitor controller is scaled. A screen overlay needs its own root.
            canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=9000;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            desktop=Panel(go.transform,"Desktop",new Color(.022f,.047f,.09f),Vector2.zero,new Vector2(1920,1080));
            desktop.anchorMin=Vector2.zero; desktop.anchorMax=Vector2.one; desktop.sizeDelta=Vector2.zero;
            // Quiet, layered wallpaper; the work window stays visually separate from the desktop.
            Panel(desktop,"Wallpaper light",new Color(.04f,.12f,.23f),new Vector2(460,100),new Vector2(760,1080)).localRotation=Quaternion.Euler(0,0,-25);
            Text(desktop,"ARCHIVE",64,new Vector2(610,340),new Vector2(440,90),new Color(.19f,.31f,.47f));
            Text(desktop,"RECONSTRUCCIÓN FORENSE  /  253",17,new Vector2(610,278),new Vector2(440,40),muted);
            App("01","Expedientes",new Vector2(-810,355),OpenCases);
            App("02","Memorias",new Vector2(-810,145),OpenMemories);
            App("03","Informe final",new Vector2(-810,-65),OpenReport);
            App("04","Configuración",new Vector2(-810,-275),OpenSettings);
            var taskbar=Panel(desktop,"Taskbar",new Color(.035f,.055f,.085f,.98f),new Vector2(0,-504),new Vector2(1920,72));
            Button(taskbar,"Inicio",new Vector2(-830,0),new Vector2(150,48),OpenStart);
            Button(taskbar,"Explorador",new Vector2(-620,0),new Vector2(200,48),()=>{if(window!=null) window.gameObject.SetActive(true); else OpenCases();});
            Button(taskbar,"Volver a la oficina",new Vector2(470,0),new Vector2(260,48),owner.DesktopLeave);
            clock=Text(taskbar,"",18,new Vector2(785,0),new Vector2(270,45),ink);
            status=Text(desktop,"OPERADOR 253   /   SESIÓN LOCAL",18,new Vector2(380,-437),new Vector2(990,45),muted);
            canvas.enabled=false;
        }
        public void Open()
        {
            canvas.enabled=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(!ready && boot==null) StartCoroutine(Boot());
        }
        public void Close() { if(canvas!=null) canvas.enabled=false; }
        private void OnDestroy() { if(canvas!=null) Destroy(canvas.gameObject); }
        private IEnumerator Boot()
        {
            boot=Panel(desktop,"Session boot",new Color(.016f,.028f,.05f),Vector2.zero,new Vector2(1920,1080));
            Text(boot,"ARCHIVE OS",48,new Vector2(0,70),new Vector2(700,100),ink);
            var message=Text(boot,"Iniciando sesión del operador…",22,new Vector2(0,-50),new Vector2(900,60),muted);
            var track=Panel(boot,"Progress track",new Color(.10f,.15f,.22f),new Vector2(0,-120),new Vector2(340,4));
            var bar=Panel(track,"Session progress",blue,new Vector2(-125,0),new Vector2(90,4));
            for(float elapsed=0;elapsed<1.8f;elapsed+=Time.unscaledDeltaTime)
            {
                bar.anchoredPosition=new Vector2(Mathf.Sin(elapsed*4)*125,0);
                yield return null;
            }
            EvidenceDrivenMemoryUnlock.RefreshProgress();
            message.text="Escritorio listo"; ready=true;
            Destroy(boot.gameObject); boot=null;
        }
        private void Update()
        {
            if(!IsOpen) return;
            if(clock!=null) clock.text=System.DateTime.Now.ToString("HH:mm   ·   dd/MM/yyyy");
            if(GlobalPauseMenu.IsPaused || CaseFileReader.IsAnyCaseFileOpen || RuntimeConfirmationDialog.IsOpen) return;
            if(ready && owner.HasMountedArchive) status.text="MEMORIA PREPARADA   ·   "+owner.MountedArchiveName+"   /   Salí del escritorio y equipá el visor";
        }
        private void App(string code,string label,Vector2 position,UnityEngine.Events.UnityAction action)
        {
            var button=Button(desktop,"",position,new Vector2(155,160),action);
            button.GetComponent<Image>().color=Color.clear;
            var icon=Panel(button.transform,"Application icon",new Color(.12f,.27f,.45f),new Vector2(0,25),new Vector2(72,68));
            if(code=="01" || code=="02") { icon.GetComponent<Image>().color=new Color(.72f,.50f,.18f); Panel(icon,"Folder tab",new Color(.8f,.6f,.24f),new Vector2(-17,34),new Vector2(38,14)); Panel(icon,"Folder face",new Color(.94f,.72f,.32f),new Vector2(0,-7),new Vector2(78,51)); }
            else Text(icon,code=="03" ? "Aa" : "•••",25,Vector2.zero,new Vector2(72,60),ink);
            Text(button.transform,label,21,new Vector2(0,-48),new Vector2(170,55),ink);
        }
        private void Window(string heading)
        {
            if(window!=null) { window.gameObject.SetActive(false); Destroy(window.gameObject); }
            window=Panel(desktop,"Application window",new Color(.065f,.085f,.12f),new Vector2(100,20),new Vector2(1300,780));
            var header=Panel(window,"Title bar",new Color(.09f,.12f,.17f),new Vector2(0,356),new Vector2(1300,68));
            var drag=header.gameObject.AddComponent<DesktopWindowDrag>(); drag.Target=window;
            title=Text(header,heading,24,new Vector2(-160,0),new Vector2(880,55),ink); title.alignment=TextAlignmentOptions.MidlineLeft;
            Button(header,"—",new Vector2(520,0),new Vector2(64,50),()=>window.gameObject.SetActive(false));
            Button(header,"×",new Vector2(596,0),new Vector2(64,50),()=>{Destroy(window.gameObject);window=null;});
            content=Panel(window,"Content",Color.clear,new Vector2(0,-30),new Vector2(1220,650));
            Text(content,"OPERADOR 253   /   ARCHIVO LOCAL",15,new Vector2(0,-300),new Vector2(1100,35),muted);
        }
        public void OpenCases()
        {
            currentApp="cases"; Window("Explorador  /  Expedientes");
            Text(content,"Tus expedientes",32,new Vector2(0,250),new Vector2(1100,65),ink).alignment=TextAlignmentOptions.MidlineLeft;
            Button(content,"CARPETA   /   La llave por dentro",new Vector2(0,130),new Vector2(1120,105),OpenCaseFolder);
            Text(content,"Caso 01   ·   Julián Herrera   ·   Investigación en curso",21,new Vector2(0,40),new Vector2(1100,70),muted);
        }
        private void OpenCaseFolder()
        {
            Window("Explorador  /  Expedientes  /  La llave por dentro");
            Button(content,"← Expedientes",new Vector2(-430,250),new Vector2(260,48),OpenCases);
            Button(content,"DOCUMENTO   /   Información primaria y personas vinculadas",new Vector2(0,135),new Vector2(1120,90),()=>FindAnyObjectByType<CaseFileReader>()?.OpenFromDesktop());
            Button(content,"CARPETA   /   Memorias reconstruibles",new Vector2(0,20),new Vector2(1120,90),OpenMemories);
            Text(content,"Leé el expediente antes de entrar. Las memorias nuevas se habilitan con evidencia registrada.",23,new Vector2(0,-140),new Vector2(1050,140),muted);
        }
        public void OpenMemories()
        {
            currentApp="memories"; Window("Explorador  /  La llave por dentro  /  Memorias");
            Button(content,"← Expediente",new Vector2(-430,260),new Vector2(260,48),OpenCaseFolder);
            int count=owner.DesktopArchiveCount, rowIndex=0;
            for(int i=0;i<count && i<4;i++)
            {
                if(!owner.DesktopArchiveSupported(i)) continue;
                int index=i; bool unlocked=owner.DesktopArchiveUnlocked(i);
                var row=Button(content,(unlocked ? "DISPONIBLE   /   " : "BLOQUEADA   /   ")+owner.DesktopArchiveName(i),new Vector2(0,165-rowIndex++*96),new Vector2(1120,78),()=>SelectMemory(index));
                if(!unlocked) row.GetComponent<Image>().color=new Color(.10f,.11f,.14f);
            }
            Text(content,CaseEvidenceCatalog.ContractorAccessHint,21,new Vector2(0,-225),new Vector2(1110,100),muted);
        }
        private void SelectMemory(int index)
        {
            if(!owner.DesktopMount(index)) { status.text=CaseEvidenceCatalog.ContractorAccessHint; return; }
            Window("Memoria preparada");
            Text(content,owner.MountedArchiveName,38,new Vector2(0,125),new Vector2(1100,90),ink);
            Text(content,"Ya podés apartarte del ordenador y equipar el visor neural.\nLas fotografías y evidencias se conservan al regresar.",26,new Vector2(0,-5),new Vector2(1030,150),muted);
            Button(content,"Volver a la oficina y usar el visor",new Vector2(0,-160),new Vector2(640,70),owner.DesktopLeave);
        }
        private void OpenReport()
        {
            if(ArchiveNull.Accusation.AccusationPanel.CanPresentReport(out string reason)) ArchiveNull.Accusation.AccusationPanel.OpenRuntime();
            else { Window("Informe final"); Text(content,"Investigación pendiente",34,new Vector2(0,140),new Vector2(1100,80),ink); Text(content,reason,26,new Vector2(0,-10),new Vector2(1050,240),muted); }
        }
        private void OpenSettings()
        {
            GlobalPauseMenu.OpenSettingsRuntime();
        }
        private void OpenStart()
        {
            Window("Inicio  /  Operador 253");
            Button(content,"Expedientes",new Vector2(0,180),new Vector2(700,70),OpenCases);
            Button(content,"Configuración",new Vector2(0,80),new Vector2(700,70),OpenSettings);
            Button(content,"Nueva investigación…",new Vector2(0,-20),new Vector2(700,70),owner.DesktopReset);
            Button(content,"Volver a la oficina",new Vector2(0,-120),new Vector2(700,70),owner.DesktopLeave);
        }
        private RectTransform Panel(Transform parent,string name,Color color,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=color.a>0;
            var rect=image.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        private TMP_Text Text(Transform parent,string value,float size,Vector2 position,Vector2 dimensions,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>();text.text=value;text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            text.rectTransform.sizeDelta=dimensions;text.rectTransform.anchoredPosition=position; return text;
        }
        private Button Button(Transform parent,string value,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(parent,value,new Color(.105f,.15f,.215f),position,size);var button=rect.gameObject.AddComponent<Button>();
            var colors=button.colors; colors.highlightedColor=new Color(.65f,.82f,1);colors.pressedColor=new Color(.4f,.65f,1);button.colors=colors;button.onClick.AddListener(action);
            Text(rect,value,23,Vector2.zero,size-new Vector2(24,6),ink); return button;
        }
    }
    public sealed class DesktopWindowDrag : MonoBehaviour, IDragHandler
    {
        public RectTransform Target;
        public void OnDrag(PointerEventData data)
        {
            var canvas=GetComponentInParent<Canvas>();
            Target.anchoredPosition+=data.delta/canvas.scaleFactor;
            Target.anchoredPosition=new Vector2(Mathf.Clamp(Target.anchoredPosition.x,-250,250),Mathf.Clamp(Target.anchoredPosition.y,-55,80));
        }
    }
}
