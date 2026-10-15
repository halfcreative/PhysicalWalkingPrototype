using UnityEngine;
using UnityEngine.InputSystem;

// The player's move, after DrunkWalkHome's PlayerController: the drunk sway and the player's input, two
// 2D vectors in the heading's frame (x right, y forward), sum into one force on the body's momentum.
// The mouse owns the heading. The pelvis turns toward it through BalanceController, and the feet plant
// facing it, so the hips squaring to a stance never steers the feet.
[DefaultExecutionOrder(-60)]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BalanceSensor balanceSensor;

    [Header("Drunk")]
    [SerializeField, Range(0f, 1f)] float drunkAmount = 0f;
    [SerializeField] float swayMagnitude = 1.5f;
    [SerializeField] float swayRotationRate = 90f;
    [SerializeField] float swayResponseSpeed = 3f;

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

    readonly DrunkSway sway = new DrunkSway();
    Rigidbody[] bodies;
    Vector2 steerInput;
    float pendingMouseX;
    float headingYaw;

    public Quaternion Heading => Quaternion.Euler(0f, headingYaw, 0f);
    public Vector2 Resultant { get; private set; }
    // The resultant in world space: BalanceController leans the body by it, into the sway and into
    // where the player is going.
    public Vector3 ResultantWorld { get; private set; }
    // The sway alone, in world space, for diagnostics.
    public Vector3 SwayWorld { get; private set; }

    void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
        ResetSway();
        headingYaw = transform.eulerAngles.y;
    }

    public void SetDrunkAmount(float amount) => drunkAmount = Mathf.Clamp01(amount);

    // Restarts the sway from calm. A non-zero seed replays the same sway every time, so a fall can be
    // reproduced and a fix measured against it; 0 picks one at random.
    public void ResetSway(int seed = 0)
    {
        if (seed != 0) Random.InitState(seed);
        sway.Reset();
    }

    // Banked here and spent in FixedUpdate: a delta read in FixedUpdate is lost between physics ticks.
    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null) pendingMouseX += mouse.delta.ReadValue().x;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        headingYaw += pendingMouseX * mouseSensitivity;
        pendingMouseX = 0f;

        Vector2 swayNow = sway.Tick(swayMagnitude * drunkAmount, swayRotationRate, swayResponseSpeed, dt);
        steerInput = Vector2.Lerp(steerInput, ReadMove() * walkDrive, 1f - Mathf.Exp(-steerResponse * dt));
        Resultant = swayNow + steerInput;
        ResultantWorld = Heading * new Vector3(Resultant.x, 0f, Resultant.y);
        SwayWorld = Heading * new Vector3(swayNow.x, 0f, swayNow.y);

        // A force on the whole body's momentum, never a velocity: terminal speed is maxSpeed × the resultant
        // (maxSideSpeed across the heading), approached over momentumTime. Every body gets the same
        // acceleration, so it moves the body without bending it; the legs have to keep up by stepping.
        Vector3 force = Heading * new Vector3(Resultant.x * maxSideSpeed, 0f, Resultant.y * maxSpeed);
        float invTau = 1f / Mathf.Max(momentumTime, 0.01f);
        Vector3 drive = force * invTau;
        Vector3 drag = -balanceSensor.CenterOfMassVelocity.Flat() * invTau;
        foreach (Rigidbody body in bodies)
            body.AddForce(drive + drag, ForceMode.Acceleration);
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
