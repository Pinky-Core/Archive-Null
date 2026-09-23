using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace ArchiveNull.Evidence
{
    // The display is a world-space LCD on the held camera, not a full-screen HUD.
    public sealed class PhysicalEvidenceCamera : MonoBehaviour
    {
        public Camera LensCamera { get; private set; }
        private Camera eyes;
        private RenderTexture feed;
        private Material body;
        private Canvas screen;
        private RawImage image;
        public bool IsMounted { get; set; }
        public bool DisplayIsBlack => image == null || image.color == Color.black;
        public void Initialize(Camera player)
        {
            eyes=player;
            var owner=eyes.GetComponentInParent<FirstPersonMovement>();
            if(owner!=null) foreach(Renderer renderer in owner.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer=31;
            body=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Resources.Load<Shader>("ArchiveTrace")); body.color=new Color(.028f,.033f,.036f);
            TestingToolsController.Piece(transform,Vector3.zero,new Vector3(.38f,.23f,.10f),body);
            TestingToolsController.Piece(transform,new Vector3(.2f,-.01f,.015f),new Vector3(.08f,.24f,.14f),body);
            TestingToolsController.Piece(transform,new Vector3(-.03f,0,.11f),new Vector3(.15f,.15f,.15f),body);
            var panel=new GameObject("LCD",typeof(RectTransform),typeof(Canvas)); panel.transform.SetParent(transform,false); panel.transform.localPosition=new Vector3(-.025f,0,-.052f); panel.transform.localScale=Vector3.one*.0005f;
            screen=panel.GetComponent<Canvas>(); screen.renderMode=RenderMode.WorldSpace; ((RectTransform)panel.transform).sizeDelta=new Vector2(600,360);
            var display=new GameObject("Live image",typeof(RectTransform),typeof(RawImage)); display.transform.SetParent(panel.transform,false);
            image=display.GetComponent<RawImage>(); image.raycastTarget=false;
            var rect=(RectTransform)display.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.sizeDelta=Vector2.zero;
            feed=new RenderTexture(800,480,24,RenderTextureFormat.ARGB32) { name="Camera LCD feed", antiAliasing=1 }; feed.Create(); image.texture=feed;
            var lens=new GameObject("Evidence lens"); lens.transform.SetParent(transform,false); lens.transform.localPosition=new Vector3(-.03f,0,.20f); LensCamera=lens.AddComponent<Camera>(); LensCamera.CopyFrom(eyes); LensCamera.tag="Untagged";
            LensCamera.targetTexture=feed; LensCamera.aspect=800f/480; LensCamera.cullingMask=eyes.cullingMask & ~(1<<31); LensCamera.enabled=false; LensCamera.useOcclusionCulling=false; LensCamera.nearClipPlane=.035f; image.color=Color.black;
            var data=lens.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing=false; data.renderShadows=false;
            foreach(Transform child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
        }
        private void LateUpdate()
        {
            if(LensCamera==null) return;
            bool powered=(IsMounted || EvidenceCameraController.IsAnyCameraModeActive) && !GlobalPauseMenu.IsPaused && !EvidenceNotebookUI.IsAnyNotebookOpen;
            LensCamera.enabled=powered;
            image.color=powered ? Color.white : Color.black;
            // The lens is attached to the physical body, including when mounted on a tripod.
            LensCamera.clearFlags=eyes.clearFlags; LensCamera.backgroundColor=eyes.backgroundColor;
        }
        public void MountHud(GameObject hud)
        {
            if(hud==null || screen==null) return;
            hud.transform.SetParent(screen.transform,false);
            var rect=(RectTransform)hud.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.sizeDelta=Vector2.zero; rect.localPosition=Vector3.zero; rect.localScale=Vector3.one;
            foreach(Transform child in hud.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31;
        }
        private void OnDisable() { if(LensCamera!=null) LensCamera.enabled=false; if(image!=null) image.color=Color.black; }
        private void OnDestroy() { if(LensCamera!=null) Destroy(LensCamera.gameObject); if(feed!=null) { feed.Release(); Destroy(feed); } if(body!=null) Destroy(body); }
    }
}
