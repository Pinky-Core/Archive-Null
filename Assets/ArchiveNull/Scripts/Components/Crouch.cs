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
    public bool CanStandNow()
    {
        if(!ready) return true;
        Transform rig=controller.transform;
        float radius=controller.radius*Mathf.Max(Mathf.Abs(rig.lossyScale.x),Mathf.Abs(rig.lossyScale.z))*.92f;
        Vector3 center=rig.TransformPoint(standingCenter);
        float half=standingHeight*Mathf.Abs(rig.lossyScale.y)*.5f;
        int count=Physics.OverlapCapsuleNonAlloc(center+Vector3.up*(half-radius),center-Vector3.up*(half-radius-.04f),radius,overlaps,~(1<<31),QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length) return false;
        for(int i=0;i<count;i++) if(!overlaps[i].transform.IsChildOf(rig)) return false;
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
        standingHeight=controller.height;standingCenter=controller.center;standingStep=controller.stepOffset;headY=headToLower.localPosition.y;ready=true;
        movement.speedOverrides.Add(SpeedOverride);
    }
    private float SpeedOverride()=>IsCrouched ? movement.speed*.45f : movement.IsRunning ? movement.runSpeed : movement.speed;
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
    }
}
