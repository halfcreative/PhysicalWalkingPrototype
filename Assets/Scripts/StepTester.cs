using UnityEngine;
using UnityEngine.InputSystem;

// Debug tool for Step 7. J steps the left foot, K the right, stepDistance along the body's forward.
// Hold Shift to step backward instead. Refuses while either foot is mid-swing: lifting both is a jump.
public class StepTester : MonoBehaviour
{
    [SerializeField] FootPlacement leftFoot;
    [SerializeField] FootPlacement rightFoot;
    [SerializeField] Transform ghostHips;
    [SerializeField] float stepDistance = 0.2f;   // 0.3 is 98.5% of the leg's reach — see resume-here §5.3

    // BeginStep only sets state that FootPlacement advances in its own FixedUpdate, so calling it
    // from Update is safe.
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float sign = kb.shiftKey.isPressed ? -1f : 1f;

        if (kb.jKey.wasPressedThisFrame) Step(leftFoot, sign);
        if (kb.kKey.wasPressedThisFrame) Step(rightFoot, sign);
    }

    void Step(FootPlacement foot, float sign)
    {
        if (leftFoot.IsStepping || rightFoot.IsStepping) return;

        Vector3 forward = ghostHips.forward;
        foot.BeginStep(foot.FootPos + forward * (sign * stepDistance), Vector3.up);
        Debug.Log($"Step {foot.name} {sign * stepDistance:+0.00;-0.00} m", this);
    }
}
