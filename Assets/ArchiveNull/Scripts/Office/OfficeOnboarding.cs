using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace ArchiveNull.UI
{
    public sealed class OfficeOnboarding : MonoBehaviour
    {
        public static bool CanUseWorkstation => PlayerPrefs.GetInt("archive.office.casefile.discovered",0)==1 || PlayerPrefs.GetInt(OfficeSpeakerTutorial.CompletedPref,0)==1;
        private TMP_Text text;
        private Canvas canvas;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded-=OnScene; SceneManager.sceneLoaded+=OnScene;
            OnScene(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        private static void OnScene(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="MainMenu" && FindAnyObjectByType<OfficeOnboarding>()==null) new GameObject("Office onboarding").AddComponent<OfficeOnboarding>();
        }
        private void Awake()
        {
            var go=new GameObject("Office next step",typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=9100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Step",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);
            var image=panel.GetComponent<Image>();image.color=new Color(.025f,.045f,.065f,.92f);image.raycastTarget=false;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(36,-32);rect.sizeDelta=new Vector2(630,100);
            var label=new GameObject("Instruction",typeof(TextMeshProUGUI));label.transform.SetParent(panel.transform,false);text=label.GetComponent<TMP_Text>();text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.fontSize=22;text.color=new Color(.85f,.92f,1);text.raycastTarget=false;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(22,12);text.rectTransform.offsetMax=new Vector2(-22,-12);
        }
        private void Update()
        {
            bool complete=PlayerPrefs.GetInt(OfficeSpeakerTutorial.CompletedPref,0)==1;
            canvas.enabled=!complete && !GlobalPauseMenu.IsPaused && !MemoryLoadingScreen.IsBusy && !CaseFileReader.IsAnyCaseFileOpen;
            if(!canvas.enabled) return;
            var focus=FindAnyObjectByType<CRTMenuCameraFocus>();var menu=FindAnyObjectByType<CRTMainMenuController>();var visor=FindAnyObjectByType<VRHeadsetArchiveStarter>();
            if(!CanUseWorkstation) text.text="01 / 05   INFORMACIÓN PRIMARIA\nAbrí el expediente de la mesa antes de sentarte.";
            else if(menu!=null && menu.HasMountedArchive) text.text="05 / 05   INGRESAR A LA MEMORIA\nVolvé a la oficina, equipá el visor y confirmá.";
            else if(focus!=null && focus.IsFocused) text.text="04 / 05   PREPARAR MEMORIA\nAbrí Memorias y seleccioná Casa Herrera.";
            else if(focus!=null && focus.IsInFarPose) text.text="03 / 05   ESCRITORIO\nHacé clic en el ordenador para abrir tu sesión.";
            else text.text="02 / 05   TOMAR ASIENTO\nHacé clic en la silla frente al ordenador.";
            if(visor!=null && visor.IsEquipped) {PlayerPrefs.SetInt(OfficeSpeakerTutorial.CompletedPref,1);PlayerPrefs.Save();}
        }
    }
}
