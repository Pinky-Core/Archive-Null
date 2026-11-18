using UnityEngine;

namespace ArchiveNull.Narrative
{
    public sealed class CaseVoicePlayer : MonoBehaviour
    {
        private static CaseVoicePlayer current;
        private AudioSource source;
        private string resource;
        private bool paused;
        public static bool IsPlaying(string path)=>current!=null && current.resource==path && current.source.isPlaying;
        public static bool IsPaused(string path)=>current!=null && current.resource==path && current.paused;
        public static float Position(string path)=>current!=null && current.resource==path ? current.source.time:0;
        public static float Fraction(string path)=>current!=null && current.resource==path && current.source.clip!=null ? current.source.time/Mathf.Max(.01f,current.source.clip.length):0;
        public static float Progress=>current!=null && current.source.clip!=null ? current.source.time/Mathf.Max(.01f,current.source.clip.length):0;
        public static void Play(string path)
        {
            if(current==null) {current=new GameObject("Case voice playback").AddComponent<CaseVoicePlayer>();current.source=current.gameObject.AddComponent<AudioSource>();current.source.playOnAwake=false;current.source.spatialBlend=0;}
            if(IsPlaying(path)) {current.source.Pause();current.paused=true;return;}
            if(IsPaused(path)) {current.source.UnPause();current.paused=false;return;}
            var clip=Resources.Load<AudioClip>("CaseAudio/"+path);
            if(clip==null) {Debug.LogWarning("Missing case voice: "+path);return;}
            current.source.Stop();current.paused=false;current.resource=path;current.source.clip=clip;current.source.volume=GlobalPauseMenu.VoiceVolume;current.source.Play();
        }
        public static void Seek(string path,float fraction)
        {
            if(current==null || current.resource!=path) Play(path);
            if(current==null || current.resource!=path || current.source.clip==null) return;
            current.source.time=Mathf.Clamp01(fraction)*Mathf.Max(0,current.source.clip.length-.02f);
        }
        public static void Stop(string prefix=null) {if(current!=null && current.source!=null && (prefix==null || current.resource.StartsWith(prefix,System.StringComparison.Ordinal))) {current.source.Stop();current.paused=false;}}
        private void Update() {if(source!=null) source.volume=GlobalPauseMenu.VoiceVolume;}
        private void OnDestroy() {if(current==this) current=null;}
    }
}
