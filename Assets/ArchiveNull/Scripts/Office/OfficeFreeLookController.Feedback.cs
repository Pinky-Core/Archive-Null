using UnityEngine;

namespace ArchiveNull.UI
{
    public sealed partial class OfficeFreeLookController
    {
        public float WalkingSpeed { get; private set; }
        public bool HasCarpetFootsteps => carpetSource!=null && carpetSource.clip!=null;
        public AudioClip FootstepClip=>carpetSource!=null ? carpetSource.clip:null;
        private AudioSource carpetSource;
        private FootstepAudioProfile footstepProfile;
        private float walkedDistance, nextStepDistance, walkingEnvelope;
        private int stepIndex;
        private Vector3 feedbackPosition, feedbackVelocity, baseEyePosition, visualEyePosition;
        private Quaternion baseEyeRotation, visualEyeRotation;
        private bool visualPoseApplied;
        private void InitializeWalkingFeedback()
        {
            carpetSource=gameObject.AddComponent<AudioSource>();carpetSource.playOnAwake=false;carpetSource.spatialBlend=0;
            footstepProfile=Resources.Load<FootstepAudioProfile>("OfficeFootsteps");
            if(footstepProfile!=null) carpetSource.clip=footstepProfile.clip;
        }
        public void PlayCarpetStep()
        {
            if(!HasCarpetFootsteps) return;
            carpetSource.pitch=GlobalInputBindings.IsPressed(GameInputAction.Run) ? footstepProfile.runningPitch:footstepProfile.walkingPitch;
            carpetSource.volume=footstepProfile.volume*GlobalPauseMenu.EffectsVolume;
            carpetSource.Play();stepIndex++;
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
            if(!walkingAllowed || _cameraRoot==null) { if(carpetSource!=null) carpetSource.Pause();walkingEnvelope=0;feedbackPosition=feedbackVelocity=Vector3.zero;return; }
            float dt=Mathf.Min(Time.deltaTime,.05f);
            walkedDistance+=WalkingSpeed*dt;
            walkingEnvelope=Mathf.MoveTowards(walkingEnvelope,WalkingSpeed>.05f ? 1 : 0,dt*5);
            if(WalkingSpeed>.05f) {if(!carpetSource.isPlaying) PlayCarpetStep();carpetSource.pitch=GlobalInputBindings.IsPressed(GameInputAction.Run) ? footstepProfile.runningPitch:footstepProfile.walkingPitch;carpetSource.volume=footstepProfile.volume*GlobalPauseMenu.EffectsVolume;}
            else carpetSource.Pause();
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
    }
}
