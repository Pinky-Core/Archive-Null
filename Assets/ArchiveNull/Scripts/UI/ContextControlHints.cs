using ArchiveNull.Evidence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Slot=ArchiveNull.Evidence.EvidenceCameraController.ToolSlot;

namespace ArchiveNull.UI
{
    public sealed class ContextControlHints : MonoBehaviour
    {
        private TMP_Text text;
        private CanvasGroup group;
        private EvidenceCameraController equipment;
        private TestingToolsController tools;
        private EvidenceMarkerPlacement markers;
        public string DisplayText => text!=null ? text.text : "";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded-=OnSceneLoaded; SceneManager.sceneLoaded+=OnSceneLoaded; OnSceneLoaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }
        private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(!scene.IsValid() || scene.name=="MainMenu" || FindAnyObjectByType<ContextControlHints>()!=null) return;
            new GameObject("ContextControlHints").AddComponent<ContextControlHints>();
        }
        private void Start()
        {
            equipment=FindAnyObjectByType<EvidenceCameraController>(); tools=FindAnyObjectByType<TestingToolsController>(); markers=FindAnyObjectByType<EvidenceMarkerPlacement>();
        }
        private void Awake()
        {
            var host=new GameObject("ContextControlsCanvas",typeof(Canvas),typeof(CanvasScaler)); host.transform.SetParent(transform,false);
            Canvas canvas=host.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=850;
            var scaler=host.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var panel=new GameObject("Equipped controls",typeof(RectTransform),typeof(CanvasGroup)); panel.transform.SetParent(host.transform,false);
            var rect=(RectTransform)panel.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0); rect.sizeDelta=new Vector2(550,145); rect.anchoredPosition=new Vector2(-52,205);
            group=panel.GetComponent<CanvasGroup>(); group.blocksRaycasts=false; group.alpha=0;
            var label=new GameObject("Controls",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(rect,false); text=label.GetComponent<TMP_Text>();
            text.fontSize=22; text.lineSpacing=8; text.alignment=TextAlignmentOptions.BottomRight; text.color=new Color(.91f,.91f,.85f); text.raycastTarget=false;
            text.outlineWidth=.18f; text.outlineColor=new Color32(0,0,0,230);
            text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one; text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        }
        private void LateUpdate()
        {
            string value=GetText(); text.text=value;
            group.alpha=Mathf.MoveTowards(group.alpha,string.IsNullOrEmpty(value) ? 0 : 1,Time.unscaledDeltaTime*7);
        }
        private string GetText()
        {
            if(GlobalPauseMenu.IsPaused || MemoryLoadingScreen.IsBusy || SceneRebuildTransition.IsBusy || EvidenceNotebookUI.IsAnyNotebookOpen || RuntimeConfirmationDialog.IsOpen || !PlayerAssistanceSettings.ShouldShowHelp) return "";
            if(EvidenceMarkerPlacement.IsPlacing) { if(markers==null) markers=FindAnyObjectByType<EvidenceMarkerPlacement>(); return markers!=null ? markers.Controls : ""; }
            if(TestingToolsController.IsPlacing) return tools!=null ? tools.ContextHint : "";
            if(InspectObject.IsAnyInspecting) return "INSPECCIÓN\nClic + mouse  rotar   ·   Rueda  acercar\nE  dejar objeto";
            if(PhoneEvidenceReader.IsAnyOpen) return "TELÉFONO\nClic  seleccionar   ·   Enter  confirmar\nEsc  volver   ·   G  guardar";
            if(EvidenceCameraController.IsAnyRadialMenuOpen) return "HERRAMIENTAS\nMouse  seleccionar   ·   Soltar G  equipar";
            if(equipment==null || equipment.EquippedTool==Slot.Hand) return "";
            string action=GlobalInputBindings.GetDisplayName(GameInputAction.Camera);
            return equipment.EquippedTool switch
            {
                Slot.Camera => EvidenceCameraController.IsAnyCameraModeActive ? "CÁMARA\nClic  fotografiar   ·   Rueda  zoom\n"+action+"  bajar cámara" : "CÁMARA\n"+action+"  levantar   ·   G  cambiar herramienta",
                Slot.UvLight => "LUZ ULTRAVIOLETA\n"+action+"  encender / apagar\nE sobre trípode  montar   ·   G  guardar",
                Slot.Tags => markers!=null ? markers.Controls : "MARCADORES\nClic  colocar   ·   E  recoger",
                Slot.InventoryItem => "OBJETO EQUIPADO\nE  interactuar   ·   G  guardar",
                _ => tools!=null ? tools.ContextHint : ""
            };
        }
    }
}
