using UnityEngine;

[DefaultExecutionOrder(1000), DisallowMultipleComponent]
public sealed class FirstPersonHeadBob : MonoBehaviour
{
    private FirstPersonMovement movement;
    private CharacterController controller;
    private Transform pivot, view;
    private Vector3 previousPosition, previousVelocity, position, positionDamping, angles, angularDamping;
    private float stride, envelope, previousYaw, turnRate, sceneScale=1;

    private void Start()
    {
        movement = GetComponent<FirstPersonMovement>();
        controller = GetComponent<CharacterController>();
        Camera camera = GetComponentInChildren<Camera>();
        if (camera == null) return;
        view = camera.transform;
        pivot = new GameObject("Head bob pivot").transform;
        pivot.SetParent(view.parent, false);
        view.SetParent(pivot, false);
        sceneScale=ArchiveNull.Evidence.TestingToolsController.PlayerScale(transform,view);
        previousPosition = transform.position;
        previousYaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (pivot == null) return;
        float dt = Mathf.Min(Time.deltaTime, .05f);
        bool active = dt > 0 && movement != null && movement.enabled && controller != null &&
            Cursor.lockState == CursorLockMode.Locked && !GlobalPauseMenu.IsPaused &&
            !InspectObject.IsAnyInspecting && !ArchiveNull.UI.MemoryLoadingScreen.IsBusy &&
            !ArchiveNull.UI.SceneRebuildTransition.IsBusy;
        Vector3 delta = transform.position - previousPosition;
        float yaw = transform.eulerAngles.y;
        float yawDelta = Mathf.DeltaAngle(previousYaw, yaw);
        previousPosition = transform.position;
        previousYaw = yaw;
        if (!active || delta.sqrMagnitude > 1f)
        {
            previousVelocity = Vector3.zero;
            if (Time.timeScale == 0) return;
            ResetMotion();
            return;
        }

        // Sample the controller's fixed-step velocity, rather than alternating between
        // zero and a full physics step on successive render frames.
        Vector3 velocity = Vector3.Lerp(previousVelocity, controller.velocity, 1 - Mathf.Exp(-10 * dt));
        Vector3 acceleration = transform.InverseTransformDirection((velocity - previousVelocity) / dt);
        previousVelocity = velocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        bool stepping = controller.isGrounded && speed > .12f;
        envelope = Mathf.MoveTowards(envelope, stepping ? Mathf.Clamp01(speed / 2.2f) : 0, dt * 5);
        float pace=Mathf.Clamp01(speed/Mathf.Max(.1f,movement.IsRunning ? movement.runSpeed : movement.speed));
        stride += stepping ? dt * Mathf.Lerp(1.2f,movement.IsRunning ? 2.6f : 1.8f,pace) * Mathf.PI : 0;
        turnRate = Mathf.Lerp(turnRate, yawDelta / dt, 1 - Mathf.Exp(-9 * dt));
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        float run = movement.IsRunning ? 1.35f : 1;
        float breath=Mathf.Sin(Time.time * 1.55f);
        // A chest-mounted camera rolls with each transfer of weight. Small, asymmetric
        // vertical impacts ride on that movement; no constant up/down eye-level sine.
        float weight = Mathf.Sin(stride);
        float impact = Mathf.Pow(Mathf.Abs(Mathf.Cos(stride)), 8);
        Vector3 targetPosition = new Vector3(weight * .0035f, (impact - .27f) * -.009f, Mathf.Sin(stride * 2 + .8f) * .003f) * envelope * run;
        targetPosition.z -= Mathf.Clamp(acceleration.z * .001f, -.012f, .012f);
        Vector3 targetAngles = new Vector3(
            (.12f * Mathf.Sin(stride * 2 + .7f) + .42f * impact) * envelope * run + Mathf.Clamp(acceleration.z * .055f, -.9f, .9f),
            weight * .12f * envelope - Mathf.Clamp(turnRate * .006f, -1.2f, 1.2f),
            weight * .6f * envelope * run - Mathf.Clamp(localVelocity.x * .32f + turnRate * .009f, -2f, 2f));
        targetPosition.y += breath * .008f * (1 - envelope * .65f);
        targetPosition.z += Mathf.Sin(Time.time * 1.55f + .6f) * .003f * (1 - envelope);
        targetAngles.x += breath * .42f * (1 - envelope * .65f);
        targetPosition *= sceneScale;
        float intensity = Mathf.Clamp01(PlayerPrefs.GetFloat("archive.headbob.intensity", .65f));
        // Keep the LCD readable when the player deliberately raises the camera.
        float aim = ArchiveNull.Evidence.EvidenceCameraController.IsAnyCameraModeActive ? .22f : 1;
        position = Vector3.SmoothDamp(position, targetPosition * intensity * aim, ref positionDamping, .085f, Mathf.Infinity, dt);
        angles = Vector3.SmoothDamp(angles, targetAngles * intensity * aim, ref angularDamping, .11f, Mathf.Infinity, dt);
        pivot.localRotation = Quaternion.Euler(angles);
        Vector3 localOffset = pivot.parent != null ? pivot.parent.InverseTransformVector(transform.rotation * position) : position;
        // Rotate around the view itself, not around the character's feet. Keep the
        // camera's local position intact so crouching continues to own its height.
        pivot.localPosition = localOffset + view.localPosition - pivot.localRotation * view.localPosition;
    }

    private void ResetMotion()
    {
        position = positionDamping = angles = angularDamping = Vector3.zero;
        envelope = turnRate = 0;
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
    }
    private void OnDisable() { if (pivot != null) ResetMotion(); }
}
