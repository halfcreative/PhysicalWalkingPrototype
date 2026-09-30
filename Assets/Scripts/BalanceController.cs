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

    public float StandingHipHeight => standingHipHeight;

    // Published rather than applied, so that LegDrive stays the only thing writing an ankle joint.
    // It composes into the foot rotation there.
    public Quaternion AnkleTrim { get; private set; } = Quaternion.identity;

    void FixedUpdate()
    {
        // Read off the feet rather than a fixed offset, so it still means something once the feet have
        // moved. FootPlacement runs later in the tick, so this is last tick's stance, which is fine
        // for a trim.
        Vector3 forward = (pelvis.rotation * Vector3.forward).Flat().normalized;
        float error = Vector3.Dot(balanceSensor.CapturePoint - StanceCentre(), forward);

        float pitch = Mathf.Clamp(ankleTrimGain * error, -maxAnkleTrim, maxAnkleTrim);
        AnkleTrim = Quaternion.Euler(pitch, 0f, 0f);

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
    void HoldPelvisUpright()
    {
        Vector3 tiltAxis = Vector3.Cross(pelvis.rotation * Vector3.up, Vector3.up);
        Vector3 torque = tiltAxis * uprightSpring - pelvis.angularVelocity * uprightDamper;
        pelvis.AddTorque(Vector3.ClampMagnitude(torque, maxUprightTorque), ForceMode.Force);
    }
}
