using UnityEngine;

// Owns where one foot should be, in world space, and writes it to its own transform: the FootTarget
// the ghost leg's TwoBoneIK reads. Planted, the target is frozen in the world. Stepping, it travels
// from liftoff to landing along an arc.
//
// It also decides when to step: if the capture point has escaped the stance, a foot steps to it. Land
// on it and the body stops; land short of it (captureGain < 1) and the body keeps going, which is walking.
//
// The gait and cadence are ported from DrunkWalkHome's FootPlacement: the Froude stride ceiling, the
// duty-factor swing duration, the speed-faded double-support dwell, the older-plant tie-break, the
// early-peak lift curve scaled to the step's span, the faded mid-flight re-aim, the body-local stance
// clamp and the knee hint posing. What stays ours is the trigger: DrunkWalkHome steps when the hip has
// travelled a stride past the foot, because its body is kinematic; ours steps when the capture point
// leaves the stance, because ours can fall. See resume-here §5.5–5.6.
[DefaultExecutionOrder(0)]
public class FootPlacement : MonoBehaviour
{
    const float Gravity = 9.81f;

    // Gait (DrunkWalkHome). Step length = c · Fr^β · L / 2 with Fr = v² / (gL); c is fitted so a 0.9 m
    // leg reproduces the generic adult fit, so on our 0.82 m chain it is dynamic similarity, not a guess.
    const float FroudeCoefficient = 2.35f;
    const float FroudeExponent = 0.25f;
    // Stance share of the stride: > 0.65 at a slow walk, 0.52–0.55 at a fast one.
    const float DutyFactorSlow = 0.65f;
    const float DutyFactorFast = 0.54f;
    const float MinSwingDuration = 0.2f;
    // The double-support beat after a landing, fading to nothing by DwellFadeSpeed: slow walks have
    // one, fast gaits don't.
    const float DoubleSupportDwell = 0.12f;
    const float DwellFadeSpeed = 4f;
    // Below this speed there is no direction of travel, so no stride ceiling and no stride to measure.
    const float CrawlSpeed = 0.15f;

    // Swing arc (DrunkWalkHome). Lift varies a little per step, and may not exceed this fraction of how
    // far the step travels, so a short shuffle doesn't lift like a full stride and read as marching.
    const float StepHeightVariance = 0.03f;
    const float StepHeightPerSpan = 0.8f;
    // How hard an in-flight foot re-aims at the moving capture point, faded to zero by landing.
    const float RetargetStrength = 10f;
    const float KneeOffsetDistance = 0.3f;

    // The foot box's centre sits this far ahead of the ankle. Balance is judged against the sole, not
    // the ankle, so this is where "the foot" is for the capture point and the ankle trim.
    const float SoleCentreForward = 0.05f;
    // A step shorter than this only unloads the leg. It happens when the clamps pin the landing.
    const float MinStepLength = 0.04f;
    // Body-local half-width band a foot may plant in, from the hip centreline: the inner bound keeps the
    // legs from crossing, the outer one stops the stance splaying. DWH uses 0.12 inside, but that is
    // wider than our 0.11 hips.
    const float MinStanceHalfWidth = 0.08f;
    const float MaxStanceHalfWidth = 0.45f;
    // The plan's 0.9 leaves no reach at all: standing already uses 0.75 m of drop against a 0.82 m
    // chain. 0.98 allows ~0.29 m of horizontal reach from the hip. See resume-here §5.3.
    const float StrideReachSafety = 0.98f;
    // No steps while the body drops onto its legs at spawn. Without it, the settling lean steps once.
    const float SpawnSettleTime = 1.0f;

    [Header("Rig")]
    [SerializeField] PlayerRig playerRig;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] FootPlacement otherFoot;
    // Yaw source. PlayerRig (−40) sets it to the pelvis's yaw only, before this runs.
    [SerializeField] Transform ghostHips;
    // This leg's IK root. Reach is measured from here because it's the hip the solver actually uses.
    [SerializeField] Transform ghostThigh;
    // This leg's TwoBoneIK hint, re-aimed every tick; see PoseKneeHint.
    [SerializeField] Transform kneeHint;

    [Header("Tuning")]
    // 1 = land on the capture point and stop. Lower lands short so the body keeps going: walking.
    [SerializeField, Range(0.3f, 1f)] float captureGain = 1.0f;
    // How far the capture point may leave the stance before a foot steps.
    [SerializeField] float stepMargin = 0.08f;
    [SerializeField] float stepHeight = 0.10f;
    // The duty factor is a walking relation and asks for long swings at recovery speeds. Lower this
    // first if a catch step arrives too late.
    [SerializeField] float maxSwingDuration = 0.7f;

    [Header("Debug")]
    [SerializeField] bool autoStep = true;
    [SerializeField] bool logSteps = false;

    // Ground points, not ankle points: the rig's foot offset is added only when writing the transform.
    Vector3 currentPos;
    Vector3 targetPos;
    Vector3 liftoffPos;
    Vector3 currentNormal = Vector3.up;
    bool isStepping;
    bool retargeting;   // set for steps the trigger commits; StepTester's fixed steps don't chase
    float stepProgress;
    float activeStepHeight;

    // Read off the rig in Awake, so neither can be set wrong: −1 for the left leg, +1 for the right, and
    // how far this hip sits from the centreline.
    float side;
    float stanceHalfWidth;
    int groundMask;

    public bool IsStepping => isStepping;
    public bool IsPlanted => !isStepping;
    public Vector3 FootPos => currentPos;
    public Vector3 SupportPoint => currentPos + Forward * SoleCentreForward;
    public float LastLandTime { get; private set; }

    Vector3 Forward => ghostHips.forward;
    Vector3 Velocity => Flat(balanceSensor.COMVelocity);
    // The IK target is the ankle, not the sole; PlayerRig owns how far apart they are.
    float FootGroundOffset => playerRig.FootGroundOffset;

    void Awake()
    {
        if (!playerRig || !balanceSensor || !otherFoot || !ghostHips || !ghostThigh || !kneeHint)
        {
            Debug.LogError("FootPlacement: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        float hipOffset = Vector3.Dot(ghostThigh.position - ghostHips.position, ghostHips.right);
        side = Mathf.Sign(hipOffset);
        stanceHalfWidth = Mathf.Abs(hipOffset);

        // Everything but the player's own bodies, or the ground cast lands on the character itself.
        groundMask = ~LayerMask.GetMask("PlayerBody");

        // Start planted wherever the target was authored.
        currentPos = transform.position - Vector3.up * FootGroundOffset;
        targetPos = liftoffPos = currentPos;
    }

    void FixedUpdate()
    {
        if (autoStep)
            DecideStep();

        if (isStepping)
            AdvanceSwing();

        transform.SetPositionAndRotation(
            currentPos + Vector3.up * FootGroundOffset,
            Quaternion.FromToRotation(Vector3.up, currentNormal) * ghostHips.rotation);

        PoseKneeHint();
    }

    // --- Trigger ---------------------------------------------------------------------------------------

    void DecideStep()
    {
        // Gates: busy, one foot at a time (lifting both is a jump), and the double-support beat after
        // EITHER foot lands. Only waiting on the other foot lets a foot step again the tick it lands.
        if (Time.time < SpawnSettleTime) return;
        if (isStepping || otherFoot.IsStepping) return;
        float lastLand = Mathf.Max(LastLandTime, otherFoot.LastLandTime);
        if (lastLand > 0f && Time.time - lastLand < Dwell(Velocity.magnitude)) return;

        Vector3 capture = Flat(balanceSensor.COMPrediction);
        Vector3 mine = Flat(SupportPoint);
        Vector3 theirs = Flat(otherFoot.SupportPoint);

        // Measured from the line between the two soles, not their midpoint: a body resting over one
        // foot of a wide stance is fine, and measuring from the midpoint made that foot march in place.
        Vector3 error = capture - ClosestOnSegment(capture, mine, theirs);
        if (error.magnitude < stepMargin) return;

        if (!IsMyStep(error, capture, mine, theirs)) return;

        Vector3 landing = Landing(capture, currentPos, out Vector3 normal);
        if (Flat(landing - currentPos).magnitude < MinStepLength) return;

        if (logSteps)
            Debug.Log($"[{Time.time:F2}] {name} steps  capture {capture:F3}  error {error:F3} " +
                      $"(|{error.magnitude:F3}|)  from {currentPos:F3} to {landing:F3}  " +
                      $"COM vel {balanceSensor.COMVelocity:F2}  stride cap {StrideLength(Velocity.magnitude):F2} " +
                      $"swing {SwingDuration(Velocity.magnitude):F2}s", this);

        BeginStep(landing, normal, true);
    }

    // Which foot steps is a decision, not a race: without one both feet chase the same threshold and it
    // shuffles. Falling mostly sideways, the foot on that side steps out, because the trailing foot
    // would have to cross over. Otherwise the trailing foot steps: the one further from the capture
    // point, with DrunkWalkHome's 2 cm deadband and tie-breaks (the older plant, then the left foot) so
    // exactly one foot is ever the candidate and a parallel stance alternates rather than repeats.
    bool IsMyStep(Vector3 error, Vector3 capture, Vector3 mine, Vector3 theirs)
    {
        float lateral = Vector3.Dot(error, ghostHips.right);
        float forward = Vector3.Dot(error, Forward);

        if (Mathf.Abs(lateral) > Mathf.Abs(forward))
            return Mathf.Sign(lateral) == side;

        float myDistance = (capture - mine).magnitude;
        float theirDistance = (capture - theirs).magnitude;
        if (myDistance > theirDistance + 0.02f) return true;
        if (theirDistance > myDistance + 0.02f) return false;

        if (LastLandTime < otherFoot.LastLandTime - 0.01f) return true;
        if (otherFoot.LastLandTime < LastLandTime - 0.01f) return false;
        return side < 0f;
    }

    // --- Landing ---------------------------------------------------------------------------------------

    // Where a foot lifting off from `from` should land to catch the body. The capture point keeps
    // moving while the foot is in the air, so this is re-run during the swing (see AdvanceSwing).
    Vector3 Landing(Vector3 capture, Vector3 from, out Vector3 normal)
    {
        // Put the sole centre on the capture point, offset out to this leg's side of it.
        Vector3 fromSole = Flat(from + Forward * SoleCentreForward);
        Vector3 sole = fromSole + (capture - fromSole) * captureGain + ghostHips.right * (side * stanceHalfWidth);
        Vector3 landing = sole - Forward * SoleCentreForward;
        landing.y = from.y;

        landing = ClampStanceLateral(landing);
        landing = ClampStride(landing);
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

    // DrunkWalkHome's body-local lateral clamp, in the ghost hips' frame (hip centre, yaw only).
    Vector3 ClampStanceLateral(Vector3 landing)
    {
        Vector3 right = ghostHips.right;
        float lateral = side * Vector3.Dot(landing - ghostHips.position, right);
        float clamped = Mathf.Clamp(lateral, MinStanceHalfWidth, MaxStanceHalfWidth);
        return landing + right * (side * (clamped - lateral));
    }

    // How far past the other foot, along the direction of travel, this landing may be. Below a crawl
    // there is no direction of travel, and a catch step must be free to go where the capture point is.
    Vector3 ClampStride(Vector3 landing)
    {
        Vector3 velocity = Velocity;
        float speed = velocity.magnitude;
        if (speed < CrawlSpeed) return landing;

        Vector3 dir = velocity / speed;
        float ahead = Vector3.Dot(landing - otherFoot.FootPos, dir);
        float limit = StrideLength(speed);
        return ahead > limit ? landing - dir * (ahead - limit) : landing;
    }

    // The horizontal budget from the hip, given how far below it the ankle has to reach.
    Vector3 ClampToReach(Vector3 landing)
    {
        Vector3 hip = ghostThigh.position;
        float drop = hip.y - (landing.y + FootGroundOffset);
        float reach = playerRig.Chain * StrideReachSafety;
        float maxHorizontal = Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop));

        Vector3 offset = Vector3.ClampMagnitude(Flat(landing - hip), maxHorizontal);
        return new Vector3(hip.x + offset.x, landing.y, hip.z + offset.z);
    }

    // --- Gait ------------------------------------------------------------------------------------------

    // Step length (m) at a given speed. A ceiling, never a target: the capture point says where to land,
    // this only says how far one step may go at this pace.
    float StrideLength(float speed)
    {
        speed = Mathf.Max(speed, 0.05f);
        float leg = playerRig.Chain;
        float froude = speed * speed / (Gravity * leg);
        return FroudeCoefficient * Mathf.Pow(froude, FroudeExponent) * leg * 0.5f;
    }

    // The hip covers one stride of stance at this speed, and the duty factor says what share of the
    // cycle that stance is, which leaves the swing.
    float SwingDuration(float speed)
    {
        speed = Mathf.Max(speed, 0.05f);
        float stance = StrideLength(speed) / speed;
        float duty = Mathf.Lerp(DutyFactorSlow, DutyFactorFast, Mathf.InverseLerp(0.5f, 1.8f, speed));
        return Mathf.Clamp(stance * (1f - duty) / duty, MinSwingDuration, maxSwingDuration);
    }

    static float Dwell(float speed) =>
        DoubleSupportDwell * (1f - Mathf.Clamp01(speed / DwellFadeSpeed));

    // --- Swing -----------------------------------------------------------------------------------------

    // Swing rate follows the body's current speed, so a step that starts slow and gets shoved finishes
    // faster. A trigger-committed step re-aims at the moving capture point with a weight that fades to
    // zero by landing: without it the foot lands where the body was heading at liftoff, and each short
    // landing sets up the next, growing, step.
    //
    // DrunkWalkHome's lift curve, t²(1−t)⁴ normalised to peak 1 at t = ⅓: it rises early to clear the
    // ground and glides in, where a sine is symmetric and slaps down.
    void AdvanceSwing()
    {
        stepProgress += Time.fixedDeltaTime / SwingDuration(Velocity.magnitude);
        float t = Mathf.Clamp01(stepProgress);

        if (retargeting)
        {
            Vector3 aim = Landing(Flat(balanceSensor.COMPrediction), liftoffPos, out Vector3 aimNormal);
            float fade = 1f - t * t * (3f - 2f * t);
            float w = Mathf.Clamp01(RetargetStrength * Time.fixedDeltaTime * fade);
            targetPos = Vector3.Lerp(targetPos, aim, w);
            currentNormal = aimNormal;
        }

        float swing = t * t * (3f - 2f * t);
        Vector3 pos = Vector3.Lerp(liftoffPos, targetPos, swing);
        float lift = t * t * (1f - t) * (1f - t) * (1f - t) * (1f - t) * 45.5625f;
        pos.y += lift * StepHeightFor(targetPos);
        currentPos = pos;

        if (stepProgress >= 1f)
        {
            isStepping = false;
            currentPos = targetPos;
            LastLandTime = Time.time;
        }
    }

    // This step's lift, capped by how far it actually travels. Re-read every tick because the landing
    // moves; the cap only ever lowers the lift, so a re-aim can't make the foot jump upward. A rise is
    // added on top so a step up keeps its clearance.
    float StepHeightFor(Vector3 target)
    {
        Vector3 span = target - liftoffPos;
        float rise = Mathf.Max(0f, span.y);
        span.y = 0f;
        return Mathf.Min(activeStepHeight, span.magnitude * StepHeightPerSpan + rise);
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
        activeStepHeight = Mathf.Max(0f, stepHeight + Random.Range(-StepHeightVariance, StepHeightVariance));
    }

    // DrunkWalkHome's PoseKneeHint: the knee points along cross(legDir, body right), mirrored forward
    // if it ever points backward so a deep tuck can't fold the knee the wrong way. Crossing against the
    // body's right rather than world right is what keeps a toed-out foot from folding the knee inward.
    void PoseKneeHint()
    {
        Vector3 hip = ghostThigh.position;
        Vector3 ankle = transform.position;
        Vector3 forward = Forward;

        Vector3 knee = Vector3.Cross((ankle - hip).normalized, ghostHips.right);
        knee = knee.sqrMagnitude > 1e-6f ? knee.normalized : forward;

        float ahead = Vector3.Dot(knee, forward);
        if (ahead < 0f) knee -= 2f * ahead * forward;

        kneeHint.position = (hip + ankle) * 0.5f + knee * KneeOffsetDistance;
    }

    // --- Helpers ---------------------------------------------------------------------------------------

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
