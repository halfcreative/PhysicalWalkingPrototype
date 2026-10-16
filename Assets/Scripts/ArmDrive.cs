using UnityEngine;

// One per arm, and the only writer of its shoulder and elbow targets. After DrunkWalkHome's TorsoPose
// arms, but driven through joints rather than posed: the arm hangs, swings against the stride, and throws
// itself around when the body is shoved. The flail is pushed by the sway, so it reads as being thrown
// rather than as a pose.
//
// The arm hangs in the heading's frame, not the torso's: a chest leaning 40° over a walk doesn't carry
// its arms 40° forward, gravity keeps them down. The shoulder limits keep them on the body.
//
// The upper arm is authored turned ±90° about its own length, so its joint's X axis (the only one with
// asymmetric limits) points fore and aft. The X limit is then abduction: far outward, barely inward.
[DefaultExecutionOrder(10)]   // after FootPlacement (0) has moved the feet the swing reads
public class ArmDrive : MonoBehaviour
{
    // Fore/aft foot spread of a full stride, m: the swing's phase signal reaches ±1 at this spread.
    const float StrideNormalization = 0.3f;
    // Below this walking speed the stride swing fades out, m/s, so a still stance holds still.
    const float SwingFullSpeed = 0.65f;

    [Header("References")]
    [SerializeField] PlayerController playerController;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] ConfigurableJoint shoulder;   // on the upper arm, connected to the torso
    [SerializeField] ConfigurableJoint elbow;      // on the forearm, connected to the upper arm
    [SerializeField] FootPlacement leftFoot, rightFoot;
    // −1 for the left arm, +1 for the right.
    [SerializeField] float side = -1f;

    [Header("Rest")]
    // Degrees the arm hangs out from the side, and the elbow's rest bend.
    [SerializeField] float restSplay = 8f;
    [SerializeField] float restElbowBend = 15f;

    [Header("Stride Swing")]
    // Degrees fore and aft at a full stride, anti-phase to this side's leg (DrunkWalkHome's armSwingScale
    // was 50 on a posed arm; a driven arm overshoots a little on its own).
    [SerializeField] float swingScale = 35f;
    [SerializeField] float swingResponse = 8f;

    [Header("Counterbalance")]
    // Degrees the arm on the side the sway is shoving toward flies up, per unit of sway, and how far the
    // other arm rises as a counterweight. As in DrunkWalkHome: both rise, unequally, so the pair doesn't
    // read as a see-saw.
    [SerializeField] float raiseScale = 60f;
    [SerializeField] float counterRaiseScale = 22f;
    // Degrees the arms reach fore/aft per unit of sway along the heading.
    [SerializeField] float reachScale = 16f;
    // How quickly the arm answers the force, 1/s. Low reads as a slow drunken wave arriving after the
    // lurch; high as a startled snatch.
    [SerializeField] float counterResponse = 6f;
    // The most the arm is asked to rise, degrees from hanging.
    [SerializeField] float maxRaise = 100f;
    // Extra elbow bend per degree of raise: a thrown arm bends.
    [SerializeField] float elbowBendPerRaise = 0.3f;

    [Header("Fallen")]
    // Down, the arm drops to this fraction of its stiffness: it goes with the fall.
    [SerializeField, Range(0f, 1f)] float fallenStiffness = 0.1f;

    Transform torso, upperArm, forearm;
    Quaternion shoulderStart, elbowStart;
    // Each segment's rest rotation in the heading's frame at spawn.
    Quaternion upperRest, forearmRest;
    JointDrive shoulderDrive, elbowDrive;
    bool limp;

    float swing;
    Vector2 force;

    void Awake()
    {
        if (!playerController || !balanceSensor || !shoulder || !elbow || !leftFoot || !rightFoot ||
            !shoulder.connectedBody || !elbow.connectedBody)
        {
            Debug.LogError("ArmDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        torso = shoulder.connectedBody.transform;
        upperArm = shoulder.transform;
        forearm = elbow.transform;

        shoulderStart = Quaternion.Inverse(torso.rotation) * upperArm.rotation;
        elbowStart = Quaternion.Inverse(upperArm.rotation) * forearm.rotation;

        Quaternion spawnHeading = Quaternion.Euler(0f, playerController.transform.eulerAngles.y, 0f);
        upperRest = Quaternion.Inverse(spawnHeading) * upperArm.rotation;
        forearmRest = Quaternion.Inverse(spawnHeading) * forearm.rotation;

        shoulderDrive = shoulder.slerpDrive;
        elbowDrive = elbow.slerpDrive;
    }

    void FixedUpdate()
    {
        bool fallen = playerController.IsFallen;
        if (fallen != limp)
        {
            limp = fallen;
            float scale = fallen ? fallenStiffness : 1f;
            shoulder.slerpDrive = Scaled(shoulderDrive, scale);
            elbow.slerpDrive = Scaled(elbowDrive, scale);
        }

        float dt = Time.fixedDeltaTime;
        Quaternion heading = playerController.Heading;
        Vector3 right = heading * Vector3.right;
        Vector3 forward = heading * Vector3.forward;

        // The stride: the feet's fore/aft spread, faded out below a walk. Right foot ahead swings this side
        // forward if it's the left arm.
        float spread = Vector3.Dot(rightFoot.GroundPoint - leftFoot.GroundPoint, forward) / StrideNormalization;
        float gate = Mathf.Clamp01(balanceSensor.CenterOfMassVelocity.Flat().magnitude / SwingFullSpeed);
        float swingTarget = fallen ? 0f : -side * Mathf.Clamp(spread, -1f, 1f) * swingScale * gate;
        swing = Mathf.Lerp(swing, swingTarget, 1f - Mathf.Exp(-swingResponse * dt));

        // The sway only, not the resultant DrunkWalkHome read: the input is where the player wants to go,
        // not something shoving the body. On the resultant, a held W reached both arms 16° forward for the
        // whole walk and a strafe raised one to 74° (2026-10-16).
        force = Vector2.Lerp(force, fallen ? Vector2.zero : playerController.Sway,
                             1f - Mathf.Exp(-counterResponse * dt));

        // Which role this arm plays: positive loading when the force shoves toward this arm's side.
        float loading = side * force.x;
        float raise = Mathf.Min(maxRaise, raiseScale * Mathf.Max(0f, loading)
                                        + counterRaiseScale * Mathf.Max(0f, -loading));
        float abduction = restSplay + raise;
        float pitch = swing + reachScale * force.y;

        // Hanging, then out to this side about the heading's forward, then fore/aft about its right.
        // Rotating "down" about +forward swings it toward +right, and about +right swings it backward.
        Quaternion upperTarget = Quaternion.AngleAxis(-pitch, right)
                               * Quaternion.AngleAxis(side * abduction, forward)
                               * heading * upperRest;
        shoulder.SetTargetWorldRotation(upperTarget, shoulderStart, torso);

        // The elbow bends forward about the arm's own right, carried by wherever the upper arm actually
        // is, so the forearm trails the upper arm on its softer drive rather than chasing a target the
        // upper arm hasn't reached.
        float bend = restElbowBend + elbowBendPerRaise * raise;
        Quaternion carried = upperArm.rotation * Quaternion.Inverse(heading * upperRest);
        Quaternion forearmTarget = carried * Quaternion.AngleAxis(-bend, right) * heading * forearmRest;
        elbow.SetTargetWorldRotation(forearmTarget, elbowStart, upperArm);
    }

    static JointDrive Scaled(JointDrive drive, float scale)
    {
        drive.positionSpring *= scale;
        drive.positionDamper *= Mathf.Sqrt(scale);
        return drive;
    }
}
