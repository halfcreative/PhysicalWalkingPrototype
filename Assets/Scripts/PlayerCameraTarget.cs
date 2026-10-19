using UnityEngine;

// Places the CinemachineCamera's follow target over the rendered centre of mass, turned to the latest
// input's heading and pitch. The target isn't parented to the body. Only the local player's character
// has one.
[RequireComponent(typeof(PlayerController), typeof(BalanceSensor))]
public class PlayerCameraTarget : MonoBehaviour
{
    [SerializeField] Transform cameraTarget;
    // Height above the centre of mass, m.
    [SerializeField] float height = 0.55f;

    PlayerController playerController;
    BalanceSensor balanceSensor;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        balanceSensor = GetComponent<BalanceSensor>();
    }

    // After interpolation, so the target moves as smoothly as the body is drawn. The latest input, not the
    // tick's heading, so the camera turns every frame rather than every physics tick.
    void LateUpdate()
    {
        if (cameraTarget == null) return;

        cameraTarget.position = balanceSensor.InterpolatedCenterOfMass() + Vector3.up * height;
        CharacterInput input = playerController.CurrentInput;
        cameraTarget.rotation = Quaternion.Euler(input.LookPitch, input.HeadingYaw, 0f);
    }
}
