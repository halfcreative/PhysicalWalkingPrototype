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
    // Peak toe-up of a swinging foot, in degrees; see the ankle write in FixedUpdate.
    const float SwingToeLift = 15f;
    // Cap on a feed-forward velocity target, rad/s. A fast swing turns a knee ~90° in a third of a second,
    // about 5 rad/s; anything far past that is a glitch, not a motion.
    const float MaxFeedForward = 12f;

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
        // JointTargetExtensions for why they are passed anyway. Measured against the joint's connected
        // body, not the transform parent: the bodies sit flat under Player, not nested in each other.
        thighStart = StartRotation(hip);
        shinStart = StartRotation(knee);
        footStart = StartRotation(ankle);

        hipDrive = hip.slerpDrive;
        kneeDrive = knee.slerpDrive;
        ankleDrive = ankle.slerpDrive;

        prevGhostThigh = ghostThigh.rotation;
        prevGhostShin = ghostShin.rotation;
        prevGhostFoot = ghostFoot.rotation;
    }

    static Quaternion StartRotation(ConfigurableJoint joint) =>
        Quaternion.Inverse(joint.connectedBody.transform.rotation) * joint.transform.rotation;

    // The ghost bone's angular velocity relative to the joint's connected body, in that body's frame, as
    // the joint's velocity target. Relative, because the drive acts between the two bodies; in the
    // connected body's frame, like targetRotation. On this rig the legs hinge almost purely about X, which
    // both bodies share, so the choice of frame barely matters.
    //
    // NEGATED, like targetRotation's inverse. Measured, not assumed: the un-negated velocity made swing
    // tracking worse (4.2° → 6.9°) and the negated one better (→ 1.7°).
    //
    // A hinge (knee, ankle) keeps only its bend axis, and every result is capped at MaxFeedForward. Taken
    // whole, the velocity asked the knee to twist and roll whenever the ghost or the thigh turned sharply
    // off-axis (an impact, a near-straight IK flip). The drive fought the knee's ±5° side limits with it
    // and pulled the joint apart: measured 108° of twist and a 5 cm gap on a 2 m/s catch, gone with the
    // feed-forward off.
    static Vector3 TargetVelocity(ConfigurableJoint joint, Quaternion ghost, ref Quaternion prevGhost, float scale,
                                  bool hinge)
    {
        (ghost * Quaternion.Inverse(prevGhost)).ToAngleAxis(out float degrees, out Vector3 axis);
        prevGhost = ghost;

        if (scale == 0f || !float.IsFinite(axis.x)) return Vector3.zero;
        if (degrees > 180f) degrees -= 360f;

        Vector3 ghostVelocity = axis * (degrees * Mathf.Deg2Rad / Time.fixedDeltaTime);
        Rigidbody parent = joint.connectedBody;
        Vector3 velocity = -scale * (Quaternion.Inverse(parent.rotation) * (ghostVelocity - parent.angularVelocity));
        if (hinge) velocity = new Vector3(velocity.x, 0f, 0f);
        return Vector3.ClampMagnitude(velocity, MaxFeedForward);
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

        // The ghost thigh without its twist about its own axis; see the hip write below. Taken against the
        // ghost hips so it stays continuous when the leg goes from planted to swinging.
        Transform ghostHips = ghostThigh.parent;
        Quaternion ghostThighNoTwist =
            ghostHips.rotation * WithoutTwist(Quaternion.Inverse(ghostHips.rotation) * ghostThigh.rotation);

        // A planted leg gets no feed-forward: its damper resisting motion is what steadies the stance.
        float feedForward = swinging ? swingFeedForward : 0f;
        hip.targetAngularVelocity = TargetVelocity(hip, ghostThighNoTwist, ref prevGhostThigh, feedForward, false);
        knee.targetAngularVelocity = TargetVelocity(knee, ghostShin.rotation, ref prevGhostShin, feedForward, true);
        ankle.targetAngularVelocity = TargetVelocity(ankle, ghostFoot.rotation, ref prevGhostFoot, feedForward, true);

        // A planted leg resolves its hip and ankle targets against the GHOST's hips and shin, which makes
        // them joint angles to hold rather than world rotations to reach. That is what lets a planted leg
        // push back: the foot can't turn, so the drive turns the body instead.
        //   Hip: the ghost hips are always upright, so the drive rights the pelvis over the leg.
        //   Ankle: the ghost shin stands where PlayerRig wants the body, so the drive rolls the real
        //   shin (and the body on it) toward there. Against the real shin, a flat foot on flat ground
        //   is already on target, and nothing moves the body back over its feet.
        // Measured: resolved against the real bodies, the character can't stand without stepping.
        //
        // A swinging leg does the same at the knee: it copies the ghost's knee ANGLE (against the ghost
        // thigh). Against the real thigh, a thigh still stretched back behind the ghost's made the ghost
        // shin read as barely bent, so the knee stayed straight for the first 0.15 s of the swing and
        // the foot scraped. The swing's clearance comes from the knee bending on time.
        Transform hipFrame = swinging ? hip.connectedBody.transform : ghostHips;
        Transform kneeFrame = swinging ? ghostThigh : knee.connectedBody.transform;
        Transform ankleFrame = swinging ? ankle.connectedBody.transform : ghostShin;

        // The hip takes the thigh's direction from the ghost but no twist about the thigh's own axis: the
        // thigh is held at neutral twist against the pelvis. The ghost's thigh twist isn't designed by
        // anything. TwoBoneIK solves each tick from last tick's pose, so it's whatever the swing path and
        // the hint alignment left there: measured 0 → 35° on a stance leg through single support, and 72°
        // at a liftoff, against a ±30° hip. The drive shoved the thigh into its limit and the reaction spun
        // the pelvis (−183 °/s on a 1 m/s sideways catch). With the planted ankle also twist-free, the leg
        // is a plain twist spring between pelvis and foot.
        Quaternion hipLocal = WithoutTwist(Quaternion.Inverse(hipFrame.rotation) * ghostThigh.rotation);
        hip.SetTargetWorldRotation(hipFrame.rotation * hipLocal, thighStart, hipFrame);

        knee.SetTargetWorldRotation(KneeTarget(kneeFrame.rotation), shinStart, kneeFrame);

        // The ankle trim composes HERE, on the right of the ghost rotation so it applies about the
        // foot's own axis. Composing it into the one write is what keeps this the sole writer of
        // the ankle — BalanceController decides the trim, it does not apply it.
        //
        // Only on a planted foot: the trim balances the body through the ground. On a swinging foot it
        // just pointed the toes down (its +12° max, during a stumble), and the toe scuffed. A swinging
        // foot instead pulls its toes up, most at mid-swing and back to flat for touchdown, the way a
        // real swing clears the ground. That also covers the ~10° the foot sags toe-down behind its
        // target in a fast swing.
        Quaternion footPitch = swinging
            ? Quaternion.Euler(-SwingToeLift * Mathf.Sin(Mathf.PI * footPlacement.SwingProgress), 0f, 0f)
            : balanceController.AnkleTrim;
        Quaternion footTarget = ghostFoot.rotation * footPitch;

        // A planted ankle takes pitch and roll from the ghost shin, but no twist about the shin's axis.
        // The ghost shin's yaw is the pelvis's (TwoBoneIK never twists the thigh), and the planted foot
        // keeps its own facing, so a body turned over its feet put all the turn into the ghost's ankle
        // twist. Asked of the real ankle, with its foot held by the ground, that twisted the leg after
        // the pelvis and pushed the whole body further the way it was turning: a yaw drift that grew
        // ~2 °/s → 10 °/s in 2.6 s, until the ±15° twist limit stopped it.
        if (!swinging)
        {
            Quaternion local = WithoutTwist(Quaternion.Inverse(ghostShin.rotation) * footTarget);
            footTarget = ghostShin.rotation * local;
        }
        ankle.SetTargetWorldRotation(footTarget, footStart, ankleFrame);
    }

    // q with its twist about local Y (a leg bone's long axis) taken out: the swing that's left.
    static Quaternion WithoutTwist(Quaternion q)
    {
        Quaternion twist = new Quaternion(0f, q.y, 0f, q.w);
        float length = Mathf.Sqrt(twist.y * twist.y + twist.w * twist.w);
        if (length < 1e-6f) return q;
        twist = new Quaternion(0f, twist.y / length, 0f, twist.w / length);
        return q * Quaternion.Inverse(twist);
    }

    // The ghost shin's world rotation, unless that would bend the knee less than MinKneeFlexion against
    // `frame`, the rotation the target is resolved in.
    //
    // Backward: resolved against the REAL thigh (a planted leg), a thigh lagging the ghost's left the
    // ghost shin's world rotation past straight relative to it, and the drive shoved the knee into its
    // hyperextension limit.
    //
    // Straight: the ghost locks its knee whenever a target is out of reach, which in a stumble is often.
    // A locked knee has no give, so a scuff or a hard landing drove it straight through the 5° limit
    // (measured −16.8° on a mid-swing scuff). With a little bend held, an impact folds it forward.
    //
    // Flexion is +X about the knee axis, from a straight (identity) start pose.
    //
    // And only ever a bend: the knee is a hinge, so the target keeps the bend about X and drops the rest.
    // Against the real thigh of a planted leg leaning sideways, the ghost shin's world rotation also
    // carried twist and sideways fold, and the drive pressed the knee's ±5° side limits with them
    // (36 ticks past 10° on a 1 m/s sideways push).
    Quaternion KneeTarget(Quaternion frame)
    {
        Quaternion local = Quaternion.Inverse(frame) * ghostShin.rotation;
        if (local.w < 0f) local = new Quaternion(-local.x, -local.y, -local.z, -local.w);

        float flexion = 2f * Mathf.Atan2(local.x, local.w) * Mathf.Rad2Deg;
        return frame * Quaternion.AngleAxis(Mathf.Max(flexion, MinKneeFlexion), Vector3.right);
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
