using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed class MemoryLoadingScreen : MonoBehaviour
    {
        public static bool IsBusy { get; private set; }
        public static bool IsLoadingAssets { get; private set; }
        public static float Progress { get; private set; }
        public static float BlackBeforeLoadSeconds { get; private set; }
        public static float LastAssetLoadSeconds { get; private set; }
        private CanvasGroup group;
        private GameObject status;
        private Image fill;
        private TMP_Text label, percent;
        private float displayedProgress;
        private ThreadPriority previousPriority;
        public static bool Load(string scene, OfficeDissolveTransition closingMemory = null)
        {
            if(IsBusy || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            IsBusy=IsLoadingAssets=true; Progress=0;
            var host=new GameObject("Memory loading",typeof(MemoryLoadingScreen)); DontDestroyOnLoad(host);
            host.GetComponent<MemoryLoadingScreen>().StartCoroutine(host.GetComponent<MemoryLoadingScreen>().Run(scene,closingMemory));
            return true;
        }
        private IEnumerator Run(string scene, OfficeDissolveTransition closingMemory)
        {
            Time.timeScale=1;
            previousPriority=Application.backgroundLoadingPriority;
            bool returning=scene=="MainMenu";
            BuildUi(returning,scene);
            if(closingMemory!=null)
            {
                group.alpha=1;
                Image blackout=transform.Find("Blackout").GetComponent<Image>(); blackout.color=Color.clear;
                RectTransform sheet=(RectTransform)status.transform; sheet.anchoredPosition=new Vector2(0,-260); sheet.localScale=Vector3.one*.65f;
                label.text="CERRANDO MEMORIA";
                IEnumerator closing=closingMemory.PlayDissolve(); float began=Time.realtimeSinceStartup;
                while(closing.MoveNext())
                {
                    SetProgress(Mathf.Clamp01((Time.realtimeSinceStartup-began)/closingMemory.RetractionDuration),"CERRANDO MEMORIA");
                    yield return closing.Current;
                }
                sheet.anchoredPosition=Vector2.zero; sheet.localScale=Vector3.one; blackout.color=Color.black;
                displayedProgress=Progress=0;
            }
            // No status panel during the blackout. Wait real frames AND a deliberate hold
            // before touching scene assets, so even a cached scene cannot flash through.
            status.SetActive(false);
            yield return Fade(0,1,returning ? .2f : .55f);
            float blackAt=Time.realtimeSinceStartup;
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(returning ? .05f : .4f);
            BlackBeforeLoadSeconds=Time.realtimeSinceStartup-blackAt;
            try { OfficeAssetCache.Retain(SceneManager.GetActiveScene()); }
            catch(System.Exception exception) { Debug.LogWarning("[Office cache] Continuing scene load without refreshing cache: " + exception.Message); }
            status.SetActive(true);
            SetProgress(0,returning ? "DESCARGANDO ENTORNO" : "LEYENDO ARCHIVO DE MEMORIA");
            yield return null;
            Application.backgroundLoadingPriority=ThreadPriority.High;
            float started=Time.realtimeSinceStartup;
            AsyncOperation operation=SceneManager.LoadSceneAsync(scene);
            operation.allowSceneActivation=false;
            while(operation.progress<.9f)
            {
                SetProgress(operation.progress,returning ? "DESCARGANDO ENTORNO / RECUPERANDO OFICINA" : "CARGANDO ASSETS Y ENTORNO");
                yield return null;
            }
            while(displayedProgress < .89f) { SetProgress(.9f,returning ? "DESCARGANDO ENTORNO / RECUPERANDO OFICINA" : "CARGANDO ASSETS Y ENTORNO"); yield return null; }
            SetProgress(.9f,returning ? "CONECTANDO CASCO" : "INICIALIZANDO RECONSTRUCCIÓN");
            operation.allowSceneActivation=true;
            while(!operation.isDone) yield return null;
            LastAssetLoadSeconds=Time.realtimeSinceStartup-started;
            Application.backgroundLoadingPriority=previousPriority;
            SetProgress(1,returning ? "CONEXIÓN CON LA OFICINA RESTABLECIDA" : "MEMORIA LISTA");
            while(displayedProgress < .999f) { SetProgress(1,returning ? "CONEXIÓN CON LA OFICINA RESTABLECIDA" : "MEMORIA LISTA"); yield return null; }
            IsLoadingAssets=false;
            yield return null;
            status.SetActive(false);
            yield return Fade(1,0,returning ? .65f : .35f);
            IsBusy=false;
            Debug.Log($"[MemoryLoading] {scene}: assets {LastAssetLoadSeconds:F2}s; black before load {BlackBeforeLoadSeconds:F2}s; office cache {OfficeAssetCache.RetainedCount} assets.");
            Destroy(gameObject);
        }
        private void BuildUi(bool returning,string scene)
        {
            var canvas=gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32000;
            var scaler=gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>(); group=gameObject.AddComponent<CanvasGroup>(); group.alpha=0;
            RectTransform black=Panel("Blackout",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.black);
            status=Panel("Session status",black,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(900,350),Vector2.zero,Color.clear).gameObject;
            RectTransform content=(RectTransform)status.transform;
            Text("Brand",content,"ARCHIVE: NULL     /     INTERFAZ NEURAL",18,new Vector2(0,135),new Vector2(900,35),new Color(.42f,.57f,.66f));
            Text("Direction",content,returning ? "VOLVER A LA OFICINA" : "ENTRAR EN LA MEMORIA",44,new Vector2(0,58),new Vector2(900,70),new Color(.91f,.95f,.98f));
            Text("Context",content,returning ? "Evidencias, fotografías y notas conservadas." : scene+"    /    Reconstrucción del entorno",20,new Vector2(0,-8),new Vector2(900,40),new Color(.5f,.61f,.69f));
            var track=Panel("Progress track",content,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(900,3),new Vector2(0,-76),new Color(.1f,.15f,.19f));
            fill=Panel("Measured progress",track,Vector2.zero,new Vector2(0,1),Vector2.zero,Vector2.zero,new Color(.44f,.78f,1)).GetComponent<Image>();
            label=Text("Stage",content,"",16,new Vector2(-65,-112),new Vector2(770,35),new Color(.64f,.74f,.82f));
            percent=Text("Percent",content,"0%",18,new Vector2(405,-112),new Vector2(90,35),Color.white); percent.alignment=TextAlignmentOptions.Right;
        }
        private static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 size,Vector2 position,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=min; rect.anchorMax=max; rect.sizeDelta=size; rect.anchoredPosition=position;
            go.GetComponent<Image>().color=color; return rect;
        }
        private static TMP_Text Text(string name,Transform parent,string value,float size,Vector2 position,Vector2 area,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var text=go.GetComponent<TMP_Text>(); text.text=value; text.fontSize=size; text.color=color; text.raycastTarget=false;
            text.alignment=TextAlignmentOptions.Left; text.rectTransform.sizeDelta=area; text.rectTransform.anchoredPosition=position; return text;
        }
        private void SetProgress(float progress,string stage)
        {
            Progress=Mathf.Clamp01(progress);
            // Ease only toward measured work, never invent progress or jump straight to 90%.
            displayedProgress=Mathf.MoveTowards(displayedProgress,Progress,Time.unscaledDeltaTime*.8f);
            fill.rectTransform.anchorMax=new Vector2(displayedProgress,1);
            label.text=stage; percent.text=Mathf.RoundToInt(displayedProgress*100)+"%";
        }
        private IEnumerator Fade(float from,float to,float duration)
        {
            for(float time=0;time<duration;time+=Time.unscaledDeltaTime) { group.alpha=Mathf.SmoothStep(from,to,time/duration); yield return null; }
            group.alpha=to;
        }
        private void OnDestroy() { Application.backgroundLoadingPriority=previousPriority; IsBusy=IsLoadingAssets=false; }
    }
}
