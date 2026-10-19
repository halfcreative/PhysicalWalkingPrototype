using UnityEngine;
using UnityEngine.Serialization;

// The player's move and the fall. The drunk sway and the move input are 2D vectors in the heading's frame
// (x right, y forward); their sum, NetForce, drives the body's momentum. Input arrives through SetInput,
// from LocalPlayerInput, a test bench or the network; this never reads a device.
//
// The fall meter charges while momentum is outside MomentumLimit, the pelvis tips too far, or the centre
// of mass drops too low. When full, the character falls and every drive goes limp.
[DefaultExecutionOrder(-60)]
public class PlayerController : MonoBehaviour
{
    // Headroom above the sober momentum limit, so a sober character can't fill the fall meter.
    const float SoberLimitMargin = 1.1f;

    [Header("References")]
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] Rigidbody pelvis;

    [Header("Drunk")]
    [SerializeField, Range(0f, 1f)] float drunkAmount = 0f;
    [SerializeField] float swayMagnitude = 1.5f;
    [SerializeField] float swayRotationRate = 90f;
    [SerializeField] float swayResponseSpeed = 3f;
    // Terminal sideways speed per unit of sway, m/s. Higher than maxSideSpeed: a drunk lurches sideways
    // harder than they can choose to sidestep.
    [SerializeField] float swaySideSpeed = 0.9f;

    [Header("Walk")]
    [SerializeField, FormerlySerializedAs("walkDrive")] float moveInputScale = 1f;
    // Terminal speed at a net force of 1, m/s. The legs cap the real walk at ~1.4 m/s.
    [SerializeField] float maxSpeed = 2.5f;
    // The same across the heading. Kept low: a sidestep can't keep pace with a walk.
    [SerializeField] float maxSideSpeed = 0.6f;
    // Seconds for momentum to build toward, or bleed away from, what the net force asks for.
    [SerializeField, FormerlySerializedAs("momentumTime")] float accelerationTime = 0.8f;
    [SerializeField, FormerlySerializedAs("steerResponse")] float moveInputResponse = 12f;

    [Header("Fall Meter")]
    // Momentum limit at full drunkenness. Eases down to this from the sober limit as drunkAmount rises.
    [SerializeField, FormerlySerializedAs("fallLimit")] float drunkMomentumLimit = 0.7f;
    // Meter level that triggers the fall, in charge-seconds (one unit past a limit for this long).
    [SerializeField, FormerlySerializedAs("breachThreshold")] float fallMeterCapacity = 0.5f;
    // Drain per second while nothing charges. Slow on purpose: it accumulates over a bad stretch.
    [SerializeField, FormerlySerializedAs("breachDecay")] float fallMeterDrainRate = 0.05f;
    // Speed at which momentum reads 1, m/s. A sober full walk reads ~0.85.
    [SerializeField, FormerlySerializedAs("topSpeed")] float fullMomentumSpeed = 1.5f;
    // Seconds the velocity is smoothed over, so the pulse within each stride doesn't charge the meter.
    [SerializeField] float momentumSmoothing = 0.5f;

    [Header("Fall Meter Sources")]
    // Pelvis tilt from upright, degrees: past tiltLimit, every tiltPerUnit degrees charges one unit.
    [SerializeField] float tiltLimit = 45f;
    [SerializeField] float tiltPerUnit = 45f;
    // Centre-of-mass height, m (standing ≈ 0.86): below dropLimit, every dropPerUnit metres charges one unit.
    [SerializeField] float dropLimit = 0.65f;
    [SerializeField] float dropPerUnit = 0.15f;

    [Header("Fallen")]
    // Fraction of their stiffness every joint drive keeps once fallen.
    [SerializeField, Range(0f, 1f)] float fallenStiffness = 0.1f;

    readonly DrunkSway sway = new DrunkSway();
    float headingYaw;
    Vector3 smoothedVelocity;
    float fallMeter;

    // Spawn pose of every body, for ResetCharacter.
    Vector3[] spawnPositions;
    Quaternion[] spawnRotations;
    float spawnYaw;
    FootPlacement[] feet;

    public event System.Action Fell;
    // Raised after ResetCharacter, so input sources can re-sync their heading to the spawn heading.
    public event System.Action CharacterReset;
    public bool IsFallen { get; private set; }
    // The latest input given; the next physics tick acts on it.
    public CharacterInput CurrentInput { get; private set; }
    public float FallenStiffness => fallenStiffness;
    public float DrunkAmount => drunkAmount;

    public Quaternion Heading => Quaternion.Euler(0f, headingYaw, 0f);
    public Quaternion SpawnHeading => Quaternion.Euler(0f, spawnYaw, 0f);

    // Heading frame (x right, y forward), in force units.
    public Vector2 Sway => sway.Current;
    public Vector2 MoveInput { get; private set; }
    public Vector2 NetForce => Sway + MoveInput;
    // Smoothed horizontal velocity over fullMomentumSpeed.
    public Vector2 Momentum { get; private set; }

    public Vector3 SwayWorld => ToWorld(Sway);
    public Vector3 MoveInputWorld => ToWorld(MoveInput);
    public Vector3 NetForceWorld => ToWorld(NetForce);

    // The live momentum limit: the sober limit at drunkAmount 0, easing to drunkMomentumLimit at 1.
    public float MomentumLimit =>
        Mathf.Lerp(moveInputScale * SoberLimitMargin, drunkMomentumLimit, Mathf.Clamp01(drunkAmount));
    public float FallMeterFraction =>
        fallMeterCapacity > 1e-4f ? Mathf.Clamp01(fallMeter / fallMeterCapacity) : 0f;
    // What each source charges the meter with this tick, units per second.
    public float MomentumCharge { get; private set; }
    public float TiltCharge { get; private set; }
    public float DropCharge { get; private set; }
    public float PelvisTilt => Vector3.Angle(pelvis.rotation * Vector3.up, Vector3.up);
    public float CenterOfMassHeight => balanceSensor.CenterOfMass.y;

    void Awake()
    {
        feet = GetComponentsInChildren<FootPlacement>();
        Rigidbody[] bodies = balanceSensor.Bodies;
        spawnPositions = new Vector3[bodies.Length];
        spawnRotations = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            spawnPositions[i] = bodies[i].transform.position;
            spawnRotations[i] = bodies[i].transform.rotation;
        }

        ResetSway();
        headingYaw = spawnYaw = transform.eulerAngles.y;
        CurrentInput = NeutralInput();
    }

    // Held until replaced, so a source only needs to call this when its input changes.
    public void SetInput(CharacterInput input) => CurrentInput = input;

    // Standing still, facing the spawn heading.
    public CharacterInput NeutralInput() => new() { HeadingYaw = spawnYaw };

    public void SetDrunkAmount(float amount) => drunkAmount = Mathf.Clamp01(amount);

    // Restarts the sway from calm. A non-zero seed replays the same sway; 0 picks one at random.
    public void ResetSway(int seed = 0)
    {
        if (seed != 0) Random.InitState(seed);
        sway.Reset();
    }

    // Puts the character back at its spawn pose, still, with the sway restarted and the input neutral.
    // Call between physics ticks, so the next tick measures the reset pose before anything acts on it.
    public void ResetCharacter()
    {
        Rigidbody[] bodies = balanceSensor.Bodies;
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
        CurrentInput = NeutralInput();
        MoveInput = Vector2.zero;
        smoothedVelocity = Vector3.zero;
        Momentum = Vector2.zero;
        ClearFallMeter();
        ResetSway();
        IsFallen = false;
        CharacterReset?.Invoke();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // The heading follows input even when fallen, so the player can look around while down.
        headingYaw = Mathf.Repeat(CurrentInput.HeadingYaw, 360f);

        UpdateMomentum(dt);

        if (IsFallen) return;

        sway.Tick(swayMagnitude * drunkAmount, swayRotationRate, swayResponseSpeed, dt);
        Vector2 move = Vector2.ClampMagnitude(CurrentInput.Move, 1f) * moveInputScale;
        MoveInput = Vector2.Lerp(MoveInput, move, Smoothing.Factor(moveInputResponse, dt));

        // The fall is judged on momentum, not force: balance is lost by travelling faster than you can
        // catch, not by asking to.
        UpdateFallMeter(dt);
        if (fallMeter >= fallMeterCapacity)
        {
            Fall();
            return;
        }

        // An acceleration on every body toward a terminal velocity, so the body moves without bending and
        // the legs have to step to keep up.
        float sideSpeed = Sway.x * swaySideSpeed + MoveInput.x * maxSideSpeed;
        Vector3 targetVelocity = Heading * new Vector3(sideSpeed, 0f, NetForce.y * maxSpeed);
        float inverseTime = 1f / Mathf.Max(accelerationTime, 0.01f);
        Vector3 acceleration = (targetVelocity - balanceSensor.CenterOfMassVelocity.Flat()) * inverseTime;
        foreach (Rigidbody body in balanceSensor.Bodies)
            body.AddForce(acceleration, ForceMode.Acceleration);
    }

    void UpdateMomentum(float dt)
    {
        Vector3 velocity = balanceSensor.CenterOfMassVelocity.Flat();
        float rate = 1f / Mathf.Max(momentumSmoothing, 1e-3f);
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, Smoothing.Factor(rate, dt));
        Vector3 local = Quaternion.Inverse(Heading) * smoothedVelocity / Mathf.Max(fullMomentumSpeed, 1e-3f);
        Momentum = new Vector2(local.x, local.z);
    }

    // The sources sum: travelling too fast while tipping is worse than either alone.
    void UpdateFallMeter(float dt)
    {
        MomentumCharge = Mathf.Max(0f, Momentum.magnitude - MomentumLimit);
        TiltCharge = Mathf.Max(0f, PelvisTilt - tiltLimit) / Mathf.Max(tiltPerUnit, 1e-3f);
        DropCharge = Mathf.Max(0f, dropLimit - CenterOfMassHeight) / Mathf.Max(dropPerUnit, 1e-3f);

        float charge = MomentumCharge + TiltCharge + DropCharge;
        if (charge > 0f) fallMeter += charge * dt;
        else fallMeter = Mathf.Max(0f, fallMeter - fallMeterDrainRate * dt);
    }

    void Fall()
    {
        Debug.Log($"[{Time.time:F2}] Fell. Charges: momentum {MomentumCharge:F2} " +
                  $"(|{Momentum.magnitude:F2}| against {MomentumLimit:F2}), tilt {TiltCharge:F2} " +
                  $"({PelvisTilt:F0}°), drop {DropCharge:F2} (CoM {CenterOfMassHeight:F2} m).", this);

        IsFallen = true;
        ClearFallMeter();
        MoveInput = Vector2.zero;
        sway.Reset();
        Fell?.Invoke();
    }

    void ClearFallMeter()
    {
        fallMeter = 0f;
        MomentumCharge = TiltCharge = DropCharge = 0f;
    }

    Vector3 ToWorld(Vector2 headingFrame) => Heading * new Vector3(headingFrame.x, 0f, headingFrame.y);
}
