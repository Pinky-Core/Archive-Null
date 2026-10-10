using System.Collections;
using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace ArchiveNull.UI
{
    public sealed partial class ModernOfficeDesktop : MonoBehaviour
    {
        private CRTMainMenuController owner;
        private Canvas canvas;
        private CanvasGroup interaction;
        private bool focused;
        private RectTransform desktop, window, content, boot;
        private TMP_Text title, status, clock;
        private bool ready;
        private Coroutine windowAnimation;
        private CanvasGroup windowGroup;
        private Vector2 windowPosition;
        public bool IsWindowAnimating => windowAnimation != null;
        public bool IsWindowVisible => window != null && window.gameObject.activeSelf;
        private readonly Color ink=new(.86f,.91f,.98f), muted=new(.53f,.63f,.76f), blue=new(.17f,.48f,.94f);
        public bool IsOpen => focused;
        public bool IsPowered => desktop != null && desktop.gameObject.activeSelf;
        public Canvas MonitorCanvas => canvas;
        public bool IsReady => ready;
        public void Initialize(CRTMainMenuController controller, RectTransform surface, Canvas sceneCanvas, float visibleHeight)
        {
            owner=controller;
            AudioListener.volume=PlayerPrefs.GetFloat("global.pause.master.volume",1);
            var go=new GameObject("Archive OS",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster),typeof(CanvasGroup));
            go.transform.SetParent(surface, false);
            canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=sceneCanvas.worldCamera != null ? sceneCanvas.worldCamera : Camera.main;
            var rect=(RectTransform)go.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.sizeDelta=new Vector2(1920,1080);
            // The authored Screen extends under the lower plastic bezel. Keep the top
            // alignment while fitting the usable area; expose the fit on the controller.
            visibleHeight=Mathf.Clamp(visibleHeight,.5f,1f);
            rect.localScale=new Vector3(surface.rect.width/1920f,surface.rect.height*visibleHeight/1080f,1);
            rect.localPosition=new Vector3(0,surface.rect.height*(1-visibleHeight)*.5f,-.01f);
            interaction=go.GetComponent<CanvasGroup>(); interaction.interactable=interaction.blocksRaycasts=false;
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
            Button(taskbar,"Explorador",new Vector2(-620,0),new Vector2(200,48),RestoreWindow);
            Button(taskbar,"Volver a la oficina",new Vector2(470,0),new Vector2(260,48),owner.DesktopLeave);
            clock=Text(taskbar,"",18,new Vector2(785,0),new Vector2(270,45),ink);
            status=Text(desktop,"OPERADOR 253   /   SESIÓN LOCAL",18,new Vector2(380,-437),new Vector2(990,45),muted);
            desktop.gameObject.AddComponent<RectMask2D>();
            InitializeFeedback();
            desktop.gameObject.SetActive(false);
        }
        public void Open()
        {
            focused=true; desktop.gameObject.SetActive(true);
            interaction.interactable=interaction.blocksRaycasts=true;
            owner.SetDesktopPower(true);
            Cursor.lockState=CursorLockMode.None; Cursor.visible=false;
            if(!ready && boot==null) StartCoroutine(Boot());
        }
        // Leaving the seat releases input but preserves the monitor's visible session.
        public void Close() { focused=false; pendingBinding=null; if(computerPointer!=null) computerPointer.gameObject.SetActive(false); if(interaction!=null) interaction.interactable=interaction.blocksRaycasts=false; }
        public void PowerOff() { Close(); if(desktop!=null) desktop.gameObject.SetActive(false); owner.SetDesktopPower(false); }
        private void OnDestroy() { DisposeFeedback(); if(canvas!=null) Destroy(canvas.gameObject); }
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
            PlayComputerSound(ComputerSound.Startup);
            Destroy(boot.gameObject); boot=null;
        }
        private void Update()
        {
            if(!IsOpen) return;
            UpdateSettingsInput();
            if(clock!=null) clock.text=System.DateTime.Now.ToString("HH:mm   ·   dd/MM/yyyy");
            if(GlobalPauseMenu.IsPaused || CaseFileReader.IsAnyCaseFileOpen || RuntimeConfirmationDialog.IsOpen) return;
            if(ready && owner.HasMountedArchive) status.text="MEMORIA PREPARADA   ·   "+owner.MountedArchiveName+"   /   Salí del escritorio y equipá el visor";
        }
        public Vector2 ConfinePointer(Vector2 screenPoint)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop,screenPoint,canvas.worldCamera,out var local)) return screenPoint;
            var bounds=desktop.rect;
            local.x=Mathf.Clamp(local.x,bounds.xMin+8,bounds.xMax-8);
            local.y=Mathf.Clamp(local.y,bounds.yMin+8,bounds.yMax-8);
            return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,desktop.TransformPoint(local));
        }
        private void LateUpdate()
        {
            if(!IsOpen || GlobalPauseMenu.IsPaused || CaseFileReader.IsAnyCaseFileOpen || RuntimeConfirmationDialog.IsOpen || ArchiveNull.Accusation.AccusationPanel.IsAnyOpen || Mouse.current==null) { if(computerPointer!=null) computerPointer.gameObject.SetActive(false); return; }
            Vector2 pointer=Mouse.current.position.ReadValue(), confined=ConfinePointer(pointer);
            UpdateComputerPointer(confined);
            if((confined-pointer).sqrMagnitude>1) Mouse.current.WarpCursorPosition(confined);
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
            if(windowAnimation!=null) { StopCoroutine(windowAnimation); windowAnimation=null; }
            if(window!=null) { window.gameObject.SetActive(false); Destroy(window.gameObject); }
            window=Panel(desktop,"Application window",new Color(.065f,.085f,.12f),new Vector2(100,20),new Vector2(1300,780));
            windowGroup=window.gameObject.AddComponent<CanvasGroup>();
            var header=Panel(window,"Title bar",new Color(.09f,.12f,.17f),new Vector2(0,356),new Vector2(1300,68));
            var drag=header.gameObject.AddComponent<DesktopWindowDrag>(); drag.Target=window;
            title=Text(header,heading,24,new Vector2(-160,0),new Vector2(880,55),ink); title.alignment=TextAlignmentOptions.MidlineLeft;
            Button(header,"—",new Vector2(520,0),new Vector2(64,50),()=>AnimateWindow(false,false));
            Button(header,"×",new Vector2(596,0),new Vector2(64,50),()=>AnimateWindow(false,true));
            content=Panel(window,"Content",Color.clear,new Vector2(0,-30),new Vector2(1220,650));
            Text(content,"OPERADOR 253   /   ARCHIVO LOCAL",15,new Vector2(0,-300),new Vector2(1100,35),muted);
            windowPosition=window.anchoredPosition;
            AnimateWindow(true,false);
        }
        public void RestoreWindow()
        {
            if(window==null) { OpenCases(); return; }
            if(window.gameObject.activeSelf) return;
            window.anchoredPosition=windowPosition;
            AnimateWindow(true,false);
        }
        private void AnimateWindow(bool opening,bool destroy)
        {
            if(window==null) return;
            PlayComputerSound(opening ? ComputerSound.Open : ComputerSound.Close);
            if(windowAnimation!=null) StopCoroutine(windowAnimation);
            if(!opening) windowPosition=window.anchoredPosition;
            window.gameObject.SetActive(true);
            windowAnimation=StartCoroutine(WindowTransition(opening,destroy));
        }
        private IEnumerator WindowTransition(bool opening,bool destroy)
        {
            windowGroup.interactable=windowGroup.blocksRaycasts=false;
            float duration=opening ? .24f : .16f;
            Vector2 from=opening ? windowPosition+new Vector2(0,-24) : window.anchoredPosition;
            Vector2 to=opening ? windowPosition : windowPosition+new Vector2(0,-18);
            float fromAlpha=opening ? 0 : windowGroup.alpha;
            float fromScale=opening ? .96f : window.localScale.x;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                float t=elapsed/duration, eased=1-Mathf.Pow(1-t,3);
                window.anchoredPosition=Vector2.Lerp(from,to,eased);
                window.localScale=Vector3.one*Mathf.Lerp(fromScale,opening ? 1 : .97f,eased);
                windowGroup.alpha=Mathf.Lerp(fromAlpha,opening ? 1 : 0,eased);
                yield return null;
            }
            window.anchoredPosition=windowPosition; window.localScale=Vector3.one;
            windowGroup.alpha=opening ? 1 : 0;
            windowGroup.interactable=windowGroup.blocksRaycasts=opening;
            if(!opening) { window.gameObject.SetActive(false); if(destroy) { Destroy(window.gameObject); window=null; } }
            windowAnimation=null;
        }
        public void OpenCases()
        {
            Window("Explorador  /  Expedientes");
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
            Window("Explorador  /  La llave por dentro  /  Memorias");
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
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=color.a>0;
            var rect=image.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        private TMP_Text Text(Transform parent,string value,float size,Vector2 position,Vector2 dimensions,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>();text.text=value;text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            text.rectTransform.sizeDelta=dimensions;text.rectTransform.anchoredPosition=position; return text;
        }
        private Button Button(Transform parent,string value,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(parent,value,new Color(.105f,.15f,.215f),position,size);var button=rect.gameObject.AddComponent<Button>();
            rect.gameObject.AddComponent<DesktopButtonSound>().Desktop=this;
            button.onClick.AddListener(()=>PlayComputerSound(ComputerSound.Click));
            var colors=button.colors; colors.highlightedColor=new Color(.65f,.82f,1);colors.pressedColor=new Color(.4f,.65f,1);button.colors=colors;button.onClick.AddListener(action);
            Text(rect,value,23,Vector2.zero,size-new Vector2(24,6),ink); return button;
        }
    }
    public sealed class DesktopWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target;
        private Vector2 grabOffset;
        public void OnBeginDrag(PointerEventData data)
        {
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Target.parent,data.position,data.pressEventCamera,out var point)) grabOffset=Target.anchoredPosition-point;
        }
        public void OnDrag(PointerEventData data)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Target.parent,data.position,data.pressEventCamera,out var point)) return;
            Target.anchoredPosition=point+grabOffset;
            Target.anchoredPosition=new Vector2(Mathf.Clamp(Target.anchoredPosition.x,-250,250),Mathf.Clamp(Target.anchoredPosition.y,-55,80));
        }
    }
}
