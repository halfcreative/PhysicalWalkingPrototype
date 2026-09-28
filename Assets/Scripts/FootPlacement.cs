using UnityEngine;

// Owns where one foot should be, in world space, and writes it to its own transform: the FootTarget
// the ghost leg's TwoBoneIK reads. Planted, the target is frozen in the world. Stepping, it travels
// from liftoff to landing along a sine arc.
//
// It also decides when to step (Step 8): if the capture point has escaped the stance, the trailing
// foot steps to it. Land on it and the body stops; land short of it (captureGain < 1) and the body
// keeps going, which is walking.
[DefaultExecutionOrder(0)]
public class FootPlacement : MonoBehaviour
{
    // Yaw source. PlayerRig (−40) sets it to the pelvis's yaw only, before this runs.
    [SerializeField] Transform ghostHips;
    // This leg's IK root. Reach is measured from here because it's the hip the solver actually uses.
    [SerializeField] Transform ghostThigh;
    [SerializeField] FootPlacement otherFoot;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] PlayerRig playerRig;

    [Header("Swing")]
    [SerializeField] float swingDuration = 0.30f;
    [SerializeField] float stepHeight = 0.10f;

    // The IK target is the ankle, not the sole, so the target sits this far above the ground point.
    [SerializeField] float footGroundOffset = 0.10f;
    // The foot box's centre sits this far ahead of the ankle. Balance is judged against the sole, not
    // the ankle, so this is where "the foot" is for the capture point and the ankle trim.
    [SerializeField] float soleCentreForward = 0.05f;

    [Header("Trigger")]
    [SerializeField] bool autoStep = true;
    [SerializeField] float stepMargin = 0.08f;
    [SerializeField] float minStepLength = 0.04f;
    [SerializeField] float captureGain = 1.0f;
    [SerializeField] float stanceHalfWidth = 0.11f;
    [SerializeField] float doubleSupportDwell = 0.12f;
    // Fraction of the swing during which the landing keeps tracking the capture point.
    [SerializeField, Range(0f, 1f)] float retargetUntil = 0.7f;
    // Landing may not bring the feet closer than this sideways, measured along the body's right.
    [SerializeField] float minStanceWidth = 0.14f;
    // No steps while the body drops onto its legs at spawn. Without it, the settling lean steps once.
    [SerializeField] float spawnSettleTime = 1.0f;
    // The plan's 0.9 leaves no reach at all: standing already uses 0.75 m of drop against a 0.82 m
    // chain. 0.98 allows ~0.29 m of horizontal reach from the hip. See resume-here §5.3.
    [SerializeField] float strideReachSafety = 0.98f;
    // Everything but the player's own layers, or the ground cast lands on the character itself.
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Debug")]
    [SerializeField] bool logSteps = false;

    // Ground points, not ankle points: footGroundOffset is added only when writing the transform.
    Vector3 currentPos;
    Vector3 targetPos;
    Vector3 liftoffPos;
    Vector3 currentNormal = Vector3.up;
    bool isStepping;
    bool retargeting;   // set for steps the trigger commits; StepTester's fixed steps don't chase
    float stepProgress;

    // −1 for the left leg, +1 for the right, read off the rig so it can't be set wrong.
    float side;

    public bool IsStepping => isStepping;
    public bool IsPlanted => !isStepping;
    public Vector3 FootPos => currentPos;
    public Vector3 SupportPoint => currentPos + Forward * soleCentreForward;
    public float LastLandTime { get; private set; }

    Vector3 Forward => ghostHips.forward;

    void Awake()
    {
        if (!ghostHips || !ghostThigh || !otherFoot || !balanceSensor || !playerRig)
        {
            Debug.LogError("FootPlacement: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        side = Mathf.Sign(Vector3.Dot(ghostThigh.position - ghostHips.position, ghostHips.right));

        // Start planted wherever the target was authored.
        currentPos = transform.position - Vector3.up * footGroundOffset;
        targetPos = liftoffPos = currentPos;
    }

    void FixedUpdate()
    {
        if (autoStep)
            DecideStep();

        if (isStepping)
            AdvanceSwing();

        transform.SetPositionAndRotation(
            currentPos + Vector3.up * footGroundOffset,
            Quaternion.FromToRotation(Vector3.up, currentNormal) * ghostHips.rotation);
    }

    void DecideStep()
    {
        // Gates: busy, one foot at a time (lifting both is a jump), and the double-support beat after
        // EITHER foot lands. Only waiting on the other foot lets a foot step again the tick it lands.
        if (Time.time < spawnSettleTime) return;
        if (isStepping || otherFoot.IsStepping) return;
        float lastLand = Mathf.Max(LastLandTime, otherFoot.LastLandTime);
        if (lastLand > 0f && Time.time - lastLand < doubleSupportDwell) return;

        Vector3 capture = Flat(balanceSensor.COMPrediction);
        Vector3 mine = Flat(SupportPoint);
        Vector3 theirs = Flat(otherFoot.SupportPoint);

        // Measured from the line between the two soles, not their midpoint: a body resting over one
        // foot of a wide stance is fine, and measuring from the midpoint made that foot march in place.
        Vector3 error = capture - ClosestOnSegment(capture, mine, theirs);
        if (error.magnitude < stepMargin) return;

        if (!IsMyStep(error, capture, mine, theirs)) return;

        Vector3 landing = Landing(capture, currentPos, out Vector3 normal);

        // A step that goes nowhere only unloads the leg. It can happen when the reach or width clamps
        // pin the landing to where the foot already is.
        if (Flat(landing - currentPos).magnitude < minStepLength) return;

        if (logSteps)
            Debug.Log($"[{Time.time:F2}] {name} steps  capture {capture:F3}  error {error:F3} " +
                      $"(|{error.magnitude:F3}|)  from {currentPos:F3} to {landing:F3}  " +
                      $"COM vel {balanceSensor.COMVelocity:F2}", this);

        BeginStep(landing, normal, true);
    }

    // Where a foot lifting off from `from` should land to catch the body. The capture point keeps
    // moving while the foot is in the air, so this is re-run during the swing (see AdvanceSwing).
    Vector3 Landing(Vector3 capture, Vector3 from, out Vector3 normal)
    {
        // Put the sole centre on the capture point, offset sideways so the feet don't cross.
        Vector3 right = ghostHips.right;
        Vector3 fromSole = Flat(from + Forward * soleCentreForward);
        Vector3 sole = fromSole + (capture - fromSole) * captureGain + right * (side * stanceHalfWidth);
        Vector3 landing = sole - Forward * soleCentreForward;
        landing.y = from.y;

        // Guard against crossing or a stance too narrow to stand on: keep this foot at least
        // minStanceWidth out on its own side of the other one.
        float width = side * Vector3.Dot(landing - otherFoot.FootPos, right);
        if (width < minStanceWidth)
            landing += right * (side * (minStanceWidth - width));

        landing = ClampToReach(landing);

        normal = Vector3.up;
        if (Physics.Raycast(landing + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f,
                            groundMask, QueryTriggerInteraction.Ignore))
        {
            landing = hit.point;
            normal = hit.normal;
        }

        return landing;
    }

    // Which foot steps is a decision, not a race: without one both feet chase the same threshold and it
    // shuffles. Falling mostly sideways, the foot on that side steps out, because the trailing foot
    // would have to cross over. Otherwise the trailing foot steps: the one further from the capture
    // point. On an exact tie the foot that runs first steps, and the other then sees it stepping.
    bool IsMyStep(Vector3 error, Vector3 capture, Vector3 mine, Vector3 theirs)
    {
        float lateral = Vector3.Dot(error, ghostHips.right);
        float forward = Vector3.Dot(error, Forward);

        if (Mathf.Abs(lateral) > Mathf.Abs(forward))
            return Mathf.Sign(lateral) == side;

        return (capture - mine).sqrMagnitude >= (capture - theirs).sqrMagnitude;
    }

    // The horizontal budget from the hip, given how far below it the ankle has to reach.
    Vector3 ClampToReach(Vector3 landing)
    {
        Vector3 hip = ghostThigh.position;
        float drop = hip.y - (landing.y + footGroundOffset);
        float reach = playerRig.Chain * strideReachSafety;
        float maxHorizontal = Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop));

        Vector3 offset = Vector3.ClampMagnitude(Flat(landing - hip), maxHorizontal);
        return new Vector3(hip.x + offset.x, landing.y, hip.z + offset.z);
    }

    // Smoothstep on the horizontal so the foot eases off and onto the ground, plus a sine hump in Y
    // that is zero at both ends. Progress is left unclamped in the sine: it is only past 1 on the
    // final tick, and that tick snaps to the target anyway.
    //
    // A step the trigger committed keeps chasing the capture point until retargetUntil of the swing,
    // then holds its landing so the foot doesn't skate in on touchdown. Without this the foot lands where
    // the body was heading at liftoff, and each short landing sets up the next, growing, step.
    void AdvanceSwing()
    {
        stepProgress += Time.fixedDeltaTime / swingDuration;

        if (retargeting && stepProgress < retargetUntil)
            targetPos = Landing(Flat(balanceSensor.COMPrediction), liftoffPos, out currentNormal);

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stepProgress));
        Vector3 xz = Vector3.Lerp(liftoffPos, targetPos, t);
        float y = Mathf.Lerp(liftoffPos.y, targetPos.y, t) + stepHeight * Mathf.Sin(stepProgress * Mathf.PI);
        currentPos = new Vector3(xz.x, y, xz.z);

        if (stepProgress >= 1f)
        {
            isStepping = false;
            currentPos = targetPos;
            LastLandTime = Time.time;
        }
    }

    // Commits a step from wherever the foot is now, to a fixed landing. The normal is taken at commit
    // rather than on landing, which only matters on uneven ground; the floor here is flat.
    public void BeginStep(Vector3 landing, Vector3 normal) => BeginStep(landing, normal, false);

    void BeginStep(Vector3 landing, Vector3 normal, bool retarget)
    {
        liftoffPos = currentPos;
        targetPos = landing;
        currentNormal = normal;
        stepProgress = 0f;
        isStepping = true;
        retargeting = retarget;
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 1e-8f) return a;
        return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = isStepping ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(currentPos, 0.03f);

        if (!isStepping) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(liftoffPos, targetPos);
        Gizmos.DrawWireSphere(targetPos, 0.04f);
    }
}
