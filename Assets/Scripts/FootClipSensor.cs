using UnityEngine;

// Whether a swinging foot clipped something on its way, as a toe does a stair's nose. On the physical foot,
// whose collisions it hears. Clip is the current swing's, or the last one's once the foot is planted:
//   Scuff: the foot hit a surface turned back against its swing, and went on.
//   Snag:  it hit one and was held there, not going forward though its target was ahead: a trip.
// Only a new contact on the way is a clip. Up stairs the foot is longer than the tread, so a toe is often
// against the next riser as it lifts off, and comes down against the one past its landing; it clips only once
// it has been clear of a riser, and not as it arrives. Brushing the ground it swings over isn't a clip either:
// that surface faces up, not back. It only reports; nothing reacts to it yet.
[DefaultExecutionOrder(15)]   // after FootPlacement (0) has moved this tick's target
public class FootClipSensor : MonoBehaviour
{
    // A contact is turned back against the swing when its normal points back along it at least this much:
    // a step's face, or the corner of its nose, not a tread's top.
    const float MinBackwardNormal = 0.35f;
    // How long after a clipping contact, s, the foot being held still counts as caught by it.
    const float SnagWindow = 0.15f;
    // A foot whose ankle is within this of its landing along the swing, m, is arriving: half the toe's reach,
    // well short of a nose the toe would catch on the way (the ankle is a toe's length and more behind it).
    const float ArrivingWithin = 0.1f;

    [SerializeField] FootPlacement footPlacement;
    // Held: its target at least snagGap, m, ahead along the swing, but the foot going forward no faster than
    // snagSpeed, m/s. The real foot always trails its target, often by 10 cm or more mid-swing, so the gap
    // alone isn't a snag; a foot that has stopped is. Caught once held for snagTime, s, all told.
    [SerializeField] float snagGap = 0.05f;
    [SerializeField] float snagSpeed = 0.1f;
    [SerializeField] float snagTime = 0.1f;

    Rigidbody body;
    bool wasStepping;
    // A contact turned back against the swing was heard in the physics step since the last tick.
    bool touchingBack;
    // The swing has been clear of such contacts, so the next one is a clip.
    bool armed;
    float lastClipTime;
    float heldTime;

    public FootClip Clip { get; private set; }
    // The real ankle along the swing: how far behind its target, m, and how fast it's going, m/s. 0 planted.
    public float SwingGap { get; private set; }
    public float SwingSpeed { get; private set; }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (!footPlacement || !body)
        {
            Debug.LogError("FootClipSensor: footPlacement or the foot's Rigidbody missing — disabling.", this);
            enabled = false;
        }
    }

    void FixedUpdate()
    {
        bool touched = touchingBack;
        touchingBack = false;
        bool stepping = footPlacement.IsStepping;
        bool started = stepping && !wasStepping;
        wasStepping = stepping;
        if (!stepping)
        {
            SwingGap = SwingSpeed = 0f;
            return;
        }

        Vector3 direction = footPlacement.SwingDirection;
        Vector3 ankle = footPlacement.RealAnkle;
        SwingGap = Vector3.Dot(footPlacement.AnkleTarget - ankle, direction);
        SwingSpeed = Vector3.Dot(body.GetPointVelocity(ankle), direction);

        // What was heard before the swing had a direction says nothing about it.
        if (started)
        {
            Clip = FootClip.None;
            armed = false;
            lastClipTime = float.NegativeInfinity;
            heldTime = 0f;
            return;
        }

        bool arriving = Vector3.Dot(footPlacement.LandingPoint - ankle, direction) <= ArrivingWithin;
        if (!touched) armed = true;
        else if (armed && !arriving)
        {
            lastClipTime = Time.time;
            if (Clip == FootClip.None) Clip = FootClip.Scuff;
        }

        bool held = Time.time - lastClipTime <= SnagWindow && SwingGap >= snagGap && SwingSpeed <= snagSpeed;
        if (held) heldTime += Time.fixedDeltaTime;
        if (Clip == FootClip.Scuff && heldTime >= snagTime) Clip = FootClip.Snag;
    }

    void OnCollisionEnter(Collision collision) => Hear(collision);
    void OnCollisionStay(Collision collision) => Hear(collision);

    void Hear(Collision collision)
    {
        if (!enabled || (Ground.Mask & (1 << collision.collider.gameObject.layer)) == 0) return;

        // A contact's normal points out of what was hit, toward this foot.
        Vector3 back = -footPlacement.SwingDirection;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Vector3.Dot(collision.GetContact(i).normal, back) < MinBackwardNormal) continue;
            touchingBack = true;
            return;
        }
    }
}

public enum FootClip { None, Scuff, Snag }
