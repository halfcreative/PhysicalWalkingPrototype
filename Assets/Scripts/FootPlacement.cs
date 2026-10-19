using UnityEngine;

// Where one foot should be, written to this transform: the FootTarget the ghost leg's TwoBoneIK reads.
// Planted, the target is fixed in the world; stepping, it arcs from liftoff to landing.
//
// It also decides when to step. Once the centre of mass leaves the stance, a foot steps toward the
// capture point. Landing on it stops the body; landing short (captureGain < 1) keeps it going, which is
// walking. Stride, swing time and lift only shape a step once it's committed.
//
// Positions are ground points (the floor under the ankle). The rig's foot offset is added only when the
// transform is written.
[DefaultExecutionOrder(0)]
public class FootPlacement : MonoBehaviour
{
    // --- Gait ---
    // Froude stride: length = c · Fr^β · L / 2, Fr = v² / (gL).
    const float FroudeCoefficient = 2.35f;
    const float FroudeExponent = 0.25f;
    // Stance share of the stride, slow walk to fast walk.
    const float DutyFactorSlow = 0.65f;
    const float DutyFactorFast = 0.54f;
    const float MinSwingDuration = 0.2f;
    // Both-feet-down pause after a landing, fading out by DoubleSupportFadeSpeed.
    const float DoubleSupportTime = 0.12f;
    const float DoubleSupportFadeSpeed = 4f;
    // Below this speed there is no direction of travel to cap a stride along.
    const float CrawlSpeed = 0.15f;

    // --- Swing ---
    // Random per-step lift variation, m.
    const float StepHeightVariance = 0.03f;
    // Lift may not exceed this many metres per metre of step, so a shuffle doesn't march.
    const float MaxLiftPerStepLength = 0.8f;
    // Cap on the hip sag added to the lift. More bends the knee to 90° and it buckles on landing.
    const float MaxSagLift = 0.05f;
    // How hard an in-flight foot re-aims at the moving capture point.
    const float RetargetStrength = 10f;
    const float KneeHintDistance = 0.3f;

    // --- Reach ---
    // The sole's centre sits this far ahead of the ankle. Balance is judged at the sole.
    const float SoleCentreForward = 0.05f;
    // Shorter steps are skipped: they only unload the leg.
    const float MinStepLength = 0.04f;
    // Band a foot may plant in, as half-width from the hip centreline.
    const float MinStanceHalfWidth = 0.08f;
    const float MaxStanceHalfWidth = 0.45f;
    // Fraction of the leg a landing may use.
    const float MaxLegExtension = 0.98f;
    // Fraction of the leg a mid-swing point may use, keeping the knee bent.
    const float SwingLegExtension = 0.9f;

    // --- Step choice ---
    // Margins for picking which foot steps by distance and by landing time.
    const float StepChoiceDistanceMargin = 0.02f;
    const float StepChoiceTimeMargin = 0.01f;
    // How far outside a foot's outer edge the capture point must be before only that foot may step.
    const float OutsideFootMargin = 0.03f;
    // Sole-to-sole width past which the outer foot closes in.
    const float GatherStanceWidth = 0.45f;
    // A crossover lands at least this far past and ahead of the other foot, and no further to the wrong
    // side of the hips than the hip can adduct.
    const float CrossoverMinPast = 0.05f;
    const float CrossoverMinAhead = 0.12f;
    const float CrossoverMaxHalfWidth = 0.3f;

    [Header("Rig")]
    [SerializeField] PlayerRig playerRig;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] PlayerController playerController;
    [SerializeField] FootPlacement otherFoot;
    // This leg's IK root; reach is measured from here.
    [SerializeField] Transform ghostThigh;
    // This leg's TwoBoneIK hint.
    [SerializeField] Transform kneeHint;

    [Header("Tuning")]
    // 1 lands on the capture point and stops. Lower lands short so the body keeps going.
    [SerializeField, Range(0.3f, 1f)] float captureGain = 1.0f;
    // How far the centre of mass may leave the stance before a foot steps.
    [SerializeField] float stepTriggerDistance = 0.08f;
    [SerializeField] float stepHeight = 0.10f;
    // Lower this first if a catch step arrives too late.
    [SerializeField] float maxSwingDuration = 0.7f;
    // Degrees the heading may turn from a planted foot before it steps to follow. The hip twists ±30°.
    [SerializeField, Range(5f, 30f)] float turnStepAngle = 25f;
    // Extra step length, m, at full input along the direction of the fall.
    [SerializeField] float inputStepReach = 0.15f;
    // Sideways speed, m/s, past which the trailing foot crosses over instead of the lead foot stepping out.
    [SerializeField] float crossoverSpeed = 0.35f;
    // A sidestep out to this foot's side lands this multiple of the sideways distance to the capture point.
    [SerializeField] float sideStepGain = 1.25f;

    [Header("Debug")]
    // Off leaves the foot to StepTester.
    [SerializeField] bool autoStep = true;
    [SerializeField] bool logSteps = false;

    Vector3 groundPoint;
    Vector3 groundNormal = Vector3.up;
    // Authored target pose relative to the character root, which NGO moves to its spawn point after Awake.
    Vector3 spawnLocalPosition;
    Quaternion spawnLocalRotation;
    // Yaw held while planted; it turns to the heading only during a swing.
    Quaternion facing = Quaternion.identity;
    Quaternion liftoffFacing = Quaternion.identity;
    Vector3 liftoffPoint;
    Vector3 landingPoint;
    bool isStepping;
    bool isCrossing;
    // Automatic steps re-aim at the capture point in flight; StepTester's don't.
    bool retargetsInFlight;
    float stepProgress;    // 0 → 1 over the swing
    float stepPeakLift;    // before the step-length cap

    // Read off the rig at Awake.
    Transform ghostHips;
    float side;            // −1 left leg, +1 right
    float hipHalfWidth;    // this hip's distance from the centreline
    int groundMask;        // everything but the player's own bodies

    public bool IsStepping => isStepping;
    public bool IsCrossing => isStepping && isCrossing;
    // 0 → 1 through the swing; 1 while planted.
    public float SwingProgress => isStepping ? Mathf.Clamp01(stepProgress) : 1f;
    public Vector3 GroundPoint => groundPoint;
    // The sole's centre on the ground: where this foot bears weight.
    public Vector3 SupportPoint => groundPoint + FootForward * SoleCentreForward;
    // 0 until the first landing.
    public float LastLandTime { get; private set; }
    public float TimeSinceLanding => LastLandTime > 0f ? Time.time - LastLandTime : float.PositiveInfinity;
    // Degrees between this foot's facing and the heading.
    public float TurnAngle => Quaternion.Angle(facing, playerController.Heading);

    Vector3 BodyForward => ghostHips.forward;
    Vector3 BodyRight => ghostHips.right;
    Vector3 FootForward => facing * Vector3.forward;
    Vector3 FootRight => facing * Vector3.right;
    Vector3 RealHips => ghostHips.position - playerRig.GhostHipOffset;
    Vector3 BodyVelocity => balanceSensor.CenterOfMassVelocity.Flat();
    float FootGroundOffset => playerRig.FootGroundOffset;

    void Awake()
    {
        if (!playerRig || !balanceSensor || !playerController || !otherFoot || !ghostThigh || !kneeHint)
        {
            Debug.LogError("FootPlacement: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        ghostHips = playerRig.GhostHips;

        float hipOffset = Vector3.Dot(ghostThigh.position - ghostHips.position, ghostHips.right);
        side = Mathf.Sign(hipOffset);
        hipHalfWidth = Mathf.Abs(hipOffset);

        groundMask = ~LayerMask.GetMask("PlayerBody");

        Transform root = playerRig.transform;
        spawnLocalPosition = root.InverseTransformPoint(transform.position);
        spawnLocalRotation = Quaternion.Inverse(root.rotation) * transform.rotation;
        PlantAtSpawn();
    }

    // Plants the foot at its authored pose, cancelling any step.
    public void PlantAtSpawn()
    {
        Transform root = playerRig.transform;
        Vector3 spawnPosition = root.TransformPoint(spawnLocalPosition);
        Quaternion spawnRotation = root.rotation * spawnLocalRotation;

        isStepping = false;
        isCrossing = false;
        groundNormal = Vector3.up;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        groundPoint = spawnPosition - Vector3.up * FootGroundOffset;
        landingPoint = liftoffPoint = groundPoint;
        facing = liftoffFacing = (spawnRotation * Vector3.forward).ToYawRotation();
        LastLandTime = 0f;
    }

    void FixedUpdate()
    {
        if (autoStep && !playerController.IsFallen)
            DecideStep();

        if (isStepping)
            AdvanceSwing();

        // A planted leg feels the ghost hips' balance shift; a swinging leg must not, or it lands off by it.
        Vector3 ikShift = isStepping ? playerRig.GhostHipOffset : Vector3.zero;
        transform.SetPositionAndRotation(
            groundPoint + Vector3.up * FootGroundOffset + ikShift,
            Quaternion.FromToRotation(Vector3.up, groundNormal) * facing);

        PoseKneeHint();
    }

    // --- Trigger ---------------------------------------------------------------------------------------

    void DecideStep()
    {
        // One foot at a time, and not during the double-support pause after either foot lands.
        if (isStepping || otherFoot.IsStepping) return;
        float lastLand = Mathf.Max(LastLandTime, otherFoot.LastLandTime);
        if (lastLand > 0f && Time.time - lastLand < DoubleSupportPause(BodyVelocity.magnitude)) return;

        Vector3 capture = balanceSensor.CapturePoint.Flat();
        Vector3 mySole = SupportPoint.Flat();
        Vector3 otherSole = otherFoot.SupportPoint.Flat();

        // Triggered by the centre of mass (where the body is), not the capture point (where it's heading),
        // so a step catches a body already tipping. Measured from the line between the soles, so resting
        // over one foot of a wide stance isn't a fall.
        Vector3 com = balanceSensor.CenterOfMass.Flat();
        Vector3 comError = com - ClosestOnSegment(com, mySole, otherSole);
        bool isCatchStep = comError.magnitude >= stepTriggerDistance;
        bool crossing = false;

        // Which foot steps is chosen by where the capture point is, not the centre of mass.
        Vector3 captureError = capture - ClosestOnSegment(capture, mySole, otherSole);

        if (CrossoverDue(mySole, otherSole, out bool thisFootSteps, out float travelSign))
        {
            if (!thisFootSteps) return;
            // A trailing foot already on the wrong side steps back out: an ordinary step.
            crossing = side != travelSign;
            isCatchStep = true;
        }
        else if (isCatchStep)
        {
            if (!ShouldCatchStep(captureError, capture, mySole, otherSole)) return;
        }
        else if (!ShouldTurnStep() && !ShouldGatherStep(com, mySole, otherSole)) return;

        Vector3 landing = ComputeLanding(capture, groundPoint, SwingDuration(BodyVelocity.magnitude), crossing,
                                         out Vector3 normal);
        // A turn step may land almost where it lifted off: it moves the facing.
        if (isCatchStep && (landing - groundPoint).Flat().magnitude < MinStepLength) return;

        if (logSteps)
        {
            float speed = BodyVelocity.magnitude;
            string kind = crossing ? "crosses" : isCatchStep ? "steps" : "turn-steps";
            Debug.Log($"[{Time.time:F2}] {name} {kind}  turn {TurnAngle:F1}°  capture {capture:F3}  " +
                      $"error {comError:F3} (|{comError.magnitude:F3}|)  from {groundPoint:F3} to {landing:F3}  " +
                      $"COM vel {balanceSensor.CenterOfMassVelocity:F2}  stride cap {StrideLength(speed):F2} " +
                      $"swing {SwingDuration(speed):F2}s", this);
        }

        BeginStep(landing, normal, true, crossing);
    }

    // Moving fast and mostly sideways, with the lead foot just landed: the trailing foot crosses in front
    // and lands beyond it. Stepping the lead foot out again leaves the body standing on the inner foot and
    // rolling over. Lead and trail are by position along the travel, so both feet agree on which steps.
    // travelSign is +1 moving right, −1 left.
    bool CrossoverDue(Vector3 mySole, Vector3 otherSole, out bool thisFootSteps, out float travelSign)
    {
        thisFootSteps = false;
        float lateralSpeed = Vector3.Dot(BodyVelocity, BodyRight);
        travelSign = Mathf.Sign(lateralSpeed);

        if (Mathf.Abs(lateralSpeed) < crossoverSpeed ||
            Mathf.Abs(lateralSpeed) <= Mathf.Abs(Vector3.Dot(BodyVelocity, BodyForward)))
            return false;

        bool iLead = travelSign * Vector3.Dot(mySole - otherSole, BodyRight) > 0f;
        float leadLand = iLead ? LastLandTime : otherFoot.LastLandTime;
        float trailLand = iLead ? otherFoot.LastLandTime : LastLandTime;
        if (leadLand <= trailLand) return false;

        thisFootSteps = !iLead;
        return true;
    }

    // Exactly one foot must be the candidate, or both chase the threshold and shuffle.
    //   1. A capture point outside one foot's outer edge belongs to that foot, if it planted first.
    //   2. Falling mostly sideways, the foot on that side steps out.
    //   3. Otherwise the foot further from the capture point, then the older plant, then the left.
    bool ShouldCatchStep(Vector3 captureError, Vector3 capture, Vector3 mySole, Vector3 otherSole)
    {
        bool olderPlant = LastLandTime <= otherFoot.LastLandTime;
        if (olderPlant && side * Vector3.Dot(capture - mySole, BodyRight) > OutsideFootMargin) return true;
        if (!olderPlant && -side * Vector3.Dot(capture - otherSole, BodyRight) > OutsideFootMargin) return false;

        float lateral = Vector3.Dot(captureError, BodyRight);
        float forward = Vector3.Dot(captureError, BodyForward);
        if (Mathf.Abs(lateral) > Mathf.Abs(forward))
            return Mathf.Sign(lateral) == side;

        float myDistance = (capture - mySole).magnitude;
        float otherDistance = (capture - otherSole).magnitude;
        if (myDistance > otherDistance + StepChoiceDistanceMargin) return true;
        if (otherDistance > myDistance + StepChoiceDistanceMargin) return false;

        if (LastLandTime < otherFoot.LastLandTime - StepChoiceTimeMargin) return true;
        if (otherFoot.LastLandTime < LastLandTime - StepChoiceTimeMargin) return false;
        return side < 0f;
    }

    // The heading has turned turnStepAngle away from this planted foot. The foot turned further goes
    // first (ties to the left).
    bool ShouldTurnStep()
    {
        float mine = TurnAngle;
        if (mine < turnStepAngle) return false;
        float other = otherFoot.TurnAngle;
        return mine > other || (mine == other && side < 0f);
    }

    // Nothing is falling, but the feet are too wide or crossed: the foot further from the centre of mass
    // closes in. This turns a steady sideways push into a sidestep: out, close, out.
    bool ShouldGatherStep(Vector3 com, Vector3 mySole, Vector3 otherSole)
    {
        float across = side * Vector3.Dot(mySole - otherSole, BodyRight);
        bool crossed = across < 0f;
        if (Mathf.Abs(across) < GatherStanceWidth && !crossed) return false;
        return (com - mySole).sqrMagnitude > (com - otherSole).sqrMagnitude;
    }

    // --- Landing ---------------------------------------------------------------------------------------

    // Where a foot lifting off from `liftoff` should land, `timeToLand` seconds from now. Everything is
    // judged where the body will be at touchdown, carried on by its velocity.
    Vector3 ComputeLanding(Vector3 capture, Vector3 liftoff, float timeToLand, bool crossing, out Vector3 normal)
    {
        Vector3 drift = BodyVelocity * timeToLand;

        // The sole on the capture point, offset out by this hip's half-width. Short by captureGain only
        // along the body's forward; an outward sidestep overshoots by sideStepGain. A crossover isn't
        // offset: its own side is behind it.
        Vector3 liftoffSole = (liftoff + BodyForward * SoleCentreForward).Flat();
        Vector3 toCapture = capture + drift - liftoffSole;
        Vector3 along = Vector3.Project(toCapture, BodyForward);
        Vector3 across = toCapture - along;
        bool outward = !crossing && side * Vector3.Dot(across, BodyRight) > 0f;
        Vector3 sole = liftoffSole + along * captureGain + across * (outward ? sideStepGain : 1f)
                     + IntoFallReach(toCapture)
                     + (crossing ? Vector3.zero : BodyRight * (side * hipHalfWidth));
        Vector3 landing = sole - BodyForward * SoleCentreForward;
        landing.y = liftoff.y;

        landing = crossing ? ClampCrossover(landing, drift) : ClampStanceWidth(landing, drift);
        landing = ClampStride(landing);
        landing = ClampToReach(landing, drift);

        normal = Vector3.up;
        if (Physics.Raycast(landing + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f,
                            groundMask, QueryTriggerInteraction.Ignore))
        {
            landing = hit.point;
            normal = hit.normal;
        }

        return landing;
    }

    // Extra reach from the player's input along `fall`. Only ever further, and never from forward input:
    // a walk is a forward fall, and lengthening its steps would brake it.
    Vector3 IntoFallReach(Vector3 fall)
    {
        if (fall.sqrMagnitude < 1e-6f) return Vector3.zero;
        Vector3 input = playerController.MoveInputWorld;
        Vector3 heading = playerController.Heading * Vector3.forward;
        float ahead = Vector3.Dot(input, heading);
        if (ahead > 0f) input -= heading * ahead;

        Vector3 direction = fall.normalized;
        float into = Vector3.Dot(input, direction);
        return into > 0f ? direction * (into * inputStepReach) : Vector3.zero;
    }

    // Keeps the landing within the stance band from the predicted hips, and on this leg's side of the
    // other foot.
    Vector3 ClampStanceWidth(Vector3 landing, Vector3 drift)
    {
        float halfWidth = side * Vector3.Dot(landing - (RealHips + drift), BodyRight);
        float clamped = Mathf.Clamp(halfWidth, MinStanceHalfWidth, MaxStanceHalfWidth);
        landing += BodyRight * (side * (clamped - halfWidth));

        float fromOther = side * Vector3.Dot(landing - otherFoot.GroundPoint, BodyRight);
        float minFromOther = 2f * MinStanceHalfWidth;
        return fromOther >= minFromOther ? landing : landing + BodyRight * (side * (minFromOther - fromOther));
    }

    // ClampStanceWidth for a crossover: past and ahead of the other foot, within the hip's adduction.
    Vector3 ClampCrossover(Vector3 landing, Vector3 drift)
    {
        Vector3 other = otherFoot.GroundPoint;

        // −side is the way it crosses.
        float past = -side * Vector3.Dot(landing - other, BodyRight);
        if (past < CrossoverMinPast) landing += BodyRight * (-side * (CrossoverMinPast - past));

        float wrongSide = -side * Vector3.Dot(landing - (RealHips + drift), BodyRight);
        if (wrongSide > CrossoverMaxHalfWidth) landing += BodyRight * (side * (wrongSide - CrossoverMaxHalfWidth));

        float ahead = Vector3.Dot(landing - other, BodyForward);
        return ahead >= CrossoverMinAhead ? landing : landing + BodyForward * (CrossoverMinAhead - ahead);
    }

    // Caps how far past the other foot the landing goes along the body's forward. Not along the velocity:
    // that capped sideways catch steps with a forward stride length.
    Vector3 ClampStride(Vector3 landing)
    {
        float along = Vector3.Dot(BodyVelocity, BodyForward);
        float speed = Mathf.Abs(along);
        if (speed < CrawlSpeed) return landing;

        Vector3 direction = BodyForward * Mathf.Sign(along);
        float ahead = Vector3.Dot(landing - otherFoot.GroundPoint, direction);
        float limit = StrideLength(speed);
        return ahead > limit ? landing - direction * (ahead - limit) : landing;
    }

    // Keeps the landing within the leg's horizontal reach from the predicted hip.
    Vector3 ClampToReach(Vector3 landing, Vector3 drift)
    {
        Vector3 hip = ghostThigh.position - playerRig.GhostHipOffset + drift;
        float drop = hip.y - (landing.y + FootGroundOffset);
        float reach = playerRig.LegLength * MaxLegExtension;
        float maxHorizontal = Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop));

        Vector3 offset = Vector3.ClampMagnitude((landing - hip).Flat(), maxHorizontal);
        return new Vector3(hip.x + offset.x, landing.y, hip.z + offset.z);
    }

    // --- Gait ------------------------------------------------------------------------------------------

    // The longest step at this speed, m. A ceiling, not a target.
    float StrideLength(float speed)
    {
        speed = Mathf.Max(speed, 0.05f);
        float leg = playerRig.LegLength;
        float froude = speed * speed / (Physics.gravity.magnitude * leg);
        return FroudeCoefficient * Mathf.Pow(froude, FroudeExponent) * leg * 0.5f;
    }

    // Stance time at this speed and the duty factor give the swing time.
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

    // Swing rate follows the current speed. Automatic steps re-aim at the capture point with a weight that
    // fades to zero by landing. The lift curve is √sin(πt), holding height through mid-swing because the
    // real foot trails the ghost by ~0.15 s; the hip sag is added on sin(πt) so touchdown is unaffected.
    void AdvanceSwing()
    {
        float swingDuration = SwingDuration(BodyVelocity.magnitude);
        stepProgress += Time.fixedDeltaTime / swingDuration;
        float t = Mathf.Clamp01(stepProgress);
        float travel = t * t * (3f - 2f * t);

        if (retargetsInFlight)
        {
            Vector3 aim = ComputeLanding(balanceSensor.CapturePoint.Flat(), liftoffPoint, (1f - t) * swingDuration,
                                         isCrossing, out Vector3 aimNormal);
            float weight = Mathf.Clamp01(RetargetStrength * Time.fixedDeltaTime * (1f - travel));
            landingPoint = Vector3.Lerp(landingPoint, aim, weight);
            groundNormal = aimNormal;
        }

        // The foot turns to the heading over the swing and holds that once planted.
        facing = Quaternion.Slerp(liftoffFacing, playerController.Heading, travel);
        Vector3 point = Vector3.Lerp(liftoffPoint, landingPoint, travel);
        float arc = Mathf.Sin(t * Mathf.PI);
        point.y += Mathf.Sqrt(arc) * PeakLift() + arc * Mathf.Min(playerRig.HipSag, MaxSagLift);
        groundPoint = KeepSwingInReach(point, arc);

        if (stepProgress >= 1f)
        {
            isStepping = false;
            groundPoint = landingPoint;
            LastLandTime = Time.time;
        }
    }

    // Pulls a swing point toward the hip until the leg reaches it with a bent knee, so a foot lifting off
    // far behind doesn't drag forward on a locked leg. Eased with the arc, so liftoff and touchdown are
    // untouched.
    Vector3 KeepSwingInReach(Vector3 point, float arc)
    {
        Vector3 offset = Vector3.up * FootGroundOffset + playerRig.GhostHipOffset;
        Vector3 hip = ghostThigh.position;
        Vector3 ankle = point + offset;

        float reach = playerRig.LegLength * Mathf.Lerp(MaxLegExtension, SwingLegExtension, Mathf.Sqrt(arc));
        Vector3 fromHip = ankle - hip;
        if (fromHip.sqrMagnitude <= reach * reach) return point;
        return hip + fromHip.normalized * reach - offset;
    }

    // This step's lift, capped by its length, plus any rise. Re-read each tick as the landing moves.
    float PeakLift()
    {
        Vector3 travel = landingPoint - liftoffPoint;
        float rise = Mathf.Max(0f, travel.y);
        travel.y = 0f;
        return Mathf.Min(stepPeakLift, travel.magnitude * MaxLiftPerStepLength + rise);
    }

    // A fixed step from wherever the foot is now, for StepTester.
    public void BeginStep(Vector3 landing, Vector3 normal) => BeginStep(landing, normal, false, false);

    void BeginStep(Vector3 landing, Vector3 normal, bool retarget, bool crossing)
    {
        isCrossing = crossing;
        liftoffPoint = groundPoint;
        liftoffFacing = facing;
        landingPoint = landing;
        groundNormal = normal;
        stepProgress = 0f;
        isStepping = true;
        retargetsInFlight = retarget;
        stepPeakLift = Mathf.Max(0f, stepHeight + Random.Range(-StepHeightVariance, StepHeightVariance));
    }

    // Points the knee along cross(leg, foot right), flipped forward if it points back. Using the foot's
    // right, not the body's, keeps the knee over the foot, so a body turned over a planted foot twists the
    // leg at the hip.
    void PoseKneeHint()
    {
        Vector3 hip = ghostThigh.position;
        Vector3 ankle = transform.position;
        Vector3 forward = FootForward;

        Vector3 knee = Vector3.Cross((ankle - hip).normalized, FootRight);
        knee = knee.sqrMagnitude > 1e-6f ? knee.normalized : forward;

        float ahead = Vector3.Dot(knee, forward);
        if (ahead < 0f) knee -= 2f * ahead * forward;

        kneeHint.position = (hip + ankle) * 0.5f + knee * KneeHintDistance;
    }

    static Vector3 ClosestOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 1e-8f) return a;
        return a + ab * Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
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
