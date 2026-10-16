using UnityEngine;

// The body's lean, after DrunkWalkHome's TorsoPose: the chest tilts along the body's momentum, and the
// hips tilt and shift with it, so the character reads as leaning into wherever it's going. There the
// lean was cosmetic, posed onto bones; here it's physical. The chest's is the target of the torso's
// joint drive over the pelvis, so it moves real mass (24 of 62 kg). The hips' is published for the
// balance system to hold: PlayerRig tilts the ghost hips by HipTilt and BalanceController's upright
// torque aims for it, and BalanceController adds HipShift to where the body holds its balance.
//
// As in DrunkWalkHome, the lean follows momentum, not the force: a body leans because it's travelling,
// not the instant a key goes down.
[DefaultExecutionOrder(-55)]   // after PlayerController (−60) has measured this tick's momentum
public class TorsoLean : MonoBehaviour
{
    // The torso joint's limits, degrees: ±25 pitch (X), ±20 roll (Z). The lean stays a hair inside them
    // so the drive isn't pressed against a stop.
    const float MaxPitch = 24f;
    const float MaxRoll = 19f;

    [SerializeField] PlayerController playerController;
    // The torso's joint, connected to the pelvis.
    [SerializeField] ConfigurableJoint torsoJoint;
    // Degrees the chest tilts per unit of momentum. A sober full walk reads ~0.85, so 25 leans it ~21°.
    // DrunkWalkHome used 30 against a momentum that reached 1.0 at a full walk; the joint caps this one.
    [SerializeField] float leanTiltScale = 25f;
    // How quickly the lean follows momentum, 1/s (DrunkWalkHome's visualLeanResponse).
    [SerializeField] float leanResponse = 8f;
    // Multiplies the sideways lean. Sideways momentum runs far smaller than forward (the body can't
    // sidestep at walking pace), so at 1 a strafe leaned the chest ~5° (2026-10-16).
    [SerializeField] float sideLeanBoost = 2.5f;

    [Header("Hips")]
    // Degrees the pelvis tilts per unit of lean, top toward the travel (DrunkWalkHome's hipTiltScale).
    [SerializeField] float hipTiltScale = 12f;
    [SerializeField] float maxHipTilt = 15f;
    // Metres the hips shift along the lean per unit, over the feet (DrunkWalkHome's hipShiftScale, 0.3,
    // there moved a pelvis that weighed nothing). Here it moves where the body balances, so it's smaller.
    [SerializeField] float hipShiftScale = 0.12f;
    [SerializeField] float maxHipShift = 0.1f;

    Transform pelvis;
    Quaternion startLocalRotation;
    Vector2 lean;

    // The smoothed momentum, sideways boosted, in the heading's frame (x right, y forward).
    public Vector2 Lean => lean;
    // The world rotation the pelvis is tilted by, on top of upright.
    public Quaternion HipTilt { get; private set; } = Quaternion.identity;
    // Where the hips are shifted, flat, world space.
    public Vector3 HipShift { get; private set; }

    void Awake()
    {
        if (!playerController || !torsoJoint || !torsoJoint.connectedBody)
        {
            Debug.LogError("TorsoLean: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        pelvis = torsoJoint.connectedBody.transform;
        // Captured before physics has moved anything: the rest pose the lean is applied on top of.
        startLocalRotation = Quaternion.Inverse(pelvis.rotation) * torsoJoint.transform.rotation;
    }

    void FixedUpdate()
    {
        // Down, the body straightens back out rather than holding the last lean.
        Vector2 momentum = playerController.Momentum;
        Vector2 target = playerController.IsFallen ? Vector2.zero : new Vector2(momentum.x * sideLeanBoost, momentum.y);
        lean = Vector2.Lerp(lean, target, 1f - Mathf.Exp(-leanResponse * Time.fixedDeltaTime));

        // In the heading's frame, the same as Momentum: forward pitches over, sideways rolls.
        Quaternion heading = playerController.Heading;
        Vector3 right = heading * Vector3.right;
        Vector3 forward = heading * Vector3.forward;

        Vector2 hip = Vector2.ClampMagnitude(lean * hipTiltScale, maxHipTilt);
        HipTilt = Quaternion.AngleAxis(hip.y, right) * Quaternion.AngleAxis(-hip.x, forward);
        HipShift = Vector3.ClampMagnitude(heading * new Vector3(lean.x, 0f, lean.y) * hipShiftScale, maxHipShift);

        float pitch = Mathf.Clamp(lean.y * leanTiltScale, -MaxPitch, MaxPitch);
        float roll = Mathf.Clamp(lean.x * leanTiltScale, -MaxRoll, MaxRoll);
        Quaternion tilt = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(-roll, forward);

        // On top of the pelvis, not against world up: the chest leans on the hips, so the hips' own tilt
        // and a pelvis that tips in a stumble both carry the chest with them.
        torsoJoint.SetTargetWorldRotation(tilt * pelvis.rotation * startLocalRotation, startLocalRotation, pelvis);
    }
}
