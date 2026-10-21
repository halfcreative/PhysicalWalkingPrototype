using UnityEngine;
using UnityEngine.Serialization;

// Balance without stepping: the ankle trim leans the body on its feet for small capture-point errors, and
// the upright torque keeps the upper body stacked over the hips. Stepping is FootPlacement's.
[DefaultExecutionOrder(-50)]
public class BalanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] Rigidbody pelvis;
    [SerializeField] PlayerController playerController;
    [SerializeField] TorsoLean torsoLean;

    [Header("Stance")]
    // Height of the ghost hips above the support plane.
    [SerializeField] float standingHipHeight = 0.85f;

    [Header("Ankle Trim")]
    // Degrees of ankle pitch per metre the capture point sits ahead (+) or behind (−) the balance target.
    [SerializeField, FormerlySerializedAs("ankleTrimGain")] float pitchTrimGain = 50f;
    [SerializeField, FormerlySerializedAs("maxAnkleTrim")] float maxPitchTrim = 12f;
    // The same sideways. Smaller cap: the sole is narrower than it is long.
    [SerializeField] float rollTrimGain = 50f;
    [SerializeField] float maxRollTrim = 8f;

    [Header("Lean")]
    // Metres the balance target moves per unit of net force, so the body leans into the sway and toward
    // where the player is going instead of holding upright against both.
    [SerializeField, FormerlySerializedAs("swayLeanDistance")] float leanDistance = 0.1f;

    [Header("Pelvis Upright Torque")]
    // N·m/rad and N·m·s/rad. Must clear gravity's ~103 N·m/rad on the upper body; 120 is critical damping
    // for the remainder against its ~4.1 kg·m². Re-derive one if you change the other.
    [SerializeField] float uprightSpring = 1000f;
    [SerializeField] float uprightDamper = 120f;
    // Torque caps, N·m. High enough to hold the upper body over the hips, too low to hold the whole body
    // over the feet. Roll is separate: strong sideways torque on one leg throws the body over its foot.
    [SerializeField, FormerlySerializedAs("maxUprightTorque")] float maxPitchTorque = 150f;
    [SerializeField] float maxRollTorque = 150f;
    // Yaw has its own small damper: about yaw it acts on the pelvis alone (~0.1 kg·m²), and above ~20 it
    // overshoots each tick.
    [SerializeField, FormerlySerializedAs("uprightYawDamper")] float yawDamper = 10f;
    // Turns the pelvis toward the heading, N·m/rad. Soft, since it also fights the hips squaring to a
    // split stance. Lower this, not raise the damper, if turns overshoot.
    [SerializeField, FormerlySerializedAs("uprightYawSpring")] float yawSpring = 100f;

    [Header("Counter-Steer")]
    // Extra torque cap, N·m, at full input pushing straight against the way the pelvis is tipping.
    [SerializeField, FormerlySerializedAs("counterTorque")] float counterSteerTorque = 200f;

    public float StandingHipHeight => standingHipHeight;
    // How squarely the player's input pushes against the lean this tick, 0–1.
    public float CounterSteer { get; private set; }
    // Applied by LegDrive, the only writer of the ankle joints.
    public Quaternion AnkleTrim { get; private set; } = Quaternion.identity;
    // Yaw component of last tick's upright torque, N·m.
    public float UprightYawTorque { get; private set; }
    // Which way the pelvis should point up: vertical, tilted by the lean.
    public Vector3 PelvisUpTarget => torsoLean.HipTilt * Vector3.up;

    void FixedUpdate()
    {
        if (playerController.IsFallen)
        {
            AnkleTrim = Quaternion.identity;
            UprightYawTorque = 0f;
            CounterSteer = 0f;
            return;
        }

        Vector3 forward = (pelvis.rotation * Vector3.forward).Flat().normalized;
        Vector3 right = (pelvis.rotation * Vector3.right).Flat().normalized;

        // Toes down (+X) pushes a forward capture point back. +Z lifts the foot's right edge, so a capture
        // point off to the right wants −Z.
        Vector3 error = balanceSensor.CapturePoint - BalanceTarget();
        float pitch = Mathf.Clamp(pitchTrimGain * Vector3.Dot(error, forward), -maxPitchTrim, maxPitchTrim);
        float roll = Mathf.Clamp(-rollTrimGain * Vector3.Dot(error, right), -maxRollTrim, maxRollTrim);
        AnkleTrim = Quaternion.Euler(pitch, 0f, roll);

        HoldPelvisUpright(forward, right);
    }

    // Where the body holds its capture point: over the support, moved by the net force and the hip shift.
    public Vector3 BalanceTarget() =>
        balanceSensor.SupportCentre + playerController.LeanForceWorld * leanDistance + torsoLean.HipShift;

    // A PD torque on the pelvis toward PelvisUpTarget and the heading. The pelvis is the root body, so
    // this is the one place AddTorque is used; everything behind a joint goes through its drive.
    void HoldPelvisUpright(Vector3 forward, Vector3 right)
    {
        Vector3 upTarget = PelvisUpTarget;
        Vector3 pelvisUp = pelvis.rotation * Vector3.up;
        Vector3 tiltAxis = Vector3.Cross(pelvisUp, upTarget);
        Vector3 yawRate = Vector3.Project(pelvis.angularVelocity, Vector3.up);
        Vector3 tiltRate = pelvis.angularVelocity - yawRate;
        float yawError = Vector3.SignedAngle(forward, playerController.Heading * Vector3.forward, Vector3.up);
        Vector3 torque = tiltAxis * uprightSpring - tiltRate * uprightDamper
                       + Vector3.up * (yawError * Mathf.Deg2Rad * yawSpring) - yawRate * yawDamper;

        Vector3 pitchTorque = Vector3.Project(torque, right);
        Vector3 rollAndYawTorque = torque - pitchTorque;

        // Input against the way the pelvis is tipping raises both caps.
        Vector3 tipDirection = (pelvisUp - upTarget).Flat();
        CounterSteer = tipDirection.sqrMagnitude > 1e-4f
            ? Mathf.Clamp01(Vector3.Dot(playerController.MoveInputWorld, -tipDirection.normalized))
            : 0f;
        float extraCap = counterSteerTorque * CounterSteer;

        Vector3 applied = Vector3.ClampMagnitude(pitchTorque, maxPitchTorque + extraCap)
                        + Vector3.ClampMagnitude(rollAndYawTorque, maxRollTorque + extraCap);
        pelvis.AddTorque(applied, ForceMode.Force);
        UprightYawTorque = applied.y;
    }
}
