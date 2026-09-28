using UnityEngine;

// Measures the leg chain from the joint anchors — which are the truth, not the transform
// positions — and checks that the ghost skeleton starts indistinguishable from the physical
// one. Writes nothing: placing the ghost hips is Step 3's job, and keeping this component
// read-only for now means a failure here can only be a measurement or an alignment problem.
[DefaultExecutionOrder(-40)]
public class PlayerRig : MonoBehaviour
{
    [Header("Physical")]
    [SerializeField] Transform pelvis;
    [SerializeField] Rigidbody pelvisBody; //Can this be combined wit the pelvis variable
    [SerializeField] Transform thighL, shinL, footL;
    [SerializeField] Transform thighR, shinR, footR;
    [SerializeField] BalanceController balanceController;

    [Header("Ghost")]
    [SerializeField] Transform ghostHips;
    [SerializeField] Transform ghostThighL, ghostShinL, ghostFootL;
    [SerializeField] Transform ghostThighR, ghostShinR, ghostFootR;

    [Header("Bind pose tolerances")]
    [SerializeField] float maxBindAngle = 0.5f;     // degrees
    [SerializeField] float maxBindOffset = 0.005f;  // metres

    [SerializeField] float footGroundOffset = 0.10f;

    // Ankle to sole. The IK targets the ankle, so FootPlacement lifts every target by this much.
    public float FootGroundOffset => footGroundOffset;

    // Leg geometry, measured at Awake. Step 8's reach clamp reads these.
    public float L1 { get; private set; }
    public float L2 { get; private set; }
    public float Chain { get; private set; }

    // Hip, knee and ankle in world space, read off the joint anchors.
    public struct LegMeasurement
    {
        public Vector3 Hip, Knee, Ankle;
        public float L1, L2;
        public float Chain => L1 + L2;
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

        L1 = (left.L1 + right.L1) * 0.5f;
        L2 = (left.L2 + right.L2) * 0.5f;
        Chain = L1 + L2;

        Debug.Log($"PlayerRig L  hip {left.Hip:F3} knee {left.Knee:F3} ankle {left.Ankle:F3}  " +
                  $"L1 {left.L1:F3}  L2 {left.L2:F3}  chain {left.Chain:F3}", this);
        Debug.Log($"PlayerRig R  hip {right.Hip:F3} knee {right.Knee:F3} ankle {right.Ankle:F3}  " +
                  $"L1 {right.L1:F3}  L2 {right.L2:F3}  chain {right.Chain:F3}", this);

        if (Mathf.Abs(left.L1 - right.L1) > maxBindOffset || Mathf.Abs(left.L2 - right.L2) > maxBindOffset)
            Debug.LogWarning($"PlayerRig: legs are asymmetric — " +
                             $"L1 {left.L1:F4}/{right.L1:F4}, L2 {left.L2:F4}/{right.L2:F4}", this);

        AssertBindPose(left, right);
    }

    // The ghost hips take the body's horizontal position and yaw, but their height comes from
    // the support plane rather than the pelvis. That one substitution is the mechanism: it
    // hands the leg drives a standing error to work against, where a ghost placed at the body's
    // actual pose would give them zero error and no reason to produce torque.
    void FixedUpdate()
    {
        Vector3 hip = HipCentre();

        ghostHips.SetPositionAndRotation(
            new Vector3(hip.x, SupportPlaneY() + balanceController.StandingHipHeight, hip.z),
            PelvisYaw());
    }

    // Horizontal position comes from the body, never from a foot. Place it over a foot and each
    // leg demands the body be over its own, the two fight, and leaning becomes impossible.
    Vector3 HipCentre() =>
        (AnchorOf(thighL, pelvis) + AnchorOf(thighR, pelvis)) * 0.5f;

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
        Vector3 forward = Vector3.ProjectOnPlane(pelvisBody.rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.ProjectOnPlane(pelvisBody.rotation * Vector3.up, Vector3.up);

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    // A joint sits on the child body and its connectedAnchor is in the PARENT's local space,
    // so read each anchor off the parent. TransformPoint applies scale, which these scaled
    // primitives need — without it L1 comes out as 2.0 instead of 0.42.
    LegMeasurement MeasureLeg(Transform thigh, Transform shin, Transform foot)
    {
        LegMeasurement m = default;
        m.Hip = AnchorOf(thigh, pelvis);
        m.Knee = AnchorOf(shin, thigh);
        m.Ankle = AnchorOf(foot, shin);
        m.L1 = Vector3.Distance(m.Hip, m.Knee);
        m.L2 = Vector3.Distance(m.Knee, m.Ankle);
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
        pelvis && pelvisBody && balanceController && thighL && shinL && footL && thighR && shinR && footR &&
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
