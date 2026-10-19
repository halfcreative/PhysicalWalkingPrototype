using UnityEngine;

// One per leg, and the only writer of its hip, knee and ankle targets: the physical leg follows the ghost.
//
// The ghost's joint angles are a tick old (the Animator solves after this runs), but PlayerRig places the
// ghost hips first (−40 vs 10), so the bones' world rotations are current with the body.
[DefaultExecutionOrder(10)]
public class LegDrive : MonoBehaviour
{
    // Least knee bend a target may ask for, degrees; see KneeTarget.
    const float MinKneeFlexion = 5f;
    // Peak toe-up of a swinging foot, degrees.
    const float SwingToeLift = 15f;
    // Cap on a feed-forward velocity target, rad/s. Real swings peak around 5.
    const float MaxFeedForward = 12f;

    [Header("References")]
    [SerializeField] Transform thigh, shin, foot;
    [SerializeField] Transform ghostThigh, ghostShin, ghostFoot;
    [SerializeField] BalanceController balanceController;
    [SerializeField] FootPlacement footPlacement;
    [SerializeField] PlayerController playerController;

    [Header("Stance Ankle")]
    // Planted ankle stiffness, as a fraction of authored: soft enough to settle the foot, stiff enough to
    // hold the body over it.
    [SerializeField, Range(0.05f, 1f)] float stanceAnkleStiffness = 0.7f;

    [Header("Swing Feed-Forward")]
    // Feeds the ghost's angular velocity to the drives so their dampers don't drag the swing. 0 is off.
    [SerializeField, Range(0f, 1.5f)] float swingFeedForward = 1f;

    [Header("Soft Landing")]
    // Hip and knee stiffness at touchdown, ramping to full over landingRampTime, so a catch doesn't rebound.
    [SerializeField, Range(0.1f, 1f)] float landingStiffness = 0.3f;
    [SerializeField] float landingRampTime = 0.15f;
    // Swing progress at which softening starts; the real foot touches down a little early.
    [SerializeField, Range(0.5f, 1f)] float softenFromSwing = 0.8f;

    ConfigurableJoint hip, knee, ankle;
    Quaternion thighStart, shinStart, footStart;

    // Drives as authored, and the stiffness scale currently applied to each.
    JointDrive hipDrive, kneeDrive, ankleDrive;
    float hipStiffness = 1f, kneeStiffness = 1f, ankleStiffness = 1f;

    // Last tick's ghost rotations, for feed-forward velocity.
    Quaternion previousGhostThigh, previousGhostShin, previousGhostFoot;

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

        thighStart = hip.LocalRotation();
        shinStart = knee.LocalRotation();
        footStart = ankle.LocalRotation();

        hipDrive = hip.slerpDrive;
        kneeDrive = knee.slerpDrive;
        ankleDrive = ankle.slerpDrive;

        previousGhostThigh = ghostThigh.rotation;
        previousGhostShin = ghostShin.rotation;
        previousGhostFoot = ghostFoot.rotation;
    }

    void FixedUpdate()
    {
        if (playerController.IsFallen)
        {
            float limp = playerController.FallenStiffness;
            SetStiffness(hip, hipDrive, ref hipStiffness, limp);
            SetStiffness(knee, kneeDrive, ref kneeStiffness, limp);
            SetStiffness(ankle, ankleDrive, ref ankleStiffness, limp);
            hip.targetAngularVelocity = knee.targetAngularVelocity = ankle.targetAngularVelocity = Vector3.zero;
            return;
        }

        bool swinging = footPlacement.IsStepping;

        // Late swing eases down to landingStiffness; after touchdown it ramps back to full.
        float landingScale = swinging
            ? Mathf.Lerp(1f, landingStiffness, Mathf.InverseLerp(softenFromSwing, 1f, footPlacement.SwingProgress))
            : Mathf.Lerp(landingStiffness, 1f, Mathf.Clamp01(footPlacement.TimeSinceLanding / landingRampTime));
        SetStiffness(hip, hipDrive, ref hipStiffness, landingScale);
        SetStiffness(knee, kneeDrive, ref kneeStiffness, landingScale);
        SetStiffness(ankle, ankleDrive, ref ankleStiffness, swinging ? 1f : stanceAnkleStiffness);

        // Taken against the ghost hips so it stays continuous between planted and swinging.
        Transform ghostHips = ghostThigh.parent;
        Quaternion ghostThighNoTwist =
            ghostHips.rotation * (Quaternion.Inverse(ghostHips.rotation) * ghostThigh.rotation).WithoutTwistY();

        // A planted leg gets no feed-forward: its dampers steady the stance.
        float feedForward = swinging ? swingFeedForward : 0f;
        hip.targetAngularVelocity = TargetVelocity(hip, ghostThighNoTwist, ref previousGhostThigh, feedForward, false);
        knee.targetAngularVelocity = TargetVelocity(knee, ghostShin.rotation, ref previousGhostShin, feedForward, true);
        ankle.targetAngularVelocity = TargetVelocity(ankle, ghostFoot.rotation, ref previousGhostFoot, feedForward, true);

        // The frame each target is resolved in. A planted leg resolves hip and ankle against the GHOST's
        // hips and shin, making them joint angles to hold: the foot can't move, so the drive moves the body
        // back over it. A swinging leg resolves the knee against the ghost thigh, so it bends on time even
        // while the real thigh lags.
        Transform hipFrame = swinging ? hip.connectedBody.transform : ghostHips;
        Transform kneeFrame = swinging ? ghostThigh : knee.connectedBody.transform;
        Transform ankleFrame = swinging ? ankle.connectedBody.transform : ghostShin;

        // No thigh twist: the IK's twist is arbitrary, and driving it spins the pelvis.
        Quaternion hipLocal = (Quaternion.Inverse(hipFrame.rotation) * ghostThigh.rotation).WithoutTwistY();
        hip.SetTargetWorldRotation(hipFrame.rotation * hipLocal, thighStart, hipFrame);

        knee.SetTargetWorldRotation(KneeTarget(kneeFrame.rotation), shinStart, kneeFrame);

        // Planted: the balance trim, about the foot's own axes. Swinging: toes up, peaking mid-swing, so
        // the toe clears the ground.
        Quaternion footAdjustment = swinging
            ? Quaternion.Euler(-SwingToeLift * Mathf.Sin(Mathf.PI * footPlacement.SwingProgress), 0f, 0f)
            : balanceController.AnkleTrim;
        Quaternion footTarget = ghostFoot.rotation * footAdjustment;

        // A planted ankle takes no twist: the foot keeps its facing while the body turns over it, and
        // driving that twist pushes the body further round.
        if (!swinging)
            footTarget = ghostShin.rotation * (Quaternion.Inverse(ghostShin.rotation) * footTarget).WithoutTwistY();
        ankle.SetTargetWorldRotation(footTarget, footStart, ankleFrame);
    }

    // The ghost bone's angular velocity relative to the connected body, as the joint's velocity target.
    // Negated like targetRotation. A hinge keeps only its bend axis, or the drive fights the side limits.
    static Vector3 TargetVelocity(ConfigurableJoint joint, Quaternion ghost, ref Quaternion previousGhost,
                                  float scale, bool hinge)
    {
        (ghost * Quaternion.Inverse(previousGhost)).ToAngleAxis(out float degrees, out Vector3 axis);
        previousGhost = ghost;

        if (scale == 0f || !float.IsFinite(axis.x)) return Vector3.zero;
        if (degrees > 180f) degrees -= 360f;

        Vector3 ghostVelocity = axis * (degrees * Mathf.Deg2Rad / Time.fixedDeltaTime);
        Rigidbody parent = joint.connectedBody;
        Vector3 velocity = -scale * (Quaternion.Inverse(parent.rotation) * (ghostVelocity - parent.angularVelocity));
        if (hinge) velocity = new Vector3(velocity.x, 0f, 0f);
        return Vector3.ClampMagnitude(velocity, MaxFeedForward);
    }

    // Writes only when the scale changes: assigning slerpDrive wakes the body.
    static void SetStiffness(ConfigurableJoint joint, JointDrive authored, ref float current, float scale)
    {
        if (Mathf.Approximately(scale, current)) return;
        current = scale;
        joint.slerpDrive = authored.Scaled(scale);
    }

    // The ghost shin's rotation in `frame`, reduced to a pure bend about X of at least MinKneeFlexion. Never
    // straight or hyperextended, so an impact folds the knee forward instead of through its limit.
    Quaternion KneeTarget(Quaternion frame)
    {
        float flexion = (Quaternion.Inverse(frame) * ghostShin.rotation).AngleAboutX();
        return frame * Quaternion.AngleAxis(Mathf.Max(flexion, MinKneeFlexion), Vector3.right);
    }

    bool ReferencesAssigned() =>
        thigh && shin && foot && ghostThigh && ghostShin && ghostFoot && balanceController && footPlacement &&
        playerController;

    bool JointsUsable()
    {
        (ConfigurableJoint joint, Transform body)[] chain = { (hip, thigh), (knee, shin), (ankle, foot) };
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
                Debug.LogError($"LegDrive: {body.name}'s joint has no connected body.", body);
                ok = false;
            }
        }

        return ok;
    }
}
