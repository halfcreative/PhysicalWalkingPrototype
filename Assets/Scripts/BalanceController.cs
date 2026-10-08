using UnityEngine;

// The two balance strategies that don't take a step: the ankle trim, which leans the body on its feet
// for small capture-point errors, and the pelvis upright torque, which keeps the upper body stacked
// over the hips. Stepping, for everything larger, is FootPlacement's.
[DefaultExecutionOrder(-50)]
public class BalanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] Rigidbody pelvis;
    [SerializeField] FootPlacement leftFoot;
    [SerializeField] FootPlacement rightFoot;

    [Header("Stance")]
    // Where PlayerRig puts the ghost hips, measured up from the support plane.
    [SerializeField] float standingHipHeight = 0.85f;

    [Header("Ankle Trim")]
    // Degrees of ankle pitch per metre the capture point sits ahead of (+) or behind (−) the planted
    // soles, and the most it may pitch either way.
    [SerializeField] float ankleTrimGain = 50f;
    [SerializeField] float maxAnkleTrim = 12f;
    // The same sideways: degrees of ankle roll per metre the capture point sits right (+) or left (−)
    // of the planted soles. Smaller cap than pitch: the ankle rolls ±15°, and the sole is narrower than
    // it is long, so there is less of it to roll the centre of pressure across.
    [SerializeField] float rollTrimGain = 50f;
    [SerializeField] float maxRollTrim = 8f;

    [Header("Pelvis Upright Torque")]
    // Real N·m per radian and N·m·s per radian, applied with ForceMode.Force. Gravity tips the upper
    // body over the hips at ~103 N·m/rad, and the hip drives don't resist that, so the spring must
    // clear it with margin. 120 is critical damping for the ~900 left over against the ~4.1 kg·m²
    // upper body: 2·√(897 × 4.1) ≈ 120. Change one and re-derive the other.
    [SerializeField] float uprightSpring = 1000f;
    [SerializeField] float uprightDamper = 120f;
    // Caps the upright torque so it can hold the upper body over the hips but not the whole body
    // over the feet. Tipping the full 62 kg about the ankles costs ~580 N·m/rad, i.e. ~150 N·m at 15°.
    // Standing needs ~80. Uncapped, nothing could knock the character over.
    [SerializeField] float maxUprightTorque = 150f;
    // The same cap for sideways tilt (roll), separately. A forward catch wants a strong pitch assist to
    // keep the body from folding over its stepping legs. The same strength sideways, standing on one leg,
    // tipped the whole body over its stance foot and threw it sideways.
    [SerializeField] float maxRollTorque = 150f;
    // Damps the pelvis's yaw rate, N·m·s per radian: no spring, so it holds no heading, it only slows a
    // turn. A swing leg's reaction spins the pelvis (−90 °/s in 0.2 s on a 2 m/s catch without this).
    // Kept small on purpose: about yaw the damper sees the pelvis alone, ~0.1 kg·m², and above ~20 it
    // overshoots each 0.01 s tick and buzzes. See HoldPelvisUpright.
    [SerializeField] float uprightYawDamper = 10f;

    public float StandingHipHeight => standingHipHeight;

    // Published rather than applied, so that LegDrive stays the only thing writing an ankle joint.
    // It composes into the foot rotation there.
    public Quaternion AnkleTrim { get; private set; } = Quaternion.identity;
    // The yaw part of last tick's upright torque, N·m, for diagnostics.
    public float UprightYawTorque { get; private set; }

    void FixedUpdate()
    {
        // Read off the feet rather than a fixed offset, so it still means something once the feet have
        // moved. FootPlacement runs later in the tick, so this is last tick's stance, which is fine
        // for a trim.
        Vector3 forward = (pelvis.rotation * Vector3.forward).Flat().normalized;
        Vector3 right = (pelvis.rotation * Vector3.right).Flat().normalized;
        Vector3 error = balanceSensor.CapturePoint - StanceCentre();

        // Pitch: toes down (+X) presses the front of the sole and pushes a forward capture point back.
        // Roll: the same on the other axis. +Z lifts the foot's right edge, so a capture point off to the
        // right wants −Z: the right edge pressed down, pushing the body back left.
        //
        // Measured in the pelvis's frame and applied about each foot's own axes. Planted feet keep their
        // own facing, up to turnStepAngle off the body's, so the two frames can disagree by that much.
        float pitch = Mathf.Clamp(ankleTrimGain * Vector3.Dot(error, forward), -maxAnkleTrim, maxAnkleTrim);
        float roll = Mathf.Clamp(-rollTrimGain * Vector3.Dot(error, right), -maxRollTrim, maxRollTrim);
        AnkleTrim = Quaternion.Euler(pitch, 0f, roll);

        HoldPelvisUpright();
    }

    // The planted soles: both while both are down, otherwise the one carrying the body.
    public Vector3 StanceCentre()
    {
        if (leftFoot.IsStepping) return rightFoot.SupportPoint;
        if (rightFoot.IsStepping) return leftFoot.SupportPoint;
        return (leftFoot.SupportPoint + rightFoot.SupportPoint) * 0.5f;
    }

    // A PD torque that tips the pelvis back toward vertical. AddTorque is legitimate here and nowhere
    // else in this system: the pelvis is the root body, with no joint above it to absorb the torque.
    // Anything behind a joint goes through targetRotation.
    //
    // Reads the Rigidbody's rotation rather than the transform's, which can be interpolated away from
    // the physics pose. Cross gives sin(tilt) about the correcting axis, so it weakens past 90° — by
    // then the character has already fallen, so that's fine for now.
    //
    // Pitch (about the body's right) and roll (about its forward) are capped separately; see
    // maxRollTorque.
    //
    // The tilt damper doesn't act on yaw. Its 120 is sized for the upper body tipping (~4 kg·m²);
    // about yaw it met the pelvis alone (~0.1 kg·m²), overshot every tick and flipped the pelvis's yaw
    // rate ±80°/s at 50 Hz, held to that only by the torque cap. Yaw gets its own, much smaller damper.
    void HoldPelvisUpright()
    {
        Vector3 tiltAxis = Vector3.Cross(pelvis.rotation * Vector3.up, Vector3.up);
        Vector3 yawRate = Vector3.Project(pelvis.angularVelocity, Vector3.up);
        Vector3 tiltRate = pelvis.angularVelocity - yawRate;
        Vector3 torque = tiltAxis * uprightSpring - tiltRate * uprightDamper - yawRate * uprightYawDamper;

        Vector3 right = (pelvis.rotation * Vector3.right).Flat().normalized;
        Vector3 pitch = Vector3.Project(torque, right);
        Vector3 rest = torque - pitch;

        Vector3 applied = Vector3.ClampMagnitude(pitch, maxUprightTorque) + Vector3.ClampMagnitude(rest, maxRollTorque);
        pelvis.AddTorque(applied, ForceMode.Force);
        UprightYawTorque = applied.y;
    }
}
