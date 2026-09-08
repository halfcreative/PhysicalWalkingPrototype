using UnityEngine;
using UnityEngine.InputSystem;

// Debug tool. Applies a calibrated velocity impulse to every body at once, so the
// COM velocity changes by exactly pushSpeed and recovery tests are repeatable.
// Arrow keys: up/down = forward/back, left/right = sideways.
public class PushTester : MonoBehaviour
{
    [SerializeField] float pushSpeed = 0.3f;   // m/s added to the COM

    Rigidbody[] bodies;
    Vector3 pendingPush;

    void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
    }

    // Input is polled in Update; wasPressedThisFrame is unreliable in FixedUpdate.
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.wasPressedThisFrame) pendingPush = transform.forward;
        if (kb.downArrowKey.wasPressedThisFrame) pendingPush = -transform.forward;
        if (kb.rightArrowKey.wasPressedThisFrame) pendingPush = transform.right;
        if (kb.leftArrowKey.wasPressedThisFrame) pendingPush = -transform.right;
    }

    void FixedUpdate()
    {
        if (pendingPush == Vector3.zero) return;

        Vector3 delta = pendingPush * pushSpeed;

        foreach (Rigidbody rb in bodies)
        {
            rb.AddForce(delta, ForceMode.VelocityChange);
        }

        Debug.Log($"Push {pushSpeed:F2} m/s  dir {pendingPush}");
        pendingPush = Vector3.zero;
    }
}
