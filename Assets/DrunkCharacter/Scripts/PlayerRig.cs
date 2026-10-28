using UnityEngine;
using UnityEngine.Serialization;

// Places the ghost hips each tick, and owns the rig's measurements. At Awake it measures the legs from the
// joint anchors and checks the ghost skeleton starts aligned with the physical one.
[DefaultExecutionOrder(-40)]
public class PlayerRig : MonoBehaviour
{
    // Cap on the balance shift, m. Past about half a foot length the planted legs can't pull the body back;
    // that needs a step. Uncapped, a hard shove launched the body off its planted leg.
    const float MaxBalanceShift = 0.08f;

    [Header("Physical")]
    [SerializeField] Rigidbody pelvis;
    [SerializeField] Transform thighL, shinL, footL;
    [SerializeField] Transform thighR, shinR, footR;
    [SerializeField] BalanceController balanceController;
    [SerializeField] BalanceSensor balanceSensor;

    [Header("Ghost")]
    [SerializeField] Transform ghostHips;
    [SerializeField] Transform ghostThighL, ghostShinL, ghostFootL;
    [SerializeField] Transform ghostThighR, ghostShinR, ghostFootR;

    [Header("Bind pose tolerances")]
    [SerializeField] float maxBindAngle = 0.5f;     // degrees
    [SerializeField] float maxBindOffset = 0.005f;  // metres

    // Ankle to sole. The IK targets the ankle, so foot targets sit this far above the ground.
    [SerializeField] float footGroundOffset = 0.10f;

    [Header("Balance")]
    // Fraction of the capture point's error the ghost hips are shifted by; see FixedUpdate.
    [SerializeField, Range(0f, 2f), FormerlySerializedAs("hipsOverStance")] float balanceShiftGain = 1f;

    public float FootGroundOffset => footGroundOffset;
    public Transform GhostHips => ghostHips;
    // Hip to ankle along a straight leg, averaged over both legs.
    public float LegLength { get; private set; }
    // How far the real hips sit below the ghost hips this tick. A swinging foot hangs this much low.
    public float HipSag { get; private set; }
    // Horizontal offset of the ghost hips from the real ones this tick.
    public Vector3 GhostHipOffset { get; private set; }

    struct LegMeasurement
    {
        public Vector3 Hip, Knee, Ankle;
        public float ThighLength, ShinLength;
        public float Length => ThighLength + ShinLength;
    }

    void Awake()
    {
        if (!ReferencesAssigned())
        {
            Debug.LogError("PlayerRig: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        if (!JointsPresent())
        {
            enabled = false;
            return;
        }

        LegMeasurement left = MeasureLeg(thighL, shinL, footL);
        LegMeasurement right = MeasureLeg(thighR, shinR, footR);
        LegLength = (left.Length + right.Length) * 0.5f;

        Debug.Log($"PlayerRig L  hip {left.Hip:F3} knee {left.Knee:F3} ankle {left.Ankle:F3}  " +
                  $"thigh {left.ThighLength:F3}  shin {left.ShinLength:F3}  leg {left.Length:F3}", this);
        Debug.Log($"PlayerRig R  hip {right.Hip:F3} knee {right.Knee:F3} ankle {right.Ankle:F3}  " +
                  $"thigh {right.ThighLength:F3}  shin {right.ShinLength:F3}  leg {right.Length:F3}", this);

        if (Mathf.Abs(left.ThighLength - right.ThighLength) > maxBindOffset ||
            Mathf.Abs(left.ShinLength - right.ShinLength) > maxBindOffset)
            Debug.LogWarning($"PlayerRig: legs are asymmetric — thigh {left.ThighLength:F4}/" +
                             $"{right.ThighLength:F4}, shin {left.ShinLength:F4}/{right.ShinLength:F4}", this);

        AssertBindPose(left, right);
    }

    // Height: standingHipHeight above the planted feet, not at the pelvis, so the stance legs always have
    // an error to push against. Measured from the support points, not the ankles, so a foot leaving the
    // ground doesn't lift the target with it.
    //
    // Horizontal: the body's hips, shifted by the capture point's error from the balance target. The planted
    // legs then push the body back over its feet. The capture point, not the hips, because it carries
    // velocity and so damps the push. The swing leg must not feel this shift; FootPlacement adds
    // GhostHipOffset back onto its target.
    //
    // Tilted by the lean, so the stance legs agree with BalanceController's upright torque.
    void FixedUpdate()
    {
        Vector3 hip = HipCentre();
        Vector3 stance = balanceSensor.SupportCentre;
        Vector3 error = (balanceController.BalanceTarget() - balanceSensor.CapturePoint).Flat();
        Vector3 shifted = hip + Vector3.ClampMagnitude(error * balanceShiftGain, MaxBalanceShift);

        ghostHips.SetPositionAndRotation(
            new Vector3(shifted.x, stance.y + balanceController.StandingHipHeight, shifted.z),
            Quaternion.FromToRotation(Vector3.up, balanceController.PelvisUpTarget) * PelvisYaw());

        HipSag = Mathf.Max(0f, ghostHips.position.y - hip.y);
        GhostHipOffset = (shifted - hip).Flat();
    }

    // From the body, never a foot: over a foot, each leg would demand the body be over its own.
    Vector3 HipCentre() =>
        (AnchorOf(thighL, pelvis.transform) + AnchorOf(thighR, pelvis.transform)) * 0.5f;

    // The lower sole, from the ankle anchors. For the gizmo.
    float SupportPlaneY() =>
        Mathf.Min(AnchorOf(footL, shinL).y, AnchorOf(footR, shinR).y) - footGroundOffset;

    // Yaw only, so a pitched pelvis doesn't tilt the solve. Falls back to the up vector when forward is
    // vertical, which jumps ~90° but never gives NaN.
    Quaternion PelvisYaw()
    {
        Vector3 forward = (pelvis.rotation * Vector3.forward).Flat();
        if (forward.sqrMagnitude < 1e-6f)
            forward = (pelvis.rotation * Vector3.up).Flat();

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    LegMeasurement MeasureLeg(Transform thigh, Transform shin, Transform foot)
    {
        LegMeasurement m = default;
        m.Hip = AnchorOf(thigh, pelvis.transform);
        m.Knee = AnchorOf(shin, thigh);
        m.Ankle = AnchorOf(foot, shin);
        m.ThighLength = Vector3.Distance(m.Hip, m.Knee);
        m.ShinLength = Vector3.Distance(m.Knee, m.Ankle);
        return m;
    }

    // The joint's world position. connectedAnchor is in the parent's local space, scale included. Silent
    // on a missing joint; JointsPresent reports it once at Awake.
    static Vector3 AnchorOf(Transform body, Transform parent)
    {
        ConfigurableJoint joint = body.GetComponent<ConfigurableJoint>();
        return joint == null ? parent.position : parent.TransformPoint(joint.connectedAnchor);
    }

    // The ghost must start aligned with the physical rig, or the offset reads as a permanent lean. Awake
    // only: once running, the ghost is meant to diverge. Each ghost bone sits on the joint above its
    // segment (GhostThigh on the hip, and so on).
    void AssertBindPose(LegMeasurement left, LegMeasurement right)
    {
        CheckBone(ghostThighL, thighL, left.Hip);
        CheckBone(ghostShinL, shinL, left.Knee);
        CheckBone(ghostFootL, footL, left.Ankle);
        CheckBone(ghostThighR, thighR, right.Hip);
        CheckBone(ghostShinR, shinR, right.Knee);
        CheckBone(ghostFootR, footR, right.Ankle);
    }

    void CheckBone(Transform ghost, Transform body, Vector3 anchor)
    {
        float angle = Quaternion.Angle(ghost.rotation, body.rotation);
        if (angle > maxBindAngle)
            Debug.LogError($"PlayerRig: {ghost.name} is {angle:F2}° off {body.name} at the bind " +
                           $"pose (max {maxBindAngle}°).", ghost);

        float offset = Vector3.Distance(ghost.position, anchor);
        if (offset > maxBindOffset)
            Debug.LogError($"PlayerRig: {ghost.name} is {offset * 1000f:F1} mm off its joint " +
                           $"anchor {anchor:F3} (max {maxBindOffset * 1000f:F0} mm).", ghost);
    }

    bool ReferencesAssigned() =>
        pelvis && balanceController && balanceSensor && thighL && shinL && footL && thighR && shinR && footR &&
        ghostHips && ghostThighL && ghostShinL && ghostFootL &&
        ghostThighR && ghostShinR && ghostFootR;

    bool JointsPresent()
    {
        Transform[] bodies = { thighL, shinL, footL, thighR, shinR, footR };
        bool ok = true;

        foreach (Transform body in bodies)
        {
            if (body.GetComponent<ConfigurableJoint>() != null) continue;
            Debug.LogError($"PlayerRig: {body.name} has no ConfigurableJoint.", body);
            ok = false;
        }

        return ok;
    }

    // Runs in edit mode too, so alignment can be checked before pressing play.
    void OnDrawGizmos()
    {
        if (!ReferencesAssigned()) return;

        DrawLeg(MeasureLeg(thighL, shinL, footL), ghostThighL, ghostShinL, ghostFootL, thighL, shinL, footL);
        DrawLeg(MeasureLeg(thighR, shinR, footR), ghostThighR, ghostShinR, ghostFootR, thighR, shinR, footR);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(ghostThighL.position, ghostHips.position);
        Gizmos.DrawLine(ghostHips.position, ghostThighR.position);
        Gizmos.DrawWireSphere(ghostHips.position, 0.03f);

        DrawSupportPlane();
    }

    // In play mode the yellow line is standingHipHeight, and stays that length while the body sags.
    void DrawSupportPlane()
    {
        Vector3 hip = HipCentre();
        Vector3 support = new(hip.x, SupportPlaneY(), hip.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(support - Vector3.right * 0.1f, support + Vector3.right * 0.1f);
        Gizmos.DrawLine(support - Vector3.forward * 0.1f, support + Vector3.forward * 0.1f);
        Gizmos.DrawLine(support, ghostHips.position);
    }

    void DrawLeg(LegMeasurement m, Transform ghostThigh, Transform ghostShin, Transform ghostFoot,
                 Transform thigh, Transform shin, Transform foot)
    {
        // Where the leg actually hinges.
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(m.Hip, 0.02f);
        Gizmos.DrawSphere(m.Knee, 0.02f);
        Gizmos.DrawSphere(m.Ankle, 0.02f);

        // The ghost chain, tinted by tracking error.
        DrawBone(ghostThigh, ghostShin, thigh);
        DrawBone(ghostShin, ghostFoot, shin);

        Gizmos.color = TrackingColor(ghostFoot, foot);
        Gizmos.DrawWireSphere(ghostFoot.position, 0.025f);
        Gizmos.DrawLine(ghostFoot.position, ghostFoot.position + ghostFoot.forward * 0.12f);
    }

    void DrawBone(Transform from, Transform to, Transform body)
    {
        Gizmos.color = TrackingColor(from, body);
        Gizmos.DrawLine(from.position, to.position);
        Gizmos.DrawWireSphere(from.position, 0.025f);
    }

    // Green while the body tracks its ghost bone, red at 20° or more behind.
    static Color TrackingColor(Transform ghost, Transform body) =>
        Color.Lerp(Color.green, Color.red,
                   Mathf.InverseLerp(5f, 20f, Quaternion.Angle(ghost.rotation, body.rotation)));
}
