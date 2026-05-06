using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteInEditMode]
public class Zoom : MonoBehaviour
{
    private Camera cameraComponent;

    public float defaultFOV = 60f;
    public float maxZoomFOV = 15f;

    [Range(0, 1)]
    public float currentZoom;

    public float sensitivity = 1f;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            defaultFOV = cameraComponent.fieldOfView;
        }
    }

    private void Update()
    {
        if (cameraComponent == null)
        {
            return;
        }

        float scrollDelta = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        currentZoom += scrollDelta * sensitivity * 0.001f;
        currentZoom = Mathf.Clamp01(currentZoom);
        cameraComponent.fieldOfView = Mathf.Lerp(defaultFOV, maxZoomFOV, currentZoom);
    }
}
