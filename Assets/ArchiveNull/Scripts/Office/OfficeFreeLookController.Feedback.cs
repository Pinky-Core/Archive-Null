using UnityEngine;

namespace ArchiveNull.UI
{
    public sealed partial class OfficeFreeLookController
    {
        public float WalkingSpeed { get; private set; }
        public bool HasCarpetFootsteps => carpetSource!=null && carpetSteps!=null;
        private AudioSource carpetSource;
        private AudioClip[] carpetSteps;
        private float walkedDistance, nextStepDistance, walkingEnvelope;
        private int stepIndex;
        private Vector3 feedbackPosition, feedbackVelocity, baseEyePosition, visualEyePosition;
        private Quaternion baseEyeRotation, visualEyeRotation;
        private bool visualPoseApplied;
        private void InitializeWalkingFeedback()
        {
            carpetSource=gameObject.AddComponent<AudioSource>();carpetSource.playOnAwake=false;carpetSource.spatialBlend=0;
            carpetSteps=new AudioClip[6];
            const int rate=22050, length=4410;
            for(int variant=0;variant<carpetSteps.Length;variant++)
            {
                var samples=new float[length];float filtered=0;uint random=(uint)(variant+12345);
                for(int i=0;i<length;i++)
                {
                    random=1664525*random+1013904223;
                    float noise=(random/(float)uint.MaxValue)*2-1;
                    filtered=filtered*.88f+noise*.12f;
                    float time=i/(float)rate, envelope=Mathf.Min(1,time/.015f)*Mathf.Pow(1-time/.2f,2);
                    float thud=Mathf.Sin(time*2*Mathf.PI*(75+variant*4))*Mathf.Exp(-time*34)*.12f;
                    samples[i]=(filtered*.65f+thud)*envelope;
                }
                carpetSteps[variant]=AudioClip.Create("Office carpet step "+variant,length,1,rate,false);carpetSteps[variant].SetData(samples,0);
            }
        }
        public void PlayCarpetStep()
        {
            carpetSource.pitch=.96f+(stepIndex%3)*.035f;
            carpetSource.PlayOneShot(carpetSteps[stepIndex++%carpetSteps.Length],.6f*GlobalPauseMenu.EffectsVolume);
        }
        private void RestoreWalkingPose()
        {
            if(!visualPoseApplied || _cameraRoot==null) return;
            if((_cameraRoot.position-visualEyePosition).sqrMagnitude<.000001f) _cameraRoot.position=baseEyePosition;
            if(Quaternion.Angle(_cameraRoot.rotation,visualEyeRotation)<.01f) _cameraRoot.rotation=baseEyeRotation;
            visualPoseApplied=false;
        }
        private void LateUpdate()
        {
            bool walkingAllowed=_freeFlightEnabled && CanMoveFreely() && !IsUiBlockingLook() && (_computerFocus==null || !_computerFocus.IsTransitioning);
            if(!walkingAllowed || _cameraRoot==null) { walkingEnvelope=0;feedbackPosition=feedbackVelocity=Vector3.zero;return; }
            float dt=Mathf.Min(Time.deltaTime,.05f);
            walkedDistance+=WalkingSpeed*dt;
            walkingEnvelope=Mathf.MoveTowards(walkingEnvelope,WalkingSpeed>.05f ? 1 : 0,dt*5);
            if(WalkingSpeed>.05f && walkedDistance>=nextStepDistance) { PlayCarpetStep();nextStepDistance=walkedDistance+(WalkingSpeed>3 ? 1.6f : 1.25f); }
            if(WalkingSpeed<=.05f) nextStepDistance=walkedDistance+.15f;
            float stride=walkedDistance/1.25f*Mathf.PI, weight=Mathf.Sin(stride), impact=Mathf.Pow(Mathf.Abs(Mathf.Cos(stride)),8);
            float intensity=PlayerPrefs.GetFloat("archive.headbob.intensity",.65f);
            Vector3 target=new Vector3(weight*.004f,(impact-.27f)*-.008f,0)*walkingEnvelope;
            target.y+=Mathf.Sin(Time.time*1.55f)*.004f*(1-walkingEnvelope*.65f);
            feedbackPosition=Vector3.SmoothDamp(feedbackPosition,target*intensity,ref feedbackVelocity,.085f,Mathf.Infinity,dt);
            baseEyePosition=_cameraRoot.position;baseEyeRotation=_cameraRoot.rotation;
            _cameraRoot.position+=baseEyeRotation*feedbackPosition;
            _cameraRoot.rotation*=Quaternion.Euler((.12f*Mathf.Sin(stride*2+.7f)+.42f*impact)*walkingEnvelope*intensity,0,weight*.6f*walkingEnvelope*intensity);
            visualEyePosition=_cameraRoot.position;visualEyeRotation=_cameraRoot.rotation;visualPoseApplied=true;
        }
        private void OnDisable() { RestoreWalkingPose();if(carpetSource!=null) carpetSource.Stop(); }
        private void OnDestroy() { if(carpetSteps!=null) foreach(var clip in carpetSteps) if(clip!=null) Destroy(clip); }
    }
}
