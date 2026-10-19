using UnityEngine;
using UnityEngine.Serialization;

// Leans the body along its momentum. The chest's tilt drives the torso joint over the pelvis. The hips'
// tilt and shift are published for the balance system: PlayerRig and BalanceController hold the pelvis at
// HipTilt, and BalanceController moves the balance target by HipShift.
[DefaultExecutionOrder(-55)]   // after PlayerController (−60) has measured momentum
public class TorsoLean : MonoBehaviour
{
    // Just inside the torso joint's limits (±25° pitch, ±20° roll), so the drive isn't pressed on a stop.
    const float MaxPitch = 24f;
    const float MaxRoll = 19f;

    [SerializeField] PlayerController playerController;
    // The torso's joint, connected to the pelvis.
    [SerializeField] ConfigurableJoint torsoJoint;
    // Degrees the chest tilts per unit of momentum.
    [SerializeField, FormerlySerializedAs("leanTiltScale")] float chestTiltScale = 25f;
    // How quickly the lean follows momentum, 1/s.
    [SerializeField] float leanResponse = 8f;
    // Multiplies the sideways lean, since sideways momentum runs much smaller than forward.
    [SerializeField] float sideLeanBoost = 2.5f;
    // The sideways lean fades out as the pelvis rolls this many degrees (start, end) past its target roll,
    // so a body losing it sideways stops leaning into the fall.
    [SerializeField] Vector2 sideLeanFadeRoll = new(5f, 15f);

    [Header("Hips")]
    // Degrees the pelvis tilts toward the travel per unit of lean.
    [SerializeField] float hipTiltScale = 12f;
    [SerializeField] float maxHipTilt = 15f;
    // Metres the balance target shifts along the lean per unit.
    [SerializeField] float hipShiftScale = 0.12f;
    [SerializeField] float maxHipShift = 0.1f;

    Transform pelvis;
    Quaternion startLocalRotation;
    // Smoothed momentum, sideways boosted, in the heading's frame (x right, y forward).
    Vector2 lean;

    // The world rotation the pelvis is tilted by, on top of upright.
    public Quaternion HipTilt { get; private set; } = Quaternion.identity;
    // Horizontal shift of the balance target, world space.
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
        startLocalRotation = torsoJoint.LocalRotation();
    }

    void FixedUpdate()
    {
        Quaternion heading = playerController.Heading;
        Vector3 right = heading * Vector3.right;
        Vector3 forward = heading * Vector3.forward;

        // Fallen, the body straightens back out.
        Vector2 momentum = playerController.Momentum;
        Vector2 target = playerController.IsFallen ? Vector2.zero
                       : new Vector2(momentum.x * sideLeanBoost * SideLeanFade(right), momentum.y);
        lean = Vector2.Lerp(lean, target, Smoothing.Factor(leanResponse, Time.fixedDeltaTime));

        Vector2 hipTilt = Vector2.ClampMagnitude(lean * hipTiltScale, maxHipTilt);
        HipTilt = Quaternion.AngleAxis(hipTilt.y, right) * Quaternion.AngleAxis(-hipTilt.x, forward);
        HipShift = Vector3.ClampMagnitude(heading * new Vector3(lean.x, 0f, lean.y) * hipShiftScale, maxHipShift);

        float pitch = Mathf.Clamp(lean.y * chestTiltScale, -MaxPitch, MaxPitch);
        float roll = Mathf.Clamp(lean.x * chestTiltScale, -MaxRoll, MaxRoll);
        Quaternion chestTilt = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(-roll, forward);

        // On top of the pelvis, so the chest is carried by the hips' tilt and any stumble.
        torsoJoint.SetTargetWorldRotation(chestTilt * pelvis.rotation * startLocalRotation, startLocalRotation, pelvis);
    }

    // 1 while the pelvis's roll is within sideLeanFadeRoll.x of last tick's HipTilt roll, 0 past .y.
    float SideLeanFade(Vector3 right)
    {
        float roll = RollAbout(pelvis.up, right);
        float targetRoll = RollAbout(HipTilt * Vector3.up, right);
        return 1f - Mathf.InverseLerp(sideLeanFadeRoll.x, sideLeanFadeRoll.y, Mathf.Abs(roll - targetRoll));
    }

    static float RollAbout(Vector3 up, Vector3 right) =>
        Mathf.Atan2(Vector3.Dot(up, right), Vector3.Dot(up, Vector3.up)) * Mathf.Rad2Deg;
}
