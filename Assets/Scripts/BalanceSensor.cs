using UnityEngine;

// The whole body's centre of mass and capture point: the spot on the ground where a planted foot would
// bring the body to rest. Everything balance-related reads these.
[DefaultExecutionOrder(-100)]
public class BalanceSensor : MonoBehaviour
{
    // Floor on the pendulum height, so a body folded onto the ground can't send the capture point to
    // infinity.
    const float MinPendulumHeight = 0.3f;

    float totalMass;

    // Every Rigidbody in the character.
    public Rigidbody[] Bodies { get; private set; }
    public Vector3 CenterOfMass { get; private set; }
    public Vector3 CenterOfMassVelocity { get; private set; }
    // On the ground (y = 0): the centre of mass plus its horizontal velocity over √(g / h).
    public Vector3 CapturePoint { get; private set; }

    void Awake()
    {
        Bodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody body in Bodies)
            totalMass += body.mass;

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

        // TODO: assumes flat ground at y = 0.
        float height = Mathf.Max(CenterOfMass.y, MinPendulumHeight);
        float omega = Mathf.Sqrt(Physics.gravity.magnitude / height);
        CapturePoint = CenterOfMass.Flat() + CenterOfMassVelocity.Flat() / omega;
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

        Vector3 groundCom = CenterOfMass.Flat();

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(CenterOfMass, 0.1f);
        Gizmos.DrawLine(CenterOfMass, groundCom);
        Gizmos.DrawWireSphere(groundCom, 0.025f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(groundCom, CapturePoint);
        Gizmos.DrawSphere(CapturePoint, 0.025f);
    }
}
