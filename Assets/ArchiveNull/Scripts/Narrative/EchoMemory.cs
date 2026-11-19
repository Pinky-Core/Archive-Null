using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace ArchiveNull.Narrative
{
    public sealed class EchoMemory : MonoBehaviour
    {
        public static bool IsPlaying {get;private set;}
        private static int finishedFrame=-1;
        public static bool BlocksInput=>IsPlaying || finishedFrame==Time.frameCount;
        private GameObject stage,screen,cameraHost;
        private RenderTexture texture;
        private Material shadow;
        public void Play(Vector3 origin,string audio,string subtitle)
        {
            if(IsPlaying) return;
            GameTelemetry.Record("echo_started",audio);
            StartCoroutine(Replay(origin,audio,subtitle));
        }
        private IEnumerator Replay(Vector3 origin,string audio,string subtitle)
        {
            IsPlaying=true;
            Camera eyes=Camera.main;
            float scale=Mathf.Max(.5f,Mathf.Abs(eyes.transform.lossyScale.y));
            var player=FindAnyObjectByType<FirstPersonMovement>();
            if(player!=null) origin.y=player.transform.position.y+.02f;
            stage=new GameObject("Victim's memory silhouettes");stage.transform.position=origin;stage.transform.localScale=Vector3.one*scale;
            shadow=new Material(Resources.Load<Shader>("ArchiveTrace"));shadow.color=new Color(.035f,.045f,.07f,.72f);
            Transform victim=Figure(stage.transform,new Vector3(0,0,0));
            Transform visitor=Figure(stage.transform,new Vector3(.9f,0,.5f));
            var particles=stage.AddComponent<ParticleSystem>();var main=particles.main;main.startLifetime=1.4f;main.startSpeed=.18f;main.startSize=.035f;main.startColor=new Color(.03f,.04f,.07f,.55f);main.maxParticles=220;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(2,1.7f,1);shape.position=new Vector3(0,.8f,0);
            var emission=particles.emission;emission.rateOverTime=90;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=shadow;
            Vector3 focus=origin+Vector3.up*.7f*scale;
            cameraHost=new GameObject("Echo third-person camera");var view=cameraHost.AddComponent<Camera>();view.CopyFrom(eyes);view.tag="Untagged";view.cullingMask&=~((1<<30)|(1<<31));view.fieldOfView=65;view.nearClipPlane=.04f;view.useOcclusionCulling=false;
            view.GetUniversalAdditionalCameraData().cameraStack.Clear();
            Vector3 offset=(-eyes.transform.forward*3+Vector3.up*1.7f)*scale;
            if(Physics.SphereCast(focus,.12f,offset.normalized,out var hit,offset.magnitude,~((1<<30)|(1<<31)),QueryTriggerInteraction.Ignore)) offset=offset.normalized*Mathf.Max(.55f,hit.distance-.16f);
            view.transform.position=focus+offset;view.transform.LookAt(focus);
            texture=new RenderTexture(1280,720,24);texture.Create();view.targetTexture=texture;
            screen=new GameObject("Echo memory view",typeof(Canvas),typeof(CanvasScaler));var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=24000;
            var scaler=screen.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var picture=new GameObject("Memory",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(screen.transform,false);var image=picture.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;Stretch(image.rectTransform);
            var label=new GameObject("Recollection subtitle",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(screen.transform,false);var text=label.GetComponent<TMP_Text>();text.text="<size=20>RECUERDO · ESC para omitir</size>\n"+subtitle;text.fontSize=28;text.alignment=TextAlignmentOptions.Bottom;text.color=Color.white;text.outlineWidth=.25f;text.raycastTarget=false;
            var rect=text.rectTransform;rect.anchorMin=new Vector2(.1f,.04f);rect.anchorMax=new Vector2(.9f,.24f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            CaseVoicePlayer.Play(audio);
            float duration=Resources.Load<AudioClip>("CaseAudio/"+audio)?.length ?? 9f;
            float elapsed=0;
            while(elapsed<duration)
            {
                if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) break;
                elapsed+=Time.unscaledDeltaTime;
                visitor.localPosition=new Vector3(.9f,0,Mathf.Lerp(.8f,.35f,Mathf.Clamp01(elapsed/4)));
                victim.localRotation=Quaternion.Euler(0,Mathf.Sin(elapsed*.7f)*12,Mathf.SmoothStep(0,18,Mathf.Clamp01((elapsed-duration*.65f)/3)));
                yield return null;
            }
            GameTelemetry.Record("echo_ended",audio,context:elapsed<duration ? "skipped":"finished");
            Cleanup();
        }
        private Transform Figure(Transform parent,Vector3 local)
        {
            var root=new GameObject("Shadow figure").transform;root.SetParent(parent,false);root.localPosition=local;
            Part(root,PrimitiveType.Capsule,new Vector3(0,.85f,0),new Vector3(.38f,.48f,.25f));
            Part(root,PrimitiveType.Sphere,new Vector3(0,1.48f,0),new Vector3(.24f,.29f,.24f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(root,PrimitiveType.Capsule,new Vector3(side*.12f,.3f,0),new Vector3(.14f,.32f,.14f));
                Part(root,PrimitiveType.Capsule,new Vector3(side*.26f,.9f,0),new Vector3(.12f,.34f,.12f));
            }
            return root;
        }
        private void Part(Transform parent,PrimitiveType kind,Vector3 local,Vector3 scale)
        {
            var part=GameObject.CreatePrimitive(kind);part.transform.SetParent(parent,false);part.transform.localPosition=local;part.transform.localScale=scale;
            part.GetComponent<Collider>().enabled=false;Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=shadow;
        }
        private static void Stretch(RectTransform rect) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private void Cleanup()
        {
            CaseVoicePlayer.Stop("echo_");IsPlaying=false;finishedFrame=Time.frameCount;
            if(cameraHost!=null) {cameraHost.GetComponent<Camera>().targetTexture=null;Destroy(cameraHost);}
            if(screen!=null) Destroy(screen);if(stage!=null) Destroy(stage);if(shadow!=null) Destroy(shadow);
            if(texture!=null) {texture.Release();Destroy(texture);}
            cameraHost=screen=stage=null;texture=null;shadow=null;
        }
        private void OnDisable() {StopAllCoroutines();Cleanup();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() {IsPlaying=false;finishedFrame=-1;}
    }
}
