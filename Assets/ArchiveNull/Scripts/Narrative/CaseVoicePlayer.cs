using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArchiveNull.Narrative
{
    public sealed class CaseVoicePlayer : MonoBehaviour
    {
        private static CaseVoicePlayer current;
        private AudioSource source;
        private string resource;
        public static bool IsPlaying(string path)=>current!=null && current.resource==path && current.source.isPlaying;
        public static float Progress=>current!=null && current.source.clip!=null ? current.source.time/Mathf.Max(.01f,current.source.clip.length):0;
        public static void Play(string path)
        {
            if(current==null) {current=new GameObject("Case voice playback").AddComponent<CaseVoicePlayer>();current.source=current.gameObject.AddComponent<AudioSource>();current.source.playOnAwake=false;current.source.spatialBlend=0;}
            if(IsPlaying(path)) {Stop();return;}
            var clip=Resources.Load<AudioClip>("CaseAudio/"+path);
            if(clip==null) {Debug.LogWarning("Missing case voice: "+path);return;}
            current.resource=path;current.source.clip=clip;current.source.volume=GlobalPauseMenu.VoiceVolume;current.source.Play();
        }
        public static void Stop(string prefix=null) {if(current!=null && current.source!=null && (prefix==null || current.resource.StartsWith(prefix,System.StringComparison.Ordinal))) current.source.Stop();}
        private void Update() {if(source!=null) source.volume=GlobalPauseMenu.VoiceVolume;}
        private void OnDestroy() {if(current==this) current=null;}
    }
    public sealed class VoiceMessageButton : MonoBehaviour
    {
        public TMP_Text Label;
        public string Resource,Transcript;
        private void Update()
        {
            bool playing=CaseVoicePlayer.IsPlaying(Resource);
            Label.text=(playing ? "■ Detener · "+Mathf.RoundToInt(CaseVoicePlayer.Progress*100)+"%" : "▶ Reproducir nota de voz")+"\n<size=14>"+Transcript+"</size>";
        }
    }
}
