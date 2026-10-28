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
    // A swing whose real sole ends this far below the landing has failed: its toe caught the edge.
    const float TouchdownTolerance = 0.06f;
    // How far above the top of a step up's face the real sole must be to go on forward, m.
    const float StepUpClearMargin = 0.03f;
    // The real foot's reach from the ankle along its length, m: toe ahead, heel behind.
    const float ToeAhead = 0.2f;
    const float HeelBehind = 0.1f;
    // How far a foot keeps clear of a step's face, m.
    const float EdgeMargin = 0.04f;
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
    // This leg's physical foot. Its ankle is checked by the step-up hold and at touchdown; see TouchdownPoint.
    [SerializeField] Collider foot;

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
    // Pushing (input at least stallInput) but slower than stallSpeed, m/s, for stallTime, s, since the last
    // landing: the body is pinned against something, so the rear foot steps stallStepReach, m, past the other
    // along the input. Measured from the capture point, behind the front foot, it fell short of the next stair.
    // A climbing step (IsClimbing) is the same step, at the same input.
    [SerializeField] float stallInput = 0.3f;
    [SerializeField] float stallSpeed = 0.1f;
    [SerializeField] float stallTime = 0.5f;
    [SerializeField] float stallStepReach = 0.35f;

    [Header("Step Up / Down")]
    // Swing fraction by which a step up has reached its landing height, and from which a step down starts to
    // descend, so the foot clears an edge before crossing it rather than meeting it halfway up.
    [SerializeField, Range(0.1f, 1f)] float stepUpRiseBy = 0.5f;
    [SerializeField, Range(0f, 0.9f)] float stepDownDescendFrom = 0.5f;
    // Swing fraction before a step up's foot starts forward: it lifts first, so it isn't driven into the
    // step's face while still low.
    [SerializeField, Range(0f, 0.8f)] float stepUpForwardFrom = 0.25f;
    // Longest a step up's swing waits, s, with its toe at the face it crosses first and its target at full
    // height (stepUpRiseBy), for the real sole to clear the face's top. Until it has, the foot goes no further
    // forward than its toe EdgeMargin short of that face. The real foot trails its target, and on a high step
    // it was carried forward still under the edge and stubbed on the face.
    [SerializeField] float stepUpMaxHold = 0.6f;
    // Extra swing time per metre of rise, s/m: the real foot trails the ghost, so a high step needs longer
    // for the real foot, not just the target, to clear the edge. Kept small: the hold covers a foot that
    // hasn't, and up stairs, where each stride rises two stairs, more made every step a slow one.
    [SerializeField] float stepUpTimePerMetre = 0.4f;
    // Least clearance over a step up's edge, m, however short the step.
    [SerializeField] float stepUpClearance = 0.08f;
    // Chance per step up, times drunkAmount, that the foot is careless: it doesn't lift first, but rises and
    // goes forward together like a step on the level, and its toe may catch the face. A trip, left to physics,
    // touchdown and the catch steps. The lift-first is what keeps a climbing toe off the riser: without it on
    // every step, drunk or not, nearly every climb tripped. Every step a little sloppy didn't work: a climb
    // shrugged off skipping up to half of it, and misjudging the hold's clearance barely mattered.
    [SerializeField, Range(0f, 1f)] float drunkCarelessStepChance = 0.14f;

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
    // Where the step is aimed, and where that is on the ground and off any edge. Re-aiming blends the aim, so
    // the shifts that keep the landing off edges don't build up from tick to tick.
    Vector3 aimPoint;
    Vector3 landingPoint;
    bool isStepping;
    bool isCrossing;
    // Automatic steps re-aim at the capture point in flight; stall steps and StepTester's don't.
    bool retargetsInFlight;
    float stepProgress;    // 0 → 1 over the swing, plus any hold
    float stepUpHeld;      // progress spent holding for the real foot to clear a step up
    float stepUpHoldTime;  // the same, s
    bool stepUpCleared;    // the hold is over: the foot cleared, or it waited stepUpMaxHold
    // This step's stepUpRiseBy and stepUpForwardFrom: a careless step's don't lift first.
    float riseBy, forwardFrom;
    float stepPeakLift;    // before the step-length cap
    // How far from liftoff the swing crosses a step's face, m; infinity when it doesn't (level or a slope).
    float faceAt = float.PositiveInfinity;

    // Read off the rig at Awake.
    Transform ghostHips;
    ConfigurableJoint ankle;   // on the physical foot, connected to the shin: the ankle is its anchor
    float side;            // −1 left leg, +1 right
    float hipHalfWidth;    // this hip's distance from the centreline

    public bool IsStepping => isStepping;
    // Swinging up a step's face onto higher ground.
    public bool IsSteppingUp => isStepping && CrossesFace && landingPoint.y > liftoffPoint.y;
    // Where this foot's ankle should be this tick: the IK target, this transform.
    public Vector3 AnkleTarget => transform.position;
    public bool IsCrossing => isStepping && isCrossing;
    // The way the swing travels, from liftoff toward the landing: flat, unit; zero while planted.
    public Vector3 SwingDirection => isStepping ? (landingPoint - liftoffPoint).Flat().normalized : Vector3.zero;
    // Where the swing will plant the foot (a ground point); where it is, once planted.
    public Vector3 LandingPoint => landingPoint;
    // 0 → 1 through the swing; 1 while planted.
    public float SwingProgress => isStepping ? Mathf.Clamp01(stepProgress - stepUpHeld) : 1f;
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
    bool CrossesFace => !float.IsPositiveInfinity(faceAt);
    float FootGroundOffset => playerRig.FootGroundOffset;
    // A planted leg feels the ghost hips' balance shift; a swinging leg must not, or it lands off by it, so its
    // target carries the shift back. Not while a step up is held short of its face: the shift is added past
    // that hold, and up stairs, where the shift is largest, it threw the target up to 8 cm ahead at liftoff
    // and the toe into the riser.
    Vector3 SwingShift => isStepping && (!IsSteppingUp || stepUpCleared) ? playerRig.GhostHipOffset : Vector3.zero;
    // The physical sole's height under the ankle, as the ground point is the target's. Not the foot's lowest
    // corner: swinging up a step with the body over it, the toe hangs well below the ankle, and judged by the
    // toe a foot high enough to go on never was.
    float RealSoleHeight => RealAnkle.y - FootGroundOffset;
    // Where the physical ankle is: AnkleTarget is where it should be.
    public Vector3 RealAnkle => ankle.transform.TransformPoint(ankle.anchor);

    void Awake()
    {
        if (!playerRig || !balanceSensor || !playerController || !otherFoot || !ghostThigh || !kneeHint)
        {
            Debug.LogError("FootPlacement: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        ghostHips = playerRig.GhostHips;
        if (foot) ankle = foot.GetComponent<ConfigurableJoint>();

        float hipOffset = Vector3.Dot(ghostThigh.position - ghostHips.position, ghostHips.right);
        side = Mathf.Sign(hipOffset);
        hipHalfWidth = Mathf.Abs(hipOffset);

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

        transform.SetPositionAndRotation(
            groundPoint + Vector3.up * FootGroundOffset + SwingShift,
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
        bool stalled = false;

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
        else if (IsStalled(lastLand) || IsClimbing())
        {
            if (!IsRearFoot(mySole, otherSole)) return;
            capture = otherSole + playerController.MoveInputWorld.normalized * stallStepReach;
            isCatchStep = true;
            stalled = true;
        }
        else if (!ShouldTurnStep() && !ShouldGatherStep(com, mySole, otherSole)) return;

        float swingDuration = SwingDuration(BodyVelocity.magnitude);
        Vector3 aim = AimLanding(capture, groundPoint, swingDuration, crossing);
        Vector3 landing = OntoGround(aim, BodyVelocity * swingDuration, out Vector3 normal);
        // A turn step may land almost where it lifted off: it moves the facing.
        if (isCatchStep && (landing - groundPoint).Flat().magnitude < MinStepLength) return;

        if (logSteps)
        {
            float speed = BodyVelocity.magnitude;
            string kind = crossing ? "crosses" : stalled ? "stall-steps" : isCatchStep ? "steps" : "turn-steps";
            Debug.Log($"[{Time.time:F2}] {name} {kind}  turn {TurnAngle:F1}°  capture {capture:F3}  " +
                      $"error {comError:F3} (|{comError.magnitude:F3}|)  from {groundPoint:F3} to {landing:F3}  " +
                      $"COM vel {balanceSensor.CenterOfMassVelocity:F2}  stride cap {StrideLength(speed):F2} " +
                      $"swing {SwingDuration(speed):F2}s", this);
        }

        // A stall step goes where it was sent: re-aiming at the capture point would pull it back off the step.
        BeginStep(aim, landing, normal, !stalled, crossing);
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

    // Pushed but not moving, e.g. shins against a step's face or a foot hooked on its edge, or held back
    // short of a high step to climb it. The centre of mass stays between the soles, so no catch step comes,
    // and the body would stand there for good.
    bool IsStalled(float lastLand) =>
        playerController.MoveInputWorld.magnitude >= stallInput &&
        (BodyVelocity.magnitude < stallSpeed || playerController.IsHeldAtStepUp) &&
        Time.time - lastLand >= stallTime;

    // Pushing on with the feet split across a rise, this foot below the other, as up stairs: it steps on now,
    // as the stall step does, instead of once the body has passed the other foot. On stairs that foot lands
    // a whole stair ahead, so waiting for the body made every stair a stop.
    bool IsClimbing() =>
        playerController.MoveInputWorld.magnitude >= stallInput &&
        Ground.FaceBetween(groundPoint, otherFoot.GroundPoint, groundPoint.y, out _, out float change) && change > 0f;

    // Further back along the input (ties to the left).
    bool IsRearFoot(Vector3 mySole, Vector3 otherSole)
    {
        float behind = Vector3.Dot(otherSole - mySole, playerController.MoveInputWorld);
        return behind > 0f || (behind == 0f && side < 0f);
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

    // Where a foot lifting off from `liftoff` should aim to land, `timeToLand` seconds from now: OntoGround
    // puts it on the ground and off edges. Everything is judged where the body will be at touchdown, carried
    // on by its velocity. Reach is judged at the ground's height where that's higher than liftoff: at liftoff
    // height, a step up a stair was clamped back under the hip, as if the leg had to reach the floor. Down a
    // drop the body sinks with the step, so it's judged as on the level; at the lower ground's height, the
    // steps off it came up too short to catch the body.
    Vector3 AimLanding(Vector3 capture, Vector3 liftoff, float timeToLand, bool crossing)
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
        landing.y = Mathf.Max(liftoff.y, Ground.HeightAt(landing, liftoff.y));

        landing = crossing ? ClampCrossover(landing, drift) : ClampStanceWidth(landing, drift);
        landing = ClampStride(landing);
        return ClampToReach(landing, drift);
    }

    // An aimed landing on the ground under it, off any step's edge, and at most one level up from the other
    // foot. Up stairs a foot goes one stair past the other: one past a second rise lands just past the first
    // instead, ankle EdgeMargin onto it; taking two at once was a lunge the leg couldn't power. Checked last,
    // so the shift off an edge can't carry it up a second stair. Down them it lands where it's aimed: kept to
    // one stair, the steps were too short to catch the body, which pitched over them.
    Vector3 OntoGround(Vector3 landing, Vector3 drift, out Vector3 normal)
    {
        normal = Vector3.up;
        if (Ground.At(landing, landing.y, out Vector3 ground, out Vector3 groundUp))
        {
            landing = ground;
            normal = groundUp;
        }
        landing = KeepFootOffEdges(landing, drift, ref normal);

        Vector3 stance = otherFoot.GroundPoint;
        if (Ground.FaceBetween(stance, landing, stance.y, out float face, out float change) && change > 0f)
        {
            Vector3 pastFirst = stance + (landing - stance).Flat().normalized * (face + EdgeMargin);
            if (Ground.FaceBetween(pastFirst, landing, stance.y + change, out _, out _) &&
                Ground.At(pastFirst, stance.y + change, out ground, out groundUp))
            {
                landing = ground;
                normal = groundUp;
            }
        }
        return landing;
    }

    // A foot landing across a step's face (heel and toe on different levels) is hooked on the edge's corner.
    // It may overhang the lower level, as a heel does off the back of a stair or a toe off its front, but not
    // run into the higher one, and the ankle must be on its own level. Stair treads are shorter than the
    // foot, so it can't always be wholly on one. So the landing moves along the heading: past the face when
    // that's in reach, since the body is heading that way, otherwise short of it, each by EdgeMargin.
    // Preferring the nearer side left a slowed body shuffling at a step's face, landing short every time. A
    // foot across a slope is just on the slope.
    Vector3 KeepFootOffEdges(Vector3 landing, Vector3 drift, ref Vector3 normal)
    {
        Vector3 forward = playerController.Heading * Vector3.forward;
        Vector3 heel = landing - forward * HeelBehind;
        if (!Ground.FaceBetween(heel, landing + forward * ToeAhead, landing.y, out float fromHeel, out float change))
            return landing;

        // Where the face is, as an offset from the ankle. Up a rise, past it is the ankle on top (the heel
        // overhangs) and short of it the toe clear of it; down a drop, past it is the heel clear of it and
        // short of it the ankle on top (the toe overhangs).
        float edge = fromHeel - HeelBehind;
        bool rises = change > 0f;
        float past = edge + (rises ? 0f : HeelBehind) + EdgeMargin;
        float shortOf = edge - (rises ? ToeAhead : 0f) - EdgeMargin;
        if (TryShift(landing, forward * past, drift, ref normal, out Vector3 moved)) return moved;
        if (TryShift(landing, forward * shortOf, drift, ref normal, out moved)) return moved;
        return landing;
    }

    // The landing moved by offset, if there's ground there within the leg's reach. Out of reach, the reach
    // clamp would only pull it back onto the edge.
    bool TryShift(Vector3 landing, Vector3 offset, Vector3 drift, ref Vector3 normal, out Vector3 moved)
    {
        moved = landing;
        if (!Ground.At(landing + offset, landing.y, out Vector3 ground, out Vector3 groundUp)) return false;
        if ((ClampToReach(ground, drift) - ground).Flat().sqrMagnitude > 0.02f * 0.02f) return false;
        moved = ground;
        normal = groundUp;
        return true;
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
        faceAt = Ground.FaceBetween(liftoffPoint, landingPoint, liftoffPoint.y, out float face, out float faceRise)
            ? face
            : float.PositiveInfinity;
        float rise = landingPoint.y - liftoffPoint.y;
        bool stepUp = CrossesFace && rise > 0f;
        float swingDuration = SwingDuration(BodyVelocity.magnitude) + (stepUp ? stepUpTimePerMetre * rise : 0f);
        stepProgress += Time.fixedDeltaTime / swingDuration;
        // Until the real foot is clear of the first face a step up crosses (up stairs, the stair the other foot
        // is on, below the landing), its toe goes no closer than EdgeMargin short of it, backing off if it
        // lifted off closer, and once there at full height the swing waits. Only there: waiting while still
        // short of the face, the foot fell behind a body that walked on.
        bool clear = ankle == null || RealSoleHeight >= liftoffPoint.y + faceRise + StepUpClearMargin;
        if (stepUp && (clear || stepUpHoldTime >= stepUpMaxHold)) stepUpCleared = true;
        float t = Mathf.Clamp01(stepProgress - stepUpHeld);
        float travel = stepUp ? SmoothStep(Mathf.InverseLerp(forwardFrom, 1f, t)) : SmoothStep(t);
        if (stepUp && !stepUpCleared)
        {
            float shortOf = (faceAt - ToeAhead - EdgeMargin) / (landingPoint - liftoffPoint).Flat().magnitude;
            if (travel > shortOf)
            {
                travel = shortOf;
                if (t >= riseBy)
                {
                    stepUpHeld += Time.fixedDeltaTime / swingDuration;
                    stepUpHoldTime += Time.fixedDeltaTime;
                }
            }
        }

        if (retargetsInFlight)
        {
            float timeToLand = (1f - t) * swingDuration;
            Vector3 aim = AimLanding(balanceSensor.CapturePoint.Flat(), liftoffPoint, timeToLand, isCrossing);
            float weight = Mathf.Clamp01(RetargetStrength * Time.fixedDeltaTime * (1f - travel));
            aimPoint = Vector3.Lerp(aimPoint, aim, weight);
            landingPoint = OntoGround(aimPoint, BodyVelocity * timeToLand, out groundNormal);
        }

        // The foot turns to the heading over the swing and holds that once planted.
        facing = Quaternion.Slerp(liftoffFacing, playerController.Heading, travel);
        Vector3 point = Vector3.LerpUnclamped(liftoffPoint, landingPoint, travel);
        point.y = Mathf.Lerp(liftoffPoint.y, landingPoint.y, HeightProgress(t, travel));
        // Not below 0: sin(π) is a hair under it in floats, and a step up held at t = 1 (stepUpRiseBy 1) took
        // the square root of that, which is NaN, and so was the whole body from then on.
        float arc = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
        point.y += Mathf.Sqrt(arc) * PeakLift() + arc * Mathf.Min(playerRig.HipSag, MaxSagLift);
        groundPoint = KeepSwingInReach(point, arc);

        if (stepProgress - stepUpHeld >= 1f)
        {
            isStepping = false;
            groundPoint = landingPoint = TouchdownPoint();
            LastLandTime = Time.time;
        }
    }

    // Where the foot actually came down: the landing, unless the real sole ended well below it because its
    // toe caught the edge of a step up. Planting on top then would drive the leg toward a pose its foot isn't
    // in, so the foot is planted on the ground under where its ankle really is, and the next step tries again.
    // Judged by the sole under the ankle, as the step-up hold is: a toe hanging below a landed foot isn't a miss.
    Vector3 TouchdownPoint()
    {
        if (ankle == null || landingPoint.y - RealSoleHeight < TouchdownTolerance) return landingPoint;
        if (!Ground.At(RealAnkle, RealSoleHeight, out Vector3 planted, out Vector3 normal)) return landingPoint;

        groundNormal = normal;
        if (logSteps) Debug.Log($"[{Time.time:F2}] {name} missed its landing at {landingPoint:F2}; planted at {planted:F2}", this);
        return planted;
    }

    // Pulls a swing point toward the hip until the leg reaches it with a bent knee, so a foot lifting off
    // far behind doesn't drag forward on a locked leg. Eased with the arc, so liftoff and touchdown are
    // untouched.
    Vector3 KeepSwingInReach(Vector3 point, float arc)
    {
        Vector3 offset = Vector3.up * FootGroundOffset + SwingShift;
        Vector3 hip = ghostThigh.position;
        Vector3 ankle = point + offset;

        float reach = playerRig.LegLength * Mathf.Lerp(MaxLegExtension, SwingLegExtension, Mathf.Sqrt(arc));
        Vector3 fromHip = ankle - hip;
        if (fromHip.sqrMagnitude <= reach * reach) return point;
        return hip + fromHip.normalized * reach - offset;
    }

    // How far the foot has gone from liftoff height to landing height, 0 → 1. Across a face a step up rises
    // first and a step down descends last, so the foot is at the higher height whenever it's over the edge;
    // on level ground or a slope the height follows the horizontal travel.
    float HeightProgress(float t, float travel)
    {
        if (!CrossesFace) return travel;
        return landingPoint.y > liftoffPoint.y
            ? SmoothStep(Mathf.Clamp01(t / riseBy))
            : SmoothStep(Mathf.InverseLerp(stepDownDescendFrom, 1f, t));
    }

    static float SmoothStep(float x) => x * x * (3f - 2f * x);

    // This step's clearance above its height profile, capped by its horizontal length so a shuffle doesn't
    // march, but never under stepUpClearance on a step up. Re-read each tick as the landing moves.
    float PeakLift()
    {
        Vector3 travel = landingPoint - liftoffPoint;
        float lift = Mathf.Min(stepPeakLift, travel.Flat().magnitude * MaxLiftPerStepLength);
        return CrossesFace && travel.y > 0f ? Mathf.Max(lift, stepUpClearance) : lift;
    }

    // A fixed step from wherever the foot is now, for StepTester.
    public void BeginStep(Vector3 landing, Vector3 normal) => BeginStep(landing, landing, normal, false, false);

    void BeginStep(Vector3 aim, Vector3 landing, Vector3 normal, bool retarget, bool crossing)
    {
        isCrossing = crossing;
        liftoffPoint = groundPoint;
        liftoffFacing = facing;
        aimPoint = aim;
        landingPoint = landing;
        groundNormal = normal;
        stepProgress = 0f;
        stepUpHeld = 0f;
        stepUpHoldTime = 0f;
        stepUpCleared = false;
        isStepping = true;
        retargetsInFlight = retarget;
        stepPeakLift = Mathf.Max(0f, stepHeight + Random.Range(-StepHeightVariance, StepHeightVariance));
        bool careless = Random.value < playerController.DrunkAmount * drunkCarelessStepChance;
        riseBy = careless ? 1f : stepUpRiseBy;
        forwardFrom = careless ? 0f : stepUpForwardFrom;
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
