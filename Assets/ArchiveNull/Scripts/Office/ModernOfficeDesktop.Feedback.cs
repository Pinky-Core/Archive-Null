using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArchiveNull.UI
{
    public sealed partial class ModernOfficeDesktop
    {
        public enum ComputerSound { Hover, Click, Open, Close, Startup }
        private RectTransform computerPointer;
        private Texture2D pointerTexture;
        private Sprite pointerSprite;
        private AudioSource computerAudio;
        private AudioClip[] computerSounds;
        private float nextHoverSound;
        public bool HasComputerPointer => computerPointer!=null;
        public bool HasComputerAudio => computerAudio!=null && computerSounds!=null;
        private void InitializeFeedback()
        {
            pointerTexture=new Texture2D(32,48,TextureFormat.RGBA32,false) {name="Archive OS cursor",filterMode=FilterMode.Bilinear};
            var pixels=new Color[32*48];
            for(int y=0;y<48;y++) for(int x=0;x<32;x++)
            {
                int down=47-y;
                bool arrow=down<32 && x<=down*.65f && x<23;
                bool tail=down>=22 && down<43 && x>=9 && x<=14;
                if(arrow || tail) pixels[y*32+x]=x<2 || (arrow && x>=down*.65f-2) || down>40 ? new Color(.04f,.10f,.18f) : new Color(.90f,.97f,1);
            }
            pointerTexture.SetPixels(pixels);pointerTexture.Apply();
            pointerSprite=Sprite.Create(pointerTexture,new Rect(0,0,32,48),new Vector2(0,1));
            computerPointer=Panel(desktop,"Archive OS pointer",Color.white,Vector2.zero,new Vector2(32,48));
            computerPointer.pivot=new Vector2(0,1);
            var image=computerPointer.GetComponent<Image>(); image.sprite=pointerSprite;image.raycastTarget=false;
            computerPointer.gameObject.SetActive(false);
            computerAudio=gameObject.AddComponent<AudioSource>();computerAudio.playOnAwake=false;computerAudio.spatialBlend=.35f;computerAudio.minDistance=.5f;computerAudio.maxDistance=8;
            computerSounds=new[]{MakeComputerTone("Hover",.025f,760),MakeComputerTone("Click",.045f,560),MakeComputerTone("Window open",.11f,640),MakeComputerTone("Window close",.085f,420),MakeComputerTone("Session ready",.62f,440)};
        }
        private static AudioClip MakeComputerTone(string name,float duration,float frequency)
        {
            const int rate=22050;
            var samples=new float[Mathf.CeilToInt(rate*duration)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate, fraction=t/duration;
                float tone=frequency;
                if(name=="Session ready") tone*=new[]{1f,1.25f,1.5f,2f}[Mathf.Min(3,(int)(fraction*4))];
                float envelope=Mathf.Min(1,t/.008f)*Mathf.Pow(1-fraction,2);
                samples[i]=Mathf.Sin(2*Mathf.PI*t*tone)*envelope*.25f;
            }
            var clip=AudioClip.Create("Archive OS / "+name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void PlayComputerSound(ComputerSound sound)
        {
            if(computerAudio==null || !IsOpen) return;
            if(sound==ComputerSound.Hover) { if(Time.unscaledTime<nextHoverSound) return;nextHoverSound=Time.unscaledTime+.075f; }
            computerAudio.PlayOneShot(computerSounds[(int)sound],(sound==ComputerSound.Hover ? .15f : .45f)*GlobalPauseMenu.EffectsVolume);
        }
        private void UpdateComputerPointer(Vector2 point)
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            computerPointer.gameObject.SetActive(true); computerPointer.SetAsLastSibling();
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop,point,canvas.worldCamera,out var local)) computerPointer.anchoredPosition=local;
        }
        private void DisposeFeedback()
        {
            if(pointerSprite!=null) Destroy(pointerSprite);
            if(pointerTexture!=null) Destroy(pointerTexture);
            if(computerSounds!=null) foreach(var clip in computerSounds) if(clip!=null) Destroy(clip);
        }
    }
    public sealed class DesktopButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        public ModernOfficeDesktop Desktop;
        public void OnPointerEnter(PointerEventData data) => Desktop?.PlayComputerSound(ModernOfficeDesktop.ComputerSound.Hover);
    }
}
