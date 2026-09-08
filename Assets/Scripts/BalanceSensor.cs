using Unity.VisualScripting;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class BalanceSensor : MonoBehaviour
{
    Rigidbody[] childRigidbodies;
    float totalMass;
    public Vector3 COM; // Center of Mass (COM)
    public Vector3 COMVelocity; // Velocity of the COM
    public Vector3 COMPrediction; // Predicted COM position in the future


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    void Awake()
    {
        InitializeBody();
    }

    void FixedUpdate()
    {
        CalculateCOM();
        PredictFutureCOM();

    }

    private void InitializeBody()
    {
        childRigidbodies = GetComponentsInChildren<Rigidbody>();
        totalMass = 0;
        foreach (Rigidbody rb in childRigidbodies)
        {
            totalMass += rb.mass;
        }
        Debug.Log("Total Mass of Player: " + totalMass);
    }

    // Calculate the Center of Mass (COM) of the Player
    void CalculateCOM()
    {
        Vector3 weightedPosition = Vector3.zero;
        Vector3 weightedVelocity = Vector3.zero;

        foreach (Rigidbody rb in childRigidbodies)
        {
            weightedPosition += rb.mass * rb.worldCenterOfMass;
            weightedVelocity += rb.mass * rb.linearVelocity;
            Debug.Log("Is Sleeping: " + rb.IsSleeping());
        }

        COM = weightedPosition / totalMass;
        COMVelocity = weightedVelocity / totalMass;

    }

    void PredictFutureCOM()
    {
        float COMHeight = Mathf.Max(COM.y - 0,.3f); // TODO: 0 is the ground height for now, but will need to be changed to a dynamic number when we have uneven ground
        float omega = Mathf.Sqrt(9.81f / COMHeight);

        Vector3 groundProjectedCOM = new(COM.x, 0, COM.z);
        Vector3 groundProjectedCOMVelocity = new(COMVelocity.x, 0, COMVelocity.z);

        COMPrediction = groundProjectedCOM + (groundProjectedCOMVelocity / omega);

    }


    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        // COM Gizmo
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(COM, 0.1f);

        Vector3 groundProjection = new(COM.x, 0, COM.z);
        Gizmos.DrawLine(COM, groundProjection);
        Gizmos.DrawWireSphere(groundProjection, 0.025f);

        // Predicted COM Gizmo
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(groundProjection, COMPrediction);
        Gizmos.DrawSphere(COMPrediction, 0.025f);

    }


}
