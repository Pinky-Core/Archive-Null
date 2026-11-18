using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Narrative
{
    public sealed class VoiceMessageButton : MonoBehaviour
    {
        public string Resource,Transcript;
        public bool TranscriptVisible=>transcription!=null && transcription.gameObject.activeSelf;
        public Slider ProgressSlider {get;private set;}
        private TMP_Text duration,transcription;
        private Image[] bars;
        private VoicePlaybackIcon icon;
        private float clipLength;
        private LayoutElement layout;
        private static readonly Color Unplayed=new(.52f,.68f,.62f),Played=new(.36f,.94f,.75f);
        public static VoiceMessageButton Create(RectTransform bubble,LayoutElement row,string resource,string transcript,string time,bool outgoing)
        {
            var play=Rect("Play pause",bubble,new Vector2(14,-12),new Vector2(48,48));
            var hit=play.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var button=play.gameObject.AddComponent<Button>();button.targetGraphic=hit;
            var voice=play.gameObject.AddComponent<VoiceMessageButton>();voice.Resource=resource;voice.Transcript=transcript;voice.layout=row;
            var iconRect=Rect("Playback icon",play,Vector2.zero,new Vector2(48,48));voice.icon=iconRect.gameObject.AddComponent<VoicePlaybackIcon>();voice.icon.raycastTarget=false;
            var clip=Resources.Load<AudioClip>("CaseAudio/"+resource);voice.clipLength=clip!=null ? clip.length:0;
            button.interactable=clip!=null;button.onClick.AddListener(()=>CaseVoicePlayer.Play(resource));
            var wave=Rect("Voice waveform",bubble,new Vector2(76,-22),new Vector2(-96,27));wave.anchorMax=new Vector2(1,1);
            var seekArea=wave.gameObject.AddComponent<Image>();seekArea.color=Color.clear;
            voice.bars=new Image[28];float[] amplitudes=Waveform(clip,voice.bars.Length);
            for(int i=0;i<voice.bars.Length;i++)
            {
                var bar=Rect("Wave "+i,wave,Vector2.zero,new Vector2(3,4+amplitudes[i]*23));bar.anchorMin=bar.anchorMax=new Vector2(i/(float)(voice.bars.Length-1),.5f);bar.pivot=new Vector2(.5f,.5f);bar.anchoredPosition=Vector2.zero;
                voice.bars[i]=bar.gameObject.AddComponent<Image>();voice.bars[i].color=Unplayed;voice.bars[i].raycastTarget=false;
            }
            var marker=Rect("Playback position",wave,Vector2.zero,new Vector2(4,30));marker.pivot=new Vector2(.5f,.5f);var markerImage=marker.gameObject.AddComponent<Image>();markerImage.color=Played;markerImage.raycastTarget=false;
            var slider=wave.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.handleRect=marker;slider.targetGraphic=markerImage;slider.onValueChanged.AddListener(value=>CaseVoicePlayer.Seek(resource,value));voice.ProgressSlider=slider;
            voice.duration=Label(bubble,"Duration",Format(voice.clipLength),12,new Vector2(17,-62),new Vector2(76,20));
            var transcriptButton=Rect("Transcript toggle",bubble,new Vector2(102,-62),new Vector2(125,23));transcriptButton.gameObject.AddComponent<Image>().color=Color.clear;
            var toggle=transcriptButton.gameObject.AddComponent<Button>();toggle.targetGraphic=transcriptButton.GetComponent<Image>();
            var toggleLabel=Label(transcriptButton,"Caption","Transcripción",12,Vector2.zero,new Vector2(125,22));toggleLabel.color=new Color(.72f,.86f,.83f);toggle.onClick.AddListener(voice.ToggleTranscript);
            var stamp=Label(bubble,"Timestamp",time+(outgoing ? "  ✓✓":""),11,new Vector2(-12,-65),new Vector2(65,20));stamp.rectTransform.anchorMin=stamp.rectTransform.anchorMax=new Vector2(1,1);stamp.rectTransform.pivot=new Vector2(1,1);stamp.alignment=TextAlignmentOptions.Right;
            voice.transcription=Label(bubble,"Transcript",transcript,14,new Vector2(14,-95),new Vector2(-28,135));voice.transcription.rectTransform.anchorMax=new Vector2(1,1);voice.transcription.textWrappingMode=TextWrappingModes.Normal;voice.transcription.gameObject.SetActive(false);
            row.preferredHeight=94;return voice;
        }
        public void ToggleTranscript()
        {
            bool show=!TranscriptVisible;transcription.gameObject.SetActive(show);
            float width=Mathf.Max(150,((RectTransform)transcription.transform.parent).rect.width-28);
            float height=transcription.GetPreferredValues(Transcript,width,0).y;
            transcription.rectTransform.sizeDelta=new Vector2(-28,height+6);layout.preferredHeight=show ? 105+height:94;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)layout.transform.parent);
        }
        private void Update()
        {
            bool playing=CaseVoicePlayer.IsPlaying(Resource);icon.PausedGlyph=playing;
            float fraction=CaseVoicePlayer.Fraction(Resource);ProgressSlider.SetValueWithoutNotify(fraction);
            duration.text=playing || CaseVoicePlayer.IsPaused(Resource) ? Format(CaseVoicePlayer.Position(Resource))+" / "+Format(clipLength):Format(clipLength);
            for(int i=0;i<bars.Length;i++) bars[i].color=i/(float)bars.Length<fraction ? Played:Unplayed;
        }
        private static float[] Waveform(AudioClip clip,int count)
        {
            var levels=new float[count];
            if(clip!=null && clip.loadType==AudioClipLoadType.DecompressOnLoad && clip.loadState==AudioDataLoadState.Loaded)
            {
                var samples=new float[clip.samples*clip.channels];
                if(clip.GetData(samples,0)) for(int i=0;i<count;i++) {float peak=0;int end=(i+1)*samples.Length/count;for(int j=i*samples.Length/count;j<end;j+=8) peak=Mathf.Max(peak,Mathf.Abs(samples[j]));levels[i]=Mathf.Clamp01(Mathf.Sqrt(peak));}
            }
            for(int i=0;i<count;i++) levels[i]=Mathf.Max(.1f,levels[i]);return levels;
        }
        private static string Format(float seconds) {int value=Mathf.Max(0,Mathf.FloorToInt(seconds));return value/60+":"+(value%60).ToString("D2");}
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        private static TMP_Text Label(Transform parent,string name,string value,float size,Vector2 position,Vector2 dimensions)
        {
            var rect=Rect(name,parent,position,dimensions);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.fontSize=size;text.color=new Color(.82f,.9f,.86f);text.raycastTarget=false;return text;
        }
    }
    public sealed class VoicePlaybackIcon : MaskableGraphic
    {
        private bool pause;
        public bool PausedGlyph {get=>pause;set {if(pause==value)return;pause=value;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Rect rect=rectTransform.rect;Vector2 center=rect.center;float radius=Mathf.Min(rect.width,rect.height)*.46f;
            mesh.AddVert(center,new Color(.06f,.13f,.11f,.5f),Vector2.zero);
            for(int i=0;i<=40;i++){float angle=i*Mathf.PI*2/40;mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,new Color(.06f,.13f,.11f,.5f),Vector2.zero);if(i>0)mesh.AddTriangle(0,i,i+1);}
            if(pause) {Quad(mesh,center+new Vector2(-8,-10),new Vector2(5,20));Quad(mesh,center+new Vector2(3,-10),new Vector2(5,20));}
            else {int first=mesh.currentVertCount;mesh.AddVert(center+new Vector2(-5,-11),Color.white,Vector2.zero);mesh.AddVert(center+new Vector2(-5,11),Color.white,Vector2.zero);mesh.AddVert(center+new Vector2(11,0),Color.white,Vector2.zero);mesh.AddTriangle(first,first+1,first+2);}
        }
        private static void Quad(VertexHelper mesh,Vector2 corner,Vector2 size)
        {
            int first=mesh.currentVertCount;mesh.AddVert(corner,Color.white,Vector2.zero);mesh.AddVert(corner+Vector2.up*size.y,Color.white,Vector2.zero);mesh.AddVert(corner+size,Color.white,Vector2.zero);mesh.AddVert(corner+Vector2.right*size.x,Color.white,Vector2.zero);mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
