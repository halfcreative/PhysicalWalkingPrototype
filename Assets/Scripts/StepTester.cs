using UnityEngine;
using UnityEngine.InputSystem;

// Debug tool. J steps the left foot, K the right, stepDistance along the body's forward; Shift steps back.
// Ignored while either foot is mid-swing.
[RequireComponent(typeof(PlayerRig))]
public class StepTester : MonoBehaviour
{
    [SerializeField] FootPlacement leftFoot;
    [SerializeField] FootPlacement rightFoot;
    // 0.3 needs 98.5% of the leg's reach and locks the knee.
    [SerializeField] float stepDistance = 0.2f;

    PlayerRig playerRig;

    void Awake() => playerRig = GetComponent<PlayerRig>();

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float direction = keyboard.shiftKey.isPressed ? -1f : 1f;

        if (keyboard.jKey.wasPressedThisFrame) Step(leftFoot, direction);
        if (keyboard.kKey.wasPressedThisFrame) Step(rightFoot, direction);
    }

    void Step(FootPlacement foot, float direction)
    {
        if (leftFoot.IsStepping || rightFoot.IsStepping) return;

        Vector3 forward = playerRig.GhostHips.forward;
        foot.BeginStep(foot.GroundPoint + forward * (direction * stepDistance), Vector3.up);
        Debug.Log($"Step {foot.name} {direction * stepDistance:+0.00;-0.00} m", this);
    }
}
