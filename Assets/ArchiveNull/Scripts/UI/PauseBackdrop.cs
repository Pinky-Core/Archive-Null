using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed class PauseBackdrop : MonoBehaviour
    {
        private RenderTexture frame;
        private Material blur;
        public void Capture(Camera camera)
        {
            if(camera==null) return;
            int width=Mathf.Clamp(Screen.width/4,256,640), height=Mathf.Max(144,Mathf.RoundToInt(width/camera.aspect));
            if(frame==null || frame.width!=width || frame.height!=height)
            { if(frame!=null) { frame.Release(); Destroy(frame); } frame=new RenderTexture(width,height,0){filterMode=FilterMode.Bilinear,name="Paused scene"}; frame.Create(); }
            var source=RenderTexture.GetTemporary(width,height,24); var intermediate=RenderTexture.GetTemporary(width,height,0);
            RenderTexture previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=source});
                if(blur==null) { Shader shader=Resources.Load<Shader>("PauseBlur"); if(shader!=null) blur=new Material(shader); }
                if(blur!=null)
                {
                    blur.SetVector("_Direction",new Vector4(1,0,0,0)); Graphics.Blit(source,intermediate,blur);
                    blur.SetVector("_Direction",new Vector4(0,1,0,0)); Graphics.Blit(intermediate,frame,blur);
                }
                else Graphics.Blit(source,frame);
                GetComponent<RawImage>().texture=frame;
            }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(source); RenderTexture.ReleaseTemporary(intermediate); }
        }
        private void OnDestroy() { if(frame!=null) { frame.Release(); Destroy(frame); } if(blur!=null) Destroy(blur); }
    }
    public sealed class PauseMenuAction : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public TMP_Text Label; public Image Rule; private bool hovered,selected;
        public void OnPointerEnter(PointerEventData data) { hovered=true; }
        public void OnPointerExit(PointerEventData data) { hovered=false; }
        public void OnSelect(BaseEventData data) { selected=true; }
        public void OnDeselect(BaseEventData data) { selected=false; }
        private void Update()
        {
            bool focus=hovered || selected;
            Label.color=Color.Lerp(Label.color,focus ? new Color(.96f,.84f,.54f) : new Color(.78f,.79f,.75f),1-Mathf.Exp(-14*Time.unscaledDeltaTime));
            Color color=Rule.color; color.a=Mathf.Lerp(color.a,focus ? .85f : .13f,1-Mathf.Exp(-14*Time.unscaledDeltaTime)); Rule.color=color;
        }
    }
}
