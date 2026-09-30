using UnityEngine;

// One per leg, and the ONLY writer of that leg's three joint targets. It reads the pose the ghost
// was solved into and hands it to the drives — that handoff is the whole of "the physical leg
// follows the ghost".
//
// The ghost pose read here was solved during the PREVIOUS tick: the Animator evaluates after this
// component runs. That tick of latency is deliberate (Part 1) — the alternative is guessing at
// where Unity puts the animation pass relative to ours. The joint target is a setpoint, not a
// teleport, so a tick-old target on a foot moving at 0.3 m/s is about 6 mm of error.
//
// PlayerRig places the ghost hips before this runs (−40 vs 10), so the ghost bones' WORLD
// rotations are current with the body even though their joint angles are a tick old. The leg's
// orientation never lags the pelvis.
[DefaultExecutionOrder(10)]
public class LegDrive : MonoBehaviour
{
    // The least knee bend a target may ask for, in degrees; see KneeTarget.
    const float MinKneeFlexion = 5f;

    [Header("Physical")]
    [SerializeField] Transform thigh, shin, foot;

    [Header("Ghost")]
    [SerializeField] Transform ghostThigh, ghostShin, ghostFoot;

    [Header("Trim")]
    [SerializeField] BalanceController balanceController;

    [Header("Stance ankle")]
    // Whether this leg's foot is planted. A planted ankle runs a little soft so the foot settles onto
    // the ground instead of levering the body off it; a swinging one runs at full stiffness to hold its
    // pose.
    [SerializeField] FootPlacement footPlacement;
    // Ankle drive stiffness while planted, as a fraction of the authored spring. The damper scales by
    // its square root, which keeps the damping ratio. The planted ankle is what holds the body over its
    // feet, so not too soft: at 0.3 the body rested 7 cm ahead of the soles, a hair off a step.
    [SerializeField, Range(0.05f, 1f)] float stanceAnkleStiffness = 0.7f;

    [Header("Swing feed-forward")]
    // With a target angular velocity of zero, each drive's damper resists the very motion the ghost is
    // asking for, and the swing leg trails the ghost by ~0.15 s. Feeding the ghost's own angular velocity
    // forward stops the damper dragging: mean swing tracking error 4.2° → 1.7°. 0 turns it off.
    [SerializeField, Range(0f, 1.5f)] float swingFeedForward = 1f;

    [Header("Soft landing")]
    // A foot landing ahead of the body puts the leg at an angle, and a leg driven hard to straighten along
    // that angle shoves the body backward: the catch rebounds. So the hip and knee land soft, at this
    // fraction of their authored stiffness, and ramp to full over landingRampTime, the way a knee gives on
    // impact.
    [SerializeField, Range(0.1f, 1f)] float landingStiffness = 0.3f;
    [SerializeField] float landingRampTime = 0.15f;
    // The physical foot touches down a few ticks before the swing's logical end, so the leg starts
    // softening from this point in the swing, not at the landing. Measured: at full stiffness that last
    // stretch of swing was where most of the rebound happened.
    [SerializeField, Range(0.5f, 1f)] float softenFromSwing = 0.8f;

    ConfigurableJoint hip, knee, ankle;
    Quaternion thighStart, shinStart, footStart;

    // Drives as authored, and the stiffness scale currently applied to each.
    JointDrive hipDrive, kneeDrive, ankleDrive;
    float hipStiffness = 1f, kneeStiffness = 1f, ankleStiffness = 1f;

    bool wasSwinging;
    float landTime = float.NegativeInfinity;

    // Last tick's ghost rotations. The Animator solves the ghost once per physics step, so a tick is the
    // interval between target updates.
    Quaternion prevGhostThigh, prevGhostShin, prevGhostFoot;

    void Awake()
    {
        if (!ReferencesAssigned())
        {
            Debug.LogError("LegDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        hip = thigh.GetComponent<ConfigurableJoint>();
        knee = shin.GetComponent<ConfigurableJoint>();
        ankle = foot.GetComponent<ConfigurableJoint>();

        if (!JointsUsable())
        {
            enabled = false;
            return;
        }

        // Captured before physics has moved anything, which is what makes these the start pose the
        // drives measure against. Identity on this rig, so they currently cancel — see
        // JointTargetExtensions for why they are passed anyway.
        thighStart = thigh.localRotation;
        shinStart = shin.localRotation;
        footStart = foot.localRotation;

        hipDrive = hip.slerpDrive;
        kneeDrive = knee.slerpDrive;
        ankleDrive = ankle.slerpDrive;

        prevGhostThigh = ghostThigh.rotation;
        prevGhostShin = ghostShin.rotation;
        prevGhostFoot = ghostFoot.rotation;
    }

    // The ghost bone's angular velocity relative to the joint's connected body, in that body's frame, as
    // the joint's velocity target. Relative, because the drive acts between the two bodies; in the
    // connected body's frame, like targetRotation. On this rig the legs hinge almost purely about X, which
    // both bodies share, so the choice of frame barely matters.
    //
    // NEGATED, like targetRotation's inverse. Measured, not assumed: the un-negated velocity made swing
    // tracking worse (4.2° → 6.9°) and the negated one better (→ 1.7°).
    static Vector3 TargetVelocity(ConfigurableJoint joint, Quaternion ghost, ref Quaternion prevGhost, float scale)
    {
        (ghost * Quaternion.Inverse(prevGhost)).ToAngleAxis(out float degrees, out Vector3 axis);
        prevGhost = ghost;

        if (scale == 0f || !float.IsFinite(axis.x)) return Vector3.zero;
        if (degrees > 180f) degrees -= 360f;

        Vector3 ghostVelocity = axis * (degrees * Mathf.Deg2Rad / Time.fixedDeltaTime);
        Rigidbody parent = joint.connectedBody;
        return -scale * (Quaternion.Inverse(parent.rotation) * (ghostVelocity - parent.angularVelocity));
    }

    // Scales a joint's authored drive. The damper goes by the square root, which keeps the damping ratio.
    // Writes only when the scale changes: assigning slerpDrive wakes the body every time.
    static void SetStiffness(ConfigurableJoint joint, JointDrive authored, ref float current, float scale)
    {
        if (Mathf.Approximately(scale, current)) return;
        current = scale;

        authored.positionSpring *= scale;
        authored.positionDamper *= Mathf.Sqrt(scale);
        joint.slerpDrive = authored;
    }

    // The entire component. Each joint gets the ghost bone's world rotation, resolved against the
    // connected body's CURRENT pose rather than a cached one, so a lagging thigh doesn't push the
    // shin's target out of place as well.
    void FixedUpdate()
    {
        bool swinging = footPlacement.IsStepping;
        if (wasSwinging && !swinging) landTime = Time.time;
        wasSwinging = swinging;

        // Late swing eases down to landingStiffness; after touchdown it ramps back up to full.
        float landing = swinging
            ? Mathf.Lerp(1f, landingStiffness, Mathf.InverseLerp(softenFromSwing, 1f, footPlacement.SwingProgress))
            : Mathf.Lerp(landingStiffness, 1f, Mathf.Clamp01((Time.time - landTime) / landingRampTime));
        SetStiffness(hip, hipDrive, ref hipStiffness, landing);
        SetStiffness(knee, kneeDrive, ref kneeStiffness, landing);
        SetStiffness(ankle, ankleDrive, ref ankleStiffness, swinging ? 1f : stanceAnkleStiffness);

        // A planted leg gets no feed-forward: its damper resisting motion is what steadies the stance.
        float feedForward = swinging ? swingFeedForward : 0f;
        hip.targetAngularVelocity = TargetVelocity(hip, ghostThigh.rotation, ref prevGhostThigh, feedForward);
        knee.targetAngularVelocity = TargetVelocity(knee, ghostShin.rotation, ref prevGhostShin, feedForward);
        ankle.targetAngularVelocity = TargetVelocity(ankle, ghostFoot.rotation, ref prevGhostFoot, feedForward);

        // A planted leg resolves its hip and ankle targets against the GHOST's hips and shin, which makes
        // them joint angles to hold rather than world rotations to reach. That is what lets a planted leg
        // push back: the foot can't turn, so the drive turns the body instead.
        //   Hip: the ghost hips are always upright, so the drive rights the pelvis over the leg.
        //   Ankle: the ghost shin stands where PlayerRig wants the body, so the drive rolls the real
        //   shin (and the body on it) toward there. Against the real shin, a flat foot on flat ground
        //   is already on target, and nothing moves the body back over its feet.
        // Measured: resolved against the real bodies, the character can't stand without stepping.
        Transform hipFrame = swinging ? hip.connectedBody.transform : ghostThigh.parent;
        Transform ankleFrame = swinging ? ankle.connectedBody.transform : ghostShin;

        hip.SetTargetWorldRotation(ghostThigh.rotation, thighStart, hipFrame);

        knee.SetTargetWorldRotation(KneeTarget(), shinStart, knee.connectedBody.transform);

        // The ankle trim composes HERE, on the right of the ghost rotation so it applies about the
        // foot's own axis. Composing it into the one write is what keeps this the sole writer of
        // the ankle — BalanceController decides the trim, it does not apply it.
        ankle.SetTargetWorldRotation(ghostFoot.rotation * balanceController.AnkleTrim, footStart, ankleFrame);
    }

    // The ghost shin's world rotation, unless that would bend the knee less than MinKneeFlexion.
    //
    // Backward: the target is resolved against the REAL thigh, so when the thigh lags the ghost's (a leg
    // reaching forward to catch, say), the ghost shin's world rotation sits past straight relative to
    // it, and the drive shoved the knee into its hyperextension limit.
    //
    // Straight: the ghost locks its knee whenever a target is out of reach, which in a stumble is often.
    // A locked knee has no give, so a scuff or a hard landing drove it straight through the 5° limit
    // (measured −16.8° on a mid-swing scuff). With a little bend held, an impact folds it forward.
    //
    // Flexion is +X about the knee axis, from a straight (identity) start pose.
    Quaternion KneeTarget()
    {
        Quaternion thighNow = knee.connectedBody.rotation;
        Quaternion local = Quaternion.Inverse(thighNow) * ghostShin.rotation;
        if (local.w < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);

        float flexion = 2f * Mathf.Atan2(local.x, local.w) * Mathf.Rad2Deg;
        return flexion >= MinKneeFlexion
            ? ghostShin.rotation
            : thighNow * Quaternion.AngleAxis(MinKneeFlexion, Vector3.right);
    }

    bool ReferencesAssigned() =>
        thigh && shin && foot && ghostThigh && ghostShin && ghostFoot && balanceController && footPlacement;

    bool JointsUsable()
    {
        (ConfigurableJoint joint, Transform body)[] chain =
            { (hip, thigh), (knee, shin), (ankle, foot) };
        bool ok = true;

        foreach ((ConfigurableJoint joint, Transform body) in chain)
        {
            if (joint == null)
            {
                Debug.LogError($"LegDrive: {body.name} has no ConfigurableJoint.", body);
                ok = false;
            }
            else if (joint.connectedBody == null)
            {
                Debug.LogError($"LegDrive: {body.name}'s joint has no connected body — there is " +
                               "nothing to resolve its target against.", body);
                ok = false;
            }
        }

        return ok;
    }
}
