using UnityEngine;

// Places the ghost hips each tick, and owns the rig's measurements. At Awake it measures the leg
// chain from the joint anchors — which are the truth, not the transform positions — and checks that
// the ghost skeleton starts indistinguishable from the physical one.
[DefaultExecutionOrder(-40)]
public class PlayerRig : MonoBehaviour
{
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

    // Ankle to sole. The IK targets the ankle, so FootPlacement lifts every target by this much.
    [SerializeField] float footGroundOffset = 0.10f;

    [Header("Balance")]
    // How hard the standing legs push the capture point back over the middle of the planted feet, as
    // the fraction of that error the ghost hips are shifted by. See FixedUpdate.
    [SerializeField, Range(0f, 2f)] float hipsOverStance = 1f;

    public float FootGroundOffset => footGroundOffset;
    public Transform GhostHips => ghostHips;

    // Hip to ankle along a straight leg (thigh + shin), averaged over both legs at Awake. The reach
    // clamp and the stride formula read it.
    public float LegLength { get; private set; }

    // How far the real hips sit below the ghost hips, this tick. The legs copy the ghost's joint angles,
    // so a swinging foot hangs this much below its target. ~3.5 cm standing, ~7 cm in single support.
    public float HipSag { get; private set; }

    // How far (horizontally) the ghost hips sit from the real ones this tick; see FixedUpdate.
    public Vector3 GhostHipOffset { get; private set; }

    // Hip, knee and ankle in world space, read off the joint anchors.
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

    // The ghost hips take their height from the support plane rather than the pelvis. That
    // substitution hands the leg drives a standing error to work against, where a ghost placed at
    // the body's actual pose would give them zero error and no reason to produce torque.
    //
    // Horizontally they sit off the body by the capture point's error from the middle of the planted
    // feet, times hipsOverStance. The planted legs then push the body back over its base of support,
    // which is the ankle/hip balance strategy done through the legs: internal torques, bounded by what
    // the feet can push against, so a big enough shove still needs a step. At 0 nothing holds the body
    // over its feet, and it balances like a pencil on its end.
    //
    // The capture point, not the hips: it carries the body's velocity, which damps the push. Pulling
    // the hips themselves over the feet was a spring with no damper: the body settled for a second,
    // then swayed through the stance and fell.
    //
    // It's the middle of the planted feet, never one foot while both are down: over a single foot,
    // each leg would demand the body be over its own and the two would fight. The swinging leg must
    // not feel this offset at all, or its foot lands off by the same amount, so FootPlacement adds
    // GhostHipOffset back onto the swing target.
    void FixedUpdate()
    {
        Vector3 hip = HipCentre();
        Vector3 error = (balanceController.StanceCentre() - balanceSensor.CapturePoint).Flat();
        Vector3 over = hip + error * hipsOverStance;

        ghostHips.SetPositionAndRotation(
            new Vector3(over.x, SupportPlaneY() + balanceController.StandingHipHeight, over.z),
            PelvisYaw());

        HipSag = Mathf.Max(0f, ghostHips.position.y - hip.y);
        GhostHipOffset = new Vector3(over.x - hip.x, 0f, over.z - hip.z);
    }

    // Horizontal position comes from the body, never from a foot. Place it over a foot and each
    // leg demands the body be over its own, the two fight, and leaning becomes impossible.
    Vector3 HipCentre() =>
        (AnchorOf(thighL, pelvis.transform) + AnchorOf(thighR, pelvis.transform)) * 0.5f;

    // The lower sole while both feet are down. Read off the ankle anchors rather than the foot
    // transforms, because the foot box's centre is not the ankle.
    float SupportPlaneY() =>
        Mathf.Min(AnchorOf(footL, shinL).y, AnchorOf(footR, shinR).y) - footGroundOffset;

    // Yaw only — a pelvis pitched forward mid-stumble should not tilt the whole solve. Taken
    // from the Rigidbody and flattened onto XZ rather than from eulerAngles.y, which degenerates
    // in exactly the near-vertical poses this has to survive. When forward itself goes vertical
    // the fallback gives a yaw ~90° off the true one: a discontinuity, but never a NaN.
    Quaternion PelvisYaw()
    {
        Vector3 forward = (pelvis.rotation * Vector3.forward).Flat();
        if (forward.sqrMagnitude < 1e-6f)
            forward = (pelvis.rotation * Vector3.up).Flat();

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    // A joint sits on the child body and its connectedAnchor is in the PARENT's local space,
    // so read each anchor off the parent. TransformPoint applies scale, which these scaled
    // primitives need — without it the thigh comes out as 2.0 instead of 0.42.
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

    // Silent on a missing joint — JointsPresent does the loud reporting once at Awake, so a
    // half-wired rig can't spam the console from OnDrawGizmos on every editor repaint.
    static Vector3 AnchorOf(Transform body, Transform parent)
    {
        ConfigurableJoint joint = body.GetComponent<ConfigurableJoint>();
        return joint == null ? parent.position : parent.TransformPoint(joint.connectedAnchor);
    }

    // The ghost must start indistinguishable from the physical rig: with both at identity, a
    // ghost bone's world rotation maps 1:1 onto the body's desired world rotation and there is
    // no offset table to carry. A ghost silently a few degrees off reads as a permanent lean
    // and gets blamed on drive gains for a day before anyone looks here.
    //
    // Awake only. Once the rig solves (Step 2) the ghost is SUPPOSED to diverge — that
    // divergence is the tracking error the whole system runs on. This is a bind pose check,
    // and it belongs where it can only ever see the bind pose.
    void AssertBindPose(LegMeasurement left, LegMeasurement right)
    {
        // A ghost bone is named for the segment it starts, so it sits on the joint ABOVE that
        // segment: GhostThigh on the hip, GhostShin on the knee, GhostFoot on the ankle.
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

    // Runs in edit mode too: connectedAnchor and the transforms are serialized data, and
    // checking alignment before pressing play is most of the point. So re-measure here rather
    // than use the Awake-cached values, which don't exist outside play mode.
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

    // The invariant this component exists to hold: in play mode the yellow line IS
    // standingHipHeight, and it should stay that length while the body sags away from it. Out of
    // play mode nothing has placed the hips yet, so it just measures the authored pose.
    void DrawSupportPlane()
    {
        Vector3 hip = HipCentre();
        Vector3 support = new(hip.x, SupportPlaneY(), hip.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(support - Vector3.right * 0.1f, support + Vector3.right * 0.1f);
        Gizmos.DrawLine(support - Vector3.forward * 0.1f, support + Vector3.forward * 0.1f);
        Gizmos.DrawLine(support, ghostHips.position);
    }

    void DrawLeg(LegMeasurement m, Transform gThigh, Transform gShin, Transform gFoot,
                 Transform thigh, Transform shin, Transform foot)
    {
        // Where the leg actually hinges.
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(m.Hip, 0.02f);
        Gizmos.DrawSphere(m.Knee, 0.02f);
        Gizmos.DrawSphere(m.Ankle, 0.02f);

        // The ghost chain, tinted by how far each bone has diverged from its body. At the bind
        // pose that is zero and this is an alignment check; from Step 5 on it is the tracking
        // error, and this gizmo is the instrument for reading it.
        DrawBone(gThigh, gShin, thigh);
        DrawBone(gShin, gFoot, shin);

        Gizmos.color = TrackingColor(gFoot, foot);
        Gizmos.DrawWireSphere(gFoot.position, 0.025f);
        Gizmos.DrawLine(gFoot.position, gFoot.position + gFoot.forward * 0.12f);
    }

    void DrawBone(Transform from, Transform to, Transform body)
    {
        Gizmos.color = TrackingColor(from, body);
        Gizmos.DrawLine(from.position, to.position);
        Gizmos.DrawWireSphere(from.position, 0.025f);
    }

    // Green while the body is where the ghost asked, red once it is 20° or more behind.
    static Color TrackingColor(Transform ghost, Transform body) =>
        Color.Lerp(Color.green, Color.red,
                   Mathf.InverseLerp(5f, 20f, Quaternion.Angle(ghost.rotation, body.rotation)));
}
