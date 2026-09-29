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
// early-peak lift curve scaled to the step's length, the faded mid-flight re-aim, the body-local stance
// clamp and the knee hint posing. What stays ours is the trigger: DrunkWalkHome steps when the hip has
// travelled a stride past the foot, because its body is kinematic; ours steps when the capture point
// leaves the stance, because ours can fall. See resume-here §5.5–5.6.
//
// Positions are GROUND points: the spot on the floor under the ankle. The IK target is the ankle itself,
// so the rig's foot offset is added only when the transform is written.
[DefaultExecutionOrder(0)]
public class FootPlacement : MonoBehaviour
{
    const float Gravity = 9.81f;

    // Gait (DrunkWalkHome). Step length = c · Fr^β · L / 2 with Fr = v² / (gL); c is fitted so a 0.9 m
    // leg reproduces the generic adult fit, so on our 0.82 m leg it is dynamic similarity, not a guess.
    const float FroudeCoefficient = 2.35f;
    const float FroudeExponent = 0.25f;
    // Stance share of the stride: > 0.65 at a slow walk, 0.52–0.55 at a fast one.
    const float DutyFactorSlow = 0.65f;
    const float DutyFactorFast = 0.54f;
    const float MinSwingDuration = 0.2f;
    // The both-feet-down pause after a landing, fading to nothing by DoubleSupportFadeSpeed: slow walks
    // have one, fast gaits don't.
    const float DoubleSupportTime = 0.12f;
    const float DoubleSupportFadeSpeed = 4f;
    // Below this speed there is no direction of travel, so no stride ceiling and no stride to measure.
    const float CrawlSpeed = 0.15f;

    // Swing arc (DrunkWalkHome). Lift varies a little per step, and may not exceed this many metres per
    // metre the step travels, so a short shuffle doesn't lift like a full stride and read as marching.
    const float StepHeightVariance = 0.03f;
    const float MaxLiftPerStepLength = 0.8f;
    // How hard an in-flight foot re-aims at the moving capture point, faded to zero by landing.
    const float RetargetStrength = 10f;
    const float KneeHintDistance = 0.3f;

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
    // Fraction of the leg a landing may use. The plan's 0.9 leaves no reach at all: standing already
    // uses 0.75 m of drop against a 0.82 m leg. 0.98 allows ~0.29 m of horizontal reach from the hip.
    // See resume-here §5.3.
    const float MaxLegExtension = 0.98f;
    // No steps while the body drops onto its legs at spawn. Without it, the settling lean steps once.
    const float SpawnSettleTime = 1.0f;

    [Header("Rig")]
    [SerializeField] PlayerRig playerRig;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] FootPlacement otherFoot;
    // This leg's IK root. Reach is measured from here because it's the hip the solver actually uses.
    [SerializeField] Transform ghostThigh;
    // This leg's TwoBoneIK hint, re-aimed every tick; see PoseKneeHint.
    [SerializeField] Transform kneeHint;

    [Header("Tuning")]
    // 1 = land on the capture point and stop. Lower lands short so the body keeps going: walking.
    [SerializeField, Range(0.3f, 1f)] float captureGain = 1.0f;
    // How far the capture point may leave the stance before a foot steps.
    [SerializeField] float stepTriggerDistance = 0.08f;
    [SerializeField] float stepHeight = 0.10f;
    // The duty factor is a walking relation and asks for long swings at recovery speeds. Lower this
    // first if a catch step arrives too late.
    [SerializeField] float maxSwingDuration = 0.7f;

    [Header("Debug")]
    // Off leaves the foot to StepTester alone.
    [SerializeField] bool autoStep = true;
    [SerializeField] bool logSteps = false;

    Vector3 groundPoint;
    Vector3 groundNormal = Vector3.up;
    Vector3 liftoffPoint;
    Vector3 landingPoint;
    bool isStepping;
    // Trigger-committed steps re-aim at the capture point in flight; StepTester's fixed steps don't.
    bool chasesCapturePoint;
    float stepProgress;   // 0 → 1 over the swing
    float stepLift;       // this step's peak lift, before the length cap

    // Read off the rig in Awake, so none can be set wrong.
    Transform ghostHips;
    float side;           // −1 for the left leg, +1 for the right
    float hipHalfWidth;   // how far this hip sits from the centreline
    int groundMask;       // everything but the player's own bodies

    public bool IsStepping => isStepping;
    public Vector3 GroundPoint => groundPoint;
    // The sole's centre on the ground: where this foot bears weight, for balance.
    public Vector3 SupportPoint => groundPoint + Forward * SoleCentreForward;
    public float LastLandTime { get; private set; }

    Vector3 Forward => ghostHips.forward;
    Vector3 BodyVelocity => Flat(balanceSensor.CenterOfMassVelocity);
    float FootGroundOffset => playerRig.FootGroundOffset;

    void Awake()
    {
        if (!playerRig || !balanceSensor || !otherFoot || !ghostThigh || !kneeHint)
        {
            Debug.LogError("FootPlacement: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        ghostHips = playerRig.GhostHips;

        float hipOffset = Vector3.Dot(ghostThigh.position - ghostHips.position, ghostHips.right);
        side = Mathf.Sign(hipOffset);
        hipHalfWidth = Mathf.Abs(hipOffset);

        // Without this the ground cast lands on the character itself.
        groundMask = ~LayerMask.GetMask("PlayerBody");

        // Start planted wherever the target was authored.
        groundPoint = transform.position - Vector3.up * FootGroundOffset;
        landingPoint = liftoffPoint = groundPoint;
    }

    void FixedUpdate()
    {
        if (autoStep)
            DecideStep();

        if (isStepping)
            AdvanceSwing();

        transform.SetPositionAndRotation(
            groundPoint + Vector3.up * FootGroundOffset,
            Quaternion.FromToRotation(Vector3.up, groundNormal) * ghostHips.rotation);

        PoseKneeHint();
    }

    // --- Trigger ---------------------------------------------------------------------------------------

    void DecideStep()
    {
        // Gates: busy, one foot at a time (lifting both is a jump), and the double-support pause after
        // EITHER foot lands. Only waiting on the other foot lets a foot step again the tick it lands.
        if (Time.time < SpawnSettleTime) return;
        if (isStepping || otherFoot.IsStepping) return;
        float lastLand = Mathf.Max(LastLandTime, otherFoot.LastLandTime);
        if (lastLand > 0f && Time.time - lastLand < DoubleSupportPause(BodyVelocity.magnitude)) return;

        Vector3 capture = Flat(balanceSensor.CapturePoint);
        Vector3 mySole = Flat(SupportPoint);
        Vector3 otherSole = Flat(otherFoot.SupportPoint);

        // Measured from the line between the two soles, not their midpoint: a body resting over one
        // foot of a wide stance is fine, and measuring from the midpoint made that foot march in place.
        Vector3 error = capture - ClosestOnSegment(capture, mySole, otherSole);
        if (error.magnitude < stepTriggerDistance) return;

        if (!IsMyTurn(error, capture, mySole, otherSole)) return;

        Vector3 landing = ComputeLanding(capture, groundPoint, out Vector3 normal);
        if (Flat(landing - groundPoint).magnitude < MinStepLength) return;

        if (logSteps)
        {
            float speed = BodyVelocity.magnitude;
            Debug.Log($"[{Time.time:F2}] {name} steps  capture {capture:F3}  error {error:F3} " +
                      $"(|{error.magnitude:F3}|)  from {groundPoint:F3} to {landing:F3}  " +
                      $"COM vel {balanceSensor.CenterOfMassVelocity:F2}  stride cap {StrideLength(speed):F2} " +
                      $"swing {SwingDuration(speed):F2}s", this);
        }

        BeginStep(landing, normal, true);
    }

    // Which foot steps is a decision, not a race: without one both feet chase the same threshold and it
    // shuffles. Falling mostly sideways, the foot on that side steps out, because the trailing foot
    // would have to cross over. Otherwise the trailing foot steps: the one further from the capture
    // point, with DrunkWalkHome's 2 cm deadband and tie-breaks (the older plant, then the left foot) so
    // exactly one foot is ever the candidate and a parallel stance alternates rather than repeats.
    bool IsMyTurn(Vector3 error, Vector3 capture, Vector3 mySole, Vector3 otherSole)
    {
        float lateral = Vector3.Dot(error, ghostHips.right);
        float forward = Vector3.Dot(error, Forward);

        if (Mathf.Abs(lateral) > Mathf.Abs(forward))
            return Mathf.Sign(lateral) == side;

        float myDistance = (capture - mySole).magnitude;
        float otherDistance = (capture - otherSole).magnitude;
        if (myDistance > otherDistance + 0.02f) return true;
        if (otherDistance > myDistance + 0.02f) return false;

        if (LastLandTime < otherFoot.LastLandTime - 0.01f) return true;
        if (otherFoot.LastLandTime < LastLandTime - 0.01f) return false;
        return side < 0f;
    }

    // --- Landing ---------------------------------------------------------------------------------------

    // Where a foot lifting off from `liftoff` should land to catch the body. The capture point keeps
    // moving while the foot is in the air, so this is re-run during the swing (see AdvanceSwing).
    Vector3 ComputeLanding(Vector3 capture, Vector3 liftoff, out Vector3 normal)
    {
        // Put the sole centre on the capture point, offset out to this leg's side of it.
        Vector3 liftoffSole = Flat(liftoff + Forward * SoleCentreForward);
        Vector3 sole = liftoffSole + (capture - liftoffSole) * captureGain
                     + ghostHips.right * (side * hipHalfWidth);
        Vector3 landing = sole - Forward * SoleCentreForward;
        landing.y = liftoff.y;

        landing = ClampStanceWidth(landing);
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
    Vector3 ClampStanceWidth(Vector3 landing)
    {
        Vector3 right = ghostHips.right;
        float halfWidth = side * Vector3.Dot(landing - ghostHips.position, right);
        float clamped = Mathf.Clamp(halfWidth, MinStanceHalfWidth, MaxStanceHalfWidth);
        return landing + right * (side * (clamped - halfWidth));
    }

    // How far past the other foot, along the direction of travel, this landing may be. Below a crawl
    // there is no direction of travel, and a catch step must be free to go where the capture point is.
    Vector3 ClampStride(Vector3 landing)
    {
        Vector3 velocity = BodyVelocity;
        float speed = velocity.magnitude;
        if (speed < CrawlSpeed) return landing;

        Vector3 dir = velocity / speed;
        float ahead = Vector3.Dot(landing - otherFoot.GroundPoint, dir);
        float limit = StrideLength(speed);
        return ahead > limit ? landing - dir * (ahead - limit) : landing;
    }

    // The horizontal budget from the hip, given how far below it the ankle has to reach.
    Vector3 ClampToReach(Vector3 landing)
    {
        Vector3 hip = ghostThigh.position;
        float drop = hip.y - (landing.y + FootGroundOffset);
        float reach = playerRig.LegLength * MaxLegExtension;
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
        float leg = playerRig.LegLength;
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

    static float DoubleSupportPause(float speed) =>
        DoubleSupportTime * (1f - Mathf.Clamp01(speed / DoubleSupportFadeSpeed));

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
        stepProgress += Time.fixedDeltaTime / SwingDuration(BodyVelocity.magnitude);
        float t = Mathf.Clamp01(stepProgress);

        if (chasesCapturePoint)
        {
            Vector3 aim = ComputeLanding(Flat(balanceSensor.CapturePoint), liftoffPoint, out Vector3 aimNormal);
            float fade = 1f - t * t * (3f - 2f * t);
            float weight = Mathf.Clamp01(RetargetStrength * Time.fixedDeltaTime * fade);
            landingPoint = Vector3.Lerp(landingPoint, aim, weight);
            groundNormal = aimNormal;
        }

        float travel = t * t * (3f - 2f * t);
        Vector3 point = Vector3.Lerp(liftoffPoint, landingPoint, travel);
        float lift = t * t * (1f - t) * (1f - t) * (1f - t) * (1f - t) * 45.5625f;
        point.y += lift * PeakLift();
        groundPoint = point;

        if (stepProgress >= 1f)
        {
            isStepping = false;
            groundPoint = landingPoint;
            LastLandTime = Time.time;
        }
    }

    // This step's lift, capped by how far it actually travels. Re-read every tick because the landing
    // moves; the cap only ever lowers the lift, so a re-aim can't make the foot jump upward. A rise is
    // added on top so a step up keeps its clearance.
    float PeakLift()
    {
        Vector3 travel = landingPoint - liftoffPoint;
        float rise = Mathf.Max(0f, travel.y);
        travel.y = 0f;
        return Mathf.Min(stepLift, travel.magnitude * MaxLiftPerStepLength + rise);
    }

    // Commits a step from wherever the foot is now, to a fixed landing. The normal is taken at commit
    // rather than on landing, which only matters on uneven ground; the floor here is flat.
    public void BeginStep(Vector3 landing, Vector3 normal) => BeginStep(landing, normal, false);

    void BeginStep(Vector3 landing, Vector3 normal, bool chaseCapturePoint)
    {
        liftoffPoint = groundPoint;
        landingPoint = landing;
        groundNormal = normal;
        stepProgress = 0f;
        isStepping = true;
        chasesCapturePoint = chaseCapturePoint;
        stepLift = Mathf.Max(0f, stepHeight + Random.Range(-StepHeightVariance, StepHeightVariance));
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

        kneeHint.position = (hip + ankle) * 0.5f + knee * KneeHintDistance;
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
        Gizmos.DrawWireSphere(groundPoint, 0.03f);

        if (!isStepping) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(liftoffPoint, landingPoint);
        Gizmos.DrawWireSphere(landingPoint, 0.04f);
    }
}
