using UnityEngine;
using ArchiveNull.Evidence;

// The active locomotion uses CharacterController, not the disabled legacy capsule.
[DefaultExecutionOrder(50)]
public class Crouch : MonoBehaviour
{
    public FirstPersonMovement movement;
    public Transform headToLower;
    public float movementSpeed=2;
    public bool IsCrouched { get; private set; }
    public event System.Action CrouchStart,CrouchEnd;
    private CharacterController controller;
    private float standingHeight,standingStep,headY,amount;
    private Vector3 standingCenter;
    private readonly Collider[] overlaps=new Collider[32];
    private bool ready;
    private SphereCollider headProbe;
    public Collider StandingObstruction {get;private set;}
    public bool CanStandNow()
    {
        StandingObstruction=null;
        if(!ready) return true;
        Transform rig=controller.transform;
        float horizontalScale=Mathf.Max(Mathf.Abs(rig.lossyScale.x),Mathf.Abs(rig.lossyScale.z));
        float verticalScale=Mathf.Abs(rig.lossyScale.y);
        float radius=Mathf.Max(.01f,(controller.radius-controller.skinWidth*.5f)*horizontalScale);
        Vector3 center=rig.TransformPoint(standingCenter);
        float half=standingHeight*verticalScale*.5f;
        Vector3 top=center+rig.up*(half-radius);
        Vector3 currentTop=rig.TransformPoint(controller.center)+rig.up*(controller.height*verticalScale*.5f);
        Vector3 bottom=currentTop-rig.up*radius;
        int count=Physics.OverlapCapsuleNonAlloc(top,bottom,radius,overlaps,~((1<<30)|(1<<31)),QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length) return false;
        Vector3 scale=headProbe.transform.lossyScale;
        headProbe.radius=radius/Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
        int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(bottom,top)/(radius*.5f)));
        for(int i=0;i<count;i++)
        {
            Collider other=overlaps[i];if(other.transform.IsChildOf(rig)) continue;
            // Walls and the floor can already be touching the body. Only a
            // contact pushing the growing head down is a ceiling obstruction.
            for(int step=0;step<=samples;step++)
                if(Physics.ComputePenetration(headProbe,Vector3.Lerp(bottom,top,step/(float)samples),rig.rotation,other,other.transform.position,other.transform.rotation,out var direction,out _)
                    && Vector3.Dot(direction,rig.up)<-.05f) {StandingObstruction=other;return false;}
        }
        return true;
    }
    private void Start()
    {
        movement=GetComponentInParent<FirstPersonMovement>();
        if(movement==null) { enabled=false;return; }
        controller=movement.GetComponentInParent<CharacterController>();
        var camera=movement.GetComponentInChildren<Camera>();
        if(headToLower==null && camera!=null) headToLower=camera.transform;
        if(controller==null || headToLower==null) { enabled=false;return; }
        var probe=new GameObject("Standing head clearance");probe.transform.SetParent(controller.transform,false);headProbe=probe.AddComponent<SphereCollider>();headProbe.enabled=false;
        standingHeight=controller.height;standingCenter=controller.center;standingStep=controller.stepOffset;headY=headToLower.localPosition.y;ready=true;
        movement.speedOverrides.Add(SpeedOverride);
    }
    private float SpeedOverride()=>IsCrouched ? movement.speed*.45f : movement.IsRunning ? movement.runSpeed : movement.speed;
    private void OnEnable() {if(ready && !movement.speedOverrides.Contains(SpeedOverride)) movement.speedOverrides.Add(SpeedOverride);}
    public void SetRequestedCrouch(bool requested)
    {
        if(!ready) return;
        bool next=requested || (IsCrouched && !CanStandNow());
        if(next==IsCrouched) return;
        IsCrouched=next;
        if(next) CrouchStart?.Invoke();else CrouchEnd?.Invoke();
    }
    private void Update()
    {
        if(!ready) return;
        bool blocked=GlobalPauseMenu.IsPaused || EvidenceNotebookUI.IsAnyNotebookOpen || PhoneEvidenceReader.IsAnyOpen || InspectObject.IsAnyInspecting || RuntimeConfirmationDialog.IsOpen;
        if(!blocked) SetRequestedCrouch(GlobalInputBindings.IsPressed(GameInputAction.Crouch));
        amount=Mathf.MoveTowards(amount,IsCrouched ? 1:0,Time.deltaTime*5);
        float reduction=standingHeight*.42f*amount;
        controller.height=Mathf.Max(controller.radius*2,standingHeight-reduction);
        controller.center=standingCenter-Vector3.up*((standingHeight-controller.height)*.5f);
        controller.stepOffset=Mathf.Min(standingStep,controller.height*.3f);
        Vector3 local=headToLower.localPosition;local.y=headY-reduction*controller.transform.lossyScale.y/Mathf.Max(.001f,Mathf.Abs(headToLower.parent.lossyScale.y));headToLower.localPosition=local;
    }
    private void OnDisable()
    {
        if(movement!=null) movement.speedOverrides.Remove(SpeedOverride);
        if(!ready) return;
        controller.height=standingHeight;controller.center=standingCenter;controller.stepOffset=standingStep;
        if(headToLower!=null) {var p=headToLower.localPosition;p.y=headY;headToLower.localPosition=p;}
        IsCrouched=false;
        amount=0;
    }
}
