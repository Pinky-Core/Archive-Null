using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Depth is cleared for the held object; scene geometry cannot cut through it.
public sealed class InspectionForeground : MonoBehaviour
{
    private const int Layer=30;
    private readonly Dictionary<Transform,int> layers=new();
    private Camera eyes,foreground;
    private int originalMask;
    public bool IsActive=>foreground!=null;
    public void Begin(Camera camera,Transform target)
    {
        End();eyes=camera;originalMask=eyes.cullingMask;
        foreach(Transform child in target.GetComponentsInChildren<Transform>(true)) { layers[child]=child.gameObject.layer;child.gameObject.layer=Layer; }
        var host=new GameObject("Inspected object camera");host.transform.SetParent(eyes.transform,false);
        foreground=host.AddComponent<Camera>();foreground.CopyFrom(eyes);foreground.tag="Untagged";
        foreground.cullingMask=1<<Layer;foreground.nearClipPlane=.01f;foreground.farClipPlane=20;
        foreground.targetTexture=null;foreground.useOcclusionCulling=false;
        var data=foreground.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;
        eyes.cullingMask&=~(1<<Layer);eyes.GetUniversalAdditionalCameraData().cameraStack.Add(foreground);
    }
    public void End()
    {
        foreach(var entry in layers) if(entry.Key!=null) entry.Key.gameObject.layer=entry.Value;
        layers.Clear();
        if(eyes!=null) { eyes.cullingMask=originalMask;eyes.GetUniversalAdditionalCameraData().cameraStack.Remove(foreground); }
        if(foreground!=null) {foreground.enabled=false;Destroy(foreground.gameObject);}
        eyes=null;foreground=null;
    }
    private void LateUpdate() { if(foreground!=null && eyes!=null) foreground.fieldOfView=eyes.fieldOfView; }
    private void OnDisable()=>End();
}
