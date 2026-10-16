using UnityEngine;

// Debug-grade follow camera: behind the heading, looking at the centre of mass.
public class FollowCamera : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] BalanceSensor balanceSensor;
    [SerializeField] float distance = 4f;
    [SerializeField] float height = 1.5f;

    void LateUpdate()
    {
        Vector3 target = balanceSensor.CenterOfMass;
        transform.position = target + player.Heading * new Vector3(0f, height, -distance);
        transform.LookAt(target);
    }
}
