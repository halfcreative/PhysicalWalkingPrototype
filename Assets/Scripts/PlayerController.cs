using UnityEngine;
using UnityEngine.InputSystem;

// The player's move, after DrunkWalkHome's PlayerController: the drunk sway and the player's input, two
// 2D vectors in the heading's frame (x right, y forward), sum into one force on the body's momentum.
// The mouse owns the heading. The pelvis turns toward it through BalanceController, and the feet plant
// facing it, so the hips squaring to a stance never steers the feet.
//
// It also owns the fall: DrunkWalkHome's ring and breach. Momentum outside the ring, the pelvis tipping
// and the centre of mass dropping each charge a breach that commits the fall when full, drawn by
// BalanceHUD. Once
// fallen it stops driving the body: no move force, no sway, and LegDrive, BalanceController and
// FootPlacement go limp. R puts the character back where it spawned.
[DefaultExecutionOrder(-60)]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] Rigidbody pelvis;

    [Header("Drunk")]
    [SerializeField, Range(0f, 1f)] float drunkAmount = 0f;
    [SerializeField] float swayMagnitude = 1.5f;
    [SerializeField] float swayRotationRate = 90f;
    [SerializeField] float swayResponseSpeed = 3f;
    // The sway's terminal speed across the heading, m/s per unit, apart from the input's maxSideSpeed: a
    // drunk lurches sideways harder than they can choose to sidestep. Before crossovers (FootPlacement),
    // drunk 1.0, 10 seeds: 0.6 kept 6/10 up, 0.9 1/10, 1.2 none. With them, 20 seeds: 0.6 13/20, 0.9
    // 12/20, 1.2 11/20 (2026-10-16).
    [SerializeField] float swaySideSpeed = 0.9f;

    [Header("Walk")]
    [SerializeField] float walkDrive = 1f;
    // Terminal speed at a resultant of 1, if nothing else held the body back. The legs do: 2.5 walks at
    // ~1.4 m/s, and 3 is no faster, only pitches the body forward over its feet (2026-10-15).
    [SerializeField] float maxSpeed = 2.5f;
    // The same across the heading. The legs can only sidestep there, and a sidestep can't keep pace with
    // a walk: with sideways at maxSpeed, all ten falls in a drunk 0.5 benchmark came travelling sideways
    // at 0.55–0.9 m/s, the pelvis rolling further over with every step (2026-10-15).
    [SerializeField] float maxSideSpeed = 0.6f;
    // Seconds for momentum to build toward, or bleed away from, what the force asks for.
    [SerializeField] float momentumTime = 0.8f;
    [SerializeField] float steerResponse = 12f;

    [Header("Look")]
    // Degrees of heading per pixel of mouse travel. Not scaled by time: a mouse delta is a displacement.
    [SerializeField] float mouseSensitivity = 0.1f;
    // The scene's CinemachineCamera follows this, as in DrunkWalkHome. Not a child of the body: nothing
    // under Player moves with it, so it's placed every frame over the centre of mass, turned to the
    // heading and the camera's pitch.
    [SerializeField] Transform cameraTarget;
    // Height of the target above the centre of mass, m. 0.55 over a standing 0.86 puts it at
    // DrunkWalkHome's 1.4.
    [SerializeField] float cameraTargetHeight = 0.55f;
    // Pitch is the camera's alone: the body only ever turns to the heading's yaw.
    [SerializeField] float verticalClamp = 80f;

    [Header("Instability")]
    // DrunkWalkHome's fall: a ring the body's momentum must stay inside, and a breach that charges while
    // it's outside. The ring's radius at full drunkenness, in units of momentum (1 = a sober full walk):
    // it eases from an unreachable sober ceiling down to this as drunkAmount rises, so a sober character
    // can't fall by breach whatever this is set to. Lower for a messier drunk.
    [SerializeField] float fallLimit = 0.7f;
    // Breach that commits the fall, in violation-seconds: 0.5 is half a unit of overshoot held for a
    // second, or a full unit for half a second. Raise for a drunk who can ride out a lurch.
    [SerializeField] float breachThreshold = 0.5f;
    // Breach recovered per second back inside the ring. Far below the charge rate on purpose (as in
    // DrunkWalkHome): a cumulative instability meter across a bad stretch, not a per-lurch reset.
    [SerializeField] float breachDecay = 0.05f;
    // What momentum is measured against, m/s, the same in every direction as in DrunkWalkHome. Not
    // maxSpeed: that's a force scale the legs never reach (a held W walks 1.24–1.33 m/s). Set so the
    // fastest sober walk stays inside the sober ring (1.1): replayed over four sober runs, 1.5 with 0.5 s
    // smoothing peaked at 0.99, where 1.3 peaked at 1.17 and charged the breach of a sober walk. A
    // sober full walk reads ~0.85. Per-axis top speeds were tried: against a 0.4 m/s sidestep, the walk's
    // ordinary side-to-side weave read as 0.4–0.5 of momentum (2026-10-15).
    [SerializeField] float topSpeed = 1.5f;
    // Seconds the body's velocity is smoothed over before it counts as momentum. A walking body's speed
    // pulses within every stride (1.0–1.5 m/s at a 1.3 m/s walk), and it weaves side to side; unsmoothed,
    // those peaks charge the breach. DrunkWalkHome's capsule had no stride to smooth.
    [SerializeField] float momentumSmoothing = 0.5f;

    [Header("Breach Factors")]
    // More sources that charge the breach beside the momentum ring, the way DrunkWalkHome's height falls
    // do. Each works like the ring: past its limit it charges in proportion to how far past, and all of
    // them sum. One unit past a limit charges as fast as momentum one unit outside the ring: an empty
    // breach fills in breachThreshold seconds. Nothing fells the character but a full breach, so a
    // stumble that comes back in time survives, and every fall is drawn on the HUD.
    //
    // Pelvis tilt from upright, degrees. The body comes back from far more than the drunk benchmarks
    // showed: shoved with the breach out of reach, it recovered from 47° sideways and 65° forward, and
    // every shove that went down passed 110° and ended on the floor (2026-10-15). At 25° the 65° fold
    // filled the breach though the body caught itself. tiltPerUnit degrees past tiltLimit is one unit:
    // a held 60° lean fills an empty breach in 1.5 s, 90° in 0.5 s, lying flat on the floor faster still
    // with the drop added.
    [SerializeField] float tiltLimit = 45f;
    [SerializeField] float tiltPerUnit = 45f;
    // Centre-of-mass height, m above the ground (y = 0, as in BalanceSensor). Standing it's 0.86; upright
    // it never went below 0.69, and falls went under 0.65 on their way down. dropPerUnit metres below
    // dropLimit is one unit: 0.5 m fills an empty breach in 0.5 s, the floor (~0.2 m, 3 units) in 0.17 s.
    [SerializeField] float dropLimit = 0.65f;
    [SerializeField] float dropPerUnit = 0.15f;

    // Headroom above the sober momentum ceiling, as in DrunkWalkHome: momentum only passes walkDrive for
    // a moment, e.g. when drunkenness drops while the body is moving faster than sober intent sustains.
    const float SoberCeilingMargin = 1.1f;

    readonly DrunkSway sway = new DrunkSway();
    Rigidbody[] bodies;
    Vector2 steerInput;
    float pendingMouseX;
    float headingYaw;
    float cameraPitch;
    Vector3 smoothedVelocity;
    float breach;

    // Every body's spawn pose, for ResetCharacter.
    Vector3[] spawnPositions;
    Quaternion[] spawnRotations;
    float spawnYaw;
    FootPlacement[] feet;

    public Quaternion Heading => Quaternion.Euler(0f, headingYaw, 0f);
    public Vector2 Resultant { get; private set; }
    // The resultant in world space: BalanceController leans the body by it, into the sway and into
    // where the player is going.
    public Vector3 ResultantWorld { get; private set; }
    // The sway alone, in world space, for diagnostics.
    public Vector3 SwayWorld { get; private set; }
    // The player's input alone, in world space: how hard and which way they're steering. A tipping body
    // reads it to fight back (BalanceController) and to step into the fall (FootPlacement).
    public Vector3 SteerWorld { get; private set; }
    public bool IsFallen { get; private set; }
    // For BalanceHUD, all in the heading's frame (x right, y forward) and in units of the resultant.
    public Vector2 Sway => sway.Current;
    public Vector2 SteerInput => steerInput;
    // The body's smoothed horizontal travel against topSpeed, in roughly the units of the forces: a sober
    // full walk reads ~0.85.
    public Vector2 Momentum { get; private set; }
    // The live ring radius: the sober ceiling at drunkAmount 0, easing to fallLimit at 1. The sober end
    // is derived from walkDrive, so a sober character can't fall by breach for any fallLimit.
    public float FallLimit => Mathf.Lerp(walkDrive * SoberCeilingMargin, fallLimit, Mathf.Clamp01(drunkAmount));
    public float BreachFraction => breachThreshold > 1e-4f ? Mathf.Clamp01(breach / breachThreshold) : 0f;
    // What each source is charging the breach with this tick, in units past its limit (breach per second).
    public float MomentumCharge { get; private set; }
    public float TiltCharge { get; private set; }
    public float DropCharge { get; private set; }
    // The pelvis's tilt from upright, degrees, and the centre of mass's height: what tilt and drop read.
    public float PelvisTilt => Vector3.Angle(pelvis.rotation * Vector3.up, Vector3.up);
    public float CenterOfMassHeight => balanceSensor.CenterOfMass.y;
    // Raised the moment the character falls. BalanceHUD flashes on it.
    public event System.Action Fell;

    void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
        feet = GetComponentsInChildren<FootPlacement>();
        spawnPositions = new Vector3[bodies.Length];
        spawnRotations = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            spawnPositions[i] = bodies[i].transform.position;
            spawnRotations[i] = bodies[i].transform.rotation;
        }

        ResetSway();
        headingYaw = spawnYaw = transform.eulerAngles.y;
    }

    public float DrunkAmount => drunkAmount;
    public void SetDrunkAmount(float amount) => drunkAmount = Mathf.Clamp01(amount);

    // Restarts the sway from calm. A non-zero seed replays the same sway every time, so a fall can be
    // reproduced and a fix measured against it; 0 picks one at random.
    public void ResetSway(int seed = 0)
    {
        if (seed != 0) Random.InitState(seed);
        sway.Reset();
    }

    // Stand the character back up where it spawned, still, facing the way it spawned, with the sway
    // restarted from calm. Not a get-up: the pose is put back, not reached.
    public void ResetCharacter()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            body.transform.SetPositionAndRotation(spawnPositions[i], spawnRotations[i]);
            body.position = spawnPositions[i];
            body.rotation = spawnRotations[i];
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        foreach (FootPlacement foot in feet)
            foot.PlantAtSpawn();

        headingYaw = spawnYaw;
        pendingMouseX = 0f;
        steerInput = Vector2.zero;
        SteerWorld = Vector3.zero;
        smoothedVelocity = Vector3.zero;
        Momentum = Vector2.zero;
        breach = 0f;
        MomentumCharge = TiltCharge = DropCharge = 0f;
        ResetSway();
        IsFallen = false;
    }

    // Mouse and keys are read here, since a delta or a press read in FixedUpdate can be lost between
    // physics ticks. The mouse is banked and spent in FixedUpdate. The reset happens here, between ticks,
    // so the next tick's BalanceSensor measures the reset pose before anything acts on it.
    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            pendingMouseX += delta.x;
            cameraPitch = Mathf.Clamp(cameraPitch - delta.y * mouseSensitivity, -verticalClamp, verticalClamp);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) ResetCharacter();
    }

    // After the bodies are interpolated, so the target moves as smoothly as the body is drawn.
    void LateUpdate()
    {
        if (cameraTarget == null) return;

        // The centre of mass as drawn: BalanceSensor's is the physics tick's, a step behind the render
        // and stepping at the tick rate, which the camera would judder by.
        Vector3 weighted = Vector3.zero;
        float totalMass = 0f;
        foreach (Rigidbody body in bodies)
        {
            // centerOfMass is offset by the transform's position and rotation, not its scale.
            Transform t = body.transform;
            weighted += body.mass * (t.position + t.rotation * body.centerOfMass);
            totalMass += body.mass;
        }
        cameraTarget.position = weighted / totalMass + Vector3.up * cameraTargetHeight;

        // The banked mouse is spent on the heading at the next tick; the camera turns with it now.
        float yaw = headingYaw + pendingMouseX * mouseSensitivity;
        cameraTarget.rotation = Quaternion.Euler(cameraPitch, yaw, 0f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        headingYaw += pendingMouseX * mouseSensitivity;
        pendingMouseX = 0f;

        UpdateMomentum(dt);

        // Down, the body is left to physics: no force to drag it along the floor.
        if (IsFallen) return;

        Vector2 swayNow = sway.Tick(swayMagnitude * drunkAmount, swayRotationRate, swayResponseSpeed, dt);
        steerInput = Vector2.Lerp(steerInput, ReadMove() * walkDrive, 1f - Mathf.Exp(-steerResponse * dt));
        Resultant = swayNow + steerInput;
        ResultantWorld = Heading * new Vector3(Resultant.x, 0f, Resultant.y);
        SwayWorld = Heading * new Vector3(swayNow.x, 0f, swayNow.y);
        SteerWorld = Heading * new Vector3(steerInput.x, 0f, steerInput.y);

        // The fall trigger reads MOMENTUM, not the force: balance is lost by travelling faster than you
        // can catch, not by asking to. Crossing the ring only charges a breach in proportion to the
        // overshoot, which is what lets the ring sit low. Tilt and drop charge it too; nothing else fells.
        UpdateBreach(dt);
        if (breach >= breachThreshold)
        {
            Fall();
            return;
        }

        // A force on the whole body's momentum, never a velocity: terminal speed is maxSpeed × the resultant
        // (maxSideSpeed across the heading, swaySideSpeed for the sway's share), approached over
        // momentumTime. Every body gets the same acceleration, so it moves the body without bending it;
        // the legs have to keep up by stepping.
        float side = swayNow.x * swaySideSpeed + steerInput.x * maxSideSpeed;
        Vector3 force = Heading * new Vector3(side, 0f, Resultant.y * maxSpeed);
        float invTau = 1f / Mathf.Max(momentumTime, 0.01f);
        Vector3 drive = force * invTau;
        Vector3 drag = -balanceSensor.CenterOfMassVelocity.Flat() * invTau;
        foreach (Rigidbody body in bodies)
            body.AddForce(drive + drag, ForceMode.Acceleration);
    }

    // Momentum in the heading's frame, against topSpeed.
    void UpdateMomentum(float dt)
    {
        Vector3 velocity = balanceSensor.CenterOfMassVelocity.Flat();
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-dt / Mathf.Max(momentumSmoothing, 1e-3f)));
        Vector3 local = Quaternion.Inverse(Heading) * smoothedVelocity / Mathf.Max(topSpeed, 1e-3f);
        Momentum = new Vector2(local.x, local.z);
    }

    // The sources sum, as in DrunkWalkHome: a body travelling too fast AND tipping over is in more
    // trouble than either alone.
    void UpdateBreach(float dt)
    {
        MomentumCharge = Mathf.Max(0f, Momentum.magnitude - FallLimit);
        TiltCharge = Mathf.Max(0f, PelvisTilt - tiltLimit) / Mathf.Max(tiltPerUnit, 1e-3f);
        DropCharge = Mathf.Max(0f, dropLimit - CenterOfMassHeight) / Mathf.Max(dropPerUnit, 1e-3f);

        float charge = MomentumCharge + TiltCharge + DropCharge;
        if (charge > 0f) breach += charge * dt;
        else breach = Mathf.Max(0f, breach - breachDecay * dt);
    }

    void Fall()
    {
        Debug.Log($"[{Time.time:F2}] Fell: breach full. Charging at the end: momentum {MomentumCharge:F2} " +
                  $"(|{Momentum.magnitude:F2}| against ring {FallLimit:F2}), tilt {TiltCharge:F2} " +
                  $"({PelvisTilt:F0}°), drop {DropCharge:F2} (CoM {balanceSensor.CenterOfMass.y:F2} m). " +
                  "R to reset.", this);

        IsFallen = true;
        breach = 0f;
        MomentumCharge = TiltCharge = DropCharge = 0f;
        steerInput = Vector2.zero;
        Resultant = Vector2.zero;
        ResultantWorld = SwayWorld = SteerWorld = Vector3.zero;
        // Restarted from calm, as in DrunkWalkHome, so the character doesn't come back mid-lurch.
        sway.Reset();
        Fell?.Invoke();
    }

    static Vector2 ReadMove()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        // WASD and the arrow keys, either or both.
        bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
        bool left = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
        bool forward = kb.wKey.isPressed || kb.upArrowKey.isPressed;
        bool back = kb.sKey.isPressed || kb.downArrowKey.isPressed;
        Vector2 move = new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), (forward ? 1f : 0f) - (back ? 1f : 0f));
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }
}
