using UnityEngine;

// The neck, and the only writer of its target. After DrunkWalkHome's head gaze, but driven through the
// neck joint rather than posed.
//
// Sober, the head holds the horizon and faces the heading, whatever the chest is doing under it: a walk
// leans the chest 40°, and a head carried by it would stare at the floor.
//
// Drunk, two things change, both scaled by drunkAmount:
//   - The target slides from that world-held gaze toward the head simply riding the chest
//     (drunkHeadLean), so it lolls wherever the torso leans.
//   - The neck goes soft and underdamped. The head trails a lurch, swings past it and settles back. That
//     overshoot is most of what reads as drunk. DrunkWalkHome built it as a simulated spring; here the
//     joint drive is the spring, and the torso's motion under it does the rest.
[DefaultExecutionOrder(10)]   // after TorsoLean (−55) has set this tick's chest target
public class HeadDrive : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    // On the head, connected to the torso.
    [SerializeField] ConfigurableJoint neck;

    [Header("Gaze")]
    // How much a fully drunk head gives up holding the horizon and rides the chest instead, 0–1.
    [SerializeField, Range(0f, 1f)] float drunkHeadLean = 0.6f;

    [Header("Neck")]
    // Drive spring, N·m/rad, sober and fully drunk. Tuned on a 0.22 m, 4 kg head (~0.07 kg·m² about the
    // neck, gravity tipping it at ~4.3 N·m/rad); a 0.36 m one of the same mass is ~0.18 kg·m² and
    // ~7 N·m/rad: at drunk 45 it lolled 24° (p95 59°), at 100 the stiff neck passed its swing into the
    // torso and cost 4/20, and 70 matches the small head. At 60 a sober walk bobbed the head ±10–15°. At 15 / 0.35 a
    // drunk head rolled 50–65° on average, swung past ±100° and yawed ±70°, a head on a wet noodle
    // rather than a loll, and its 4 kg flailing cost 2/20 on the drunk benchmark (2026-10-16).
    [SerializeField] float soberSpring = 120f;
    [SerializeField] float drunkSpring = 70f;
    // Damping ratio of the drive, sober and fully drunk. Below 1 overshoots.
    [SerializeField] float soberDamping = 1f;
    [SerializeField] float drunkDamping = 0.5f;

    [Header("Fallen")]
    // Down, the neck drops to this fraction of its stiffness.
    [SerializeField, Range(0f, 1f)] float fallenStiffness = 0.1f;

    Transform torso;
    Quaternion neckStart;
    // The head's rest rotation relative to the torso, and in the heading's frame at spawn.
    Quaternion restOnTorso, restInHeading;
    float maxForce;
    // The head's inertia about the neck, kg·m², for turning a damping ratio into a damper. Read off the
    // body so it follows the head's size and mass.
    float headInertia;

    void Awake()
    {
        if (!playerController || !neck || !neck.connectedBody)
        {
            Debug.LogError("HeadDrive: unassigned references — disabling.", this);
            enabled = false;
            return;
        }

        torso = neck.connectedBody.transform;
        neckStart = Quaternion.Inverse(torso.rotation) * neck.transform.rotation;
        restOnTorso = neckStart;
        Quaternion spawnHeading = Quaternion.Euler(0f, playerController.transform.eulerAngles.y, 0f);
        restInHeading = Quaternion.Inverse(spawnHeading) * neck.transform.rotation;
        maxForce = neck.slerpDrive.maximumForce;

        // About its own centre, plus the parallel-axis term out to the neck. Taken about the larger
        // principal axis, the head being near enough a sphere for one number.
        Rigidbody head = neck.GetComponent<Rigidbody>();
        Vector3 tensor = head.inertiaTensor;
        float lever = (head.worldCenterOfMass - neck.transform.TransformPoint(neck.anchor)).magnitude;
        headInertia = Mathf.Max(tensor.x, tensor.z) + head.mass * lever * lever;
    }

    void FixedUpdate()
    {
        float drunk = Mathf.Clamp01(playerController.DrunkAmount);

        float spring = Mathf.Lerp(soberSpring, drunkSpring, drunk);
        float ratio = Mathf.Lerp(soberDamping, drunkDamping, drunk);
        if (playerController.IsFallen) spring *= fallenStiffness;
        neck.slerpDrive = new JointDrive
        {
            positionSpring = spring,
            positionDamper = 2f * ratio * Mathf.Sqrt(spring * headInertia),
            maximumForce = maxForce,
        };

        Quaternion aim = playerController.Heading * restInHeading;
        Quaternion carried = torso.rotation * restOnTorso;
        Quaternion target = Quaternion.Slerp(aim, carried, drunkHeadLean * drunk);
        neck.SetTargetWorldRotation(target, neckStart, torso);
    }
}
