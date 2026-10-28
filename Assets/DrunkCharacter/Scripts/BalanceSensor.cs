using UnityEngine;

// The whole body's centre of mass, where it's supported, and its capture point: the spot on the support
// plane where a planted foot would bring the body to rest. Everything balance-related reads these.
//
// Heights are measured from the support, not from y = 0: the planted soles, whose ground points come from
// FootPlacement's landing raycasts and so follow the terrain.
[DefaultExecutionOrder(-100)]
public class BalanceSensor : MonoBehaviour
{
    // Floor on the pendulum height, so a body folded onto its support can't send the capture point to
    // infinity.
    const float MinPendulumHeight = 0.3f;
    // The ground-ahead probe: samples every ProbeSpacing from under the centre of mass along its travel,
    // out to ProbeFar or ProbeTime seconds of travel if further. Reaching further at speed leaves time to
    // stop before a step's face. It starts once the body travels faster than ProbeMinSpeed, and keeps its
    // direction while a face is in view unless the body moves on toward it, so a body slowing or rocking
    // back from a step doesn't lose sight of it.
    const float ProbeFar = 0.7f;
    const float ProbeTime = 2f;
    const float ProbeSpacing = 0.15f;
    const int MaxProbeSamples = 32;
    const float ProbeMinSpeed = 0.2f;

    [SerializeField] FootPlacement leftFoot;
    [SerializeField] FootPlacement rightFoot;

    float totalMass;

    // Every Rigidbody in the character.
    public Rigidbody[] Bodies { get; private set; }
    public Vector3 CenterOfMass { get; private set; }
    public Vector3 CenterOfMassVelocity { get; private set; }
    // The planted soles: their midpoint while both are down, otherwise the one carrying the body. Read off
    // last tick's feet, since FootPlacement runs later.
    public Vector3 SupportCentre { get; private set; }
    // How far the centre of mass is above SupportCentre: the pendulum's length. Negative once the body has
    // dropped below where its feet are planted, as off a ledge.
    public float HeightAboveSupport { get; private set; }
    // On the support plane: the centre of mass plus its horizontal velocity over √(g / h).
    public Vector3 CapturePoint { get; private set; }
    // How far the tallest step face just ahead along the body's travel climbs above the support, m: a step
    // up coming. 0 when level, falling away, or standing still; a slope reads as only a few centimetres.
    public float RiseAhead { get; private set; }
    // The same for the deepest face dropping below the support: a step down coming.
    public float DropAhead { get; private set; }
    // The direction probed (flat, unit; zero when not probing), and how far along it from the centre of mass
    // that face is, m: where the ground gets halfway up it. Infinity with no face.
    public Vector3 RiseDirection { get; private set; }
    public float RiseDistance { get; private set; } = float.PositiveInfinity;

    void Awake()
    {
        Bodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody body in Bodies)
            totalMass += body.mass;

        if (!leftFoot || !rightFoot)
            Debug.LogError("BalanceSensor: feet unassigned — support height falls back to y = 0.", this);

        Debug.Log($"BalanceSensor: {Bodies.Length} bodies, {totalMass:F1} kg", this);
    }

    void FixedUpdate()
    {
        Vector3 weightedPosition = Vector3.zero;
        Vector3 weightedVelocity = Vector3.zero;

        foreach (Rigidbody body in Bodies)
        {
            weightedPosition += body.mass * body.worldCenterOfMass;
            weightedVelocity += body.mass * body.linearVelocity;
        }

        CenterOfMass = weightedPosition / totalMass;
        CenterOfMassVelocity = weightedVelocity / totalMass;

        SupportCentre = MeasureSupport();
        HeightAboveSupport = CenterOfMass.y - SupportCentre.y;

        float omega = Mathf.Sqrt(Physics.gravity.magnitude / Mathf.Max(HeightAboveSupport, MinPendulumHeight));
        Vector3 capture = CenterOfMass + CenterOfMassVelocity.Flat() / omega;
        CapturePoint = new Vector3(capture.x, SupportCentre.y, capture.z);

        MeasureStepsAhead();
    }

    void MeasureStepsAhead()
    {
        Vector3 velocity = CenterOfMassVelocity.Flat();
        bool riseInView = RiseAhead > 0f;
        if (velocity.magnitude >= ProbeMinSpeed && (!riseInView || Vector3.Dot(velocity, RiseDirection) > 0f))
            RiseDirection = velocity.normalized;
        else if (!riseInView) RiseDirection = Vector3.zero;
        RiseAhead = 0f;
        DropAhead = 0f;
        RiseDistance = float.PositiveInfinity;
        if (RiseDirection == Vector3.zero) return;

        // Faces are height changes between neighbouring samples, from the ground under the centre of mass on:
        // a slope changes a little between every pair, a step's face its whole height between one pair. Each
        // counts only as far as it ends above (or below) the support, so a face the body is already part way
        // up (a foot on top) counts for what's left of it.
        Vector3 origin = CenterOfMass.Flat();
        float near = SupportCentre.y;
        float far = Mathf.Max(ProbeFar, velocity.magnitude * ProbeTime);
        int samples = Mathf.Min(MaxProbeSamples, Mathf.FloorToInt(far / ProbeSpacing));
        float previous = Ground.HeightAt(origin, near), faceBase = 0f, faceTop = 0f, faceStart = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float height = Ground.HeightAt(origin + RiseDirection * (i * ProbeSpacing), near);
            float rise = Mathf.Min(height - previous, height - near);
            if (rise > RiseAhead)
            {
                RiseAhead = rise;
                faceBase = previous;
                faceTop = height;
                faceStart = (i - 1) * ProbeSpacing;
            }
            DropAhead = Mathf.Max(DropAhead, Mathf.Min(previous - height, near - height));
            previous = height;
        }
        if (RiseAhead <= 0f) return;

        RiseDistance = faceStart + Ground.Crossing(origin + RiseDirection * faceStart, RiseDirection, ProbeSpacing,
                                                   near, (faceBase + faceTop) * 0.5f, rising: true);
    }

    Vector3 MeasureSupport()
    {
        if (!leftFoot || !rightFoot) return CenterOfMass.Flat();
        if (leftFoot.IsStepping) return rightFoot.SupportPoint;
        if (rightFoot.IsStepping) return leftFoot.SupportPoint;
        return (leftFoot.SupportPoint + rightFoot.SupportPoint) * 0.5f;
    }

    // The centre of mass as rendered, from the interpolated transforms. CenterOfMass is the physics
    // tick's, which steps at the tick rate and would make a camera judder.
    public Vector3 InterpolatedCenterOfMass()
    {
        Vector3 weighted = Vector3.zero;
        foreach (Rigidbody body in Bodies)
        {
            Transform t = body.transform;
            weighted += body.mass * (t.position + t.rotation * body.centerOfMass);
        }
        return weighted / totalMass;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 onSupport = new(CenterOfMass.x, SupportCentre.y, CenterOfMass.z);

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(CenterOfMass, 0.1f);
        Gizmos.DrawLine(CenterOfMass, onSupport);
        Gizmos.DrawWireSphere(onSupport, 0.025f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(onSupport, CapturePoint);
        Gizmos.DrawSphere(CapturePoint, 0.025f);
    }
}
