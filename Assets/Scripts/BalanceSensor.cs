using UnityEngine;

// The whole body's centre of mass, and its capture point: the spot on the ground where, if a foot
// were planted, the body would come to rest over it. Everything balance-related reads these.
[DefaultExecutionOrder(-100)]
public class BalanceSensor : MonoBehaviour
{
    const float Gravity = 9.81f;
    // Floor on the pendulum height, so a body folding onto the ground can't send the capture point to
    // infinity.
    const float MinPendulumHeight = 0.3f;

    Rigidbody[] bodies;
    float totalMass;

    public Vector3 CenterOfMass { get; private set; }
    public Vector3 CenterOfMassVelocity { get; private set; }
    // On the ground (y = 0): the centre of mass projected down, plus its horizontal velocity divided by
    // the pendulum's natural frequency √(g / h).
    public Vector3 CapturePoint { get; private set; }

    void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody body in bodies)
            totalMass += body.mass;

        Debug.Log($"BalanceSensor: {bodies.Length} bodies, {totalMass:F1} kg", this);
    }

    void FixedUpdate()
    {
        Vector3 weightedPosition = Vector3.zero;
        Vector3 weightedVelocity = Vector3.zero;

        foreach (Rigidbody body in bodies)
        {
            weightedPosition += body.mass * body.worldCenterOfMass;
            weightedVelocity += body.mass * body.linearVelocity;
        }

        CenterOfMass = weightedPosition / totalMass;
        CenterOfMassVelocity = weightedVelocity / totalMass;

        // TODO: the ground is y = 0 for now. Uneven ground needs the height of the support under
        // the body here.
        float height = Mathf.Max(CenterOfMass.y, MinPendulumHeight);
        float omega = Mathf.Sqrt(Gravity / height);

        Vector3 groundCom = new(CenterOfMass.x, 0f, CenterOfMass.z);
        Vector3 groundVelocity = new(CenterOfMassVelocity.x, 0f, CenterOfMassVelocity.z);
        CapturePoint = groundCom + groundVelocity / omega;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 groundCom = new(CenterOfMass.x, 0f, CenterOfMass.z);

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(CenterOfMass, 0.1f);
        Gizmos.DrawLine(CenterOfMass, groundCom);
        Gizmos.DrawWireSphere(groundCom, 0.025f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(groundCom, CapturePoint);
        Gizmos.DrawSphere(CapturePoint, 0.025f);
    }
}
