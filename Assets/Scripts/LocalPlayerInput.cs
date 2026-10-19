using UnityEngine;
using UnityEngine.InputSystem;

// The local player's devices, turned into PlayerController input every frame: Move and Look from the
// project-wide input actions (keyboard, mouse or gamepad), and a reset. Locks the cursor while enabled.
// Only the local player's character has one; a test bench disables it to drive the character itself.
[RequireComponent(typeof(PlayerController))]
public class LocalPlayerInput : MonoBehaviour
{
    const string MoveActionName = "Player/Move";
    const string LookActionName = "Player/Look";

    // Bindings are editable in the Inspector.
    [SerializeField] InputAction resetAction = new("Reset", InputActionType.Button, "<Keyboard>/r");

    [Header("Look")]
    // Degrees per pixel of pointer travel.
    [SerializeField] float mouseSensitivity = 0.1f;
    // Degrees per second at full stick deflection. A sober character survived a 90°/s turn and fell at 180.
    [SerializeField] float stickYawSpeed = 90f;
    [SerializeField] float stickPitchSpeed = 120f;
    [SerializeField] float maxPitch = 80f;

    PlayerController playerController;
    InputAction moveAction, lookAction;
    float yaw, pitch;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        moveAction = InputSystem.actions.FindAction(MoveActionName, throwIfNotFound: true);
        lookAction = InputSystem.actions.FindAction(LookActionName, throwIfNotFound: true);
    }

    // Picks up the character's current heading, so re-enabling after a bench doesn't snap it back.
    void OnEnable()
    {
        yaw = playerController.CurrentInput.HeadingYaw;
        playerController.CharacterReset += OnCharacterReset;
        resetAction.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        playerController.CharacterReset -= OnCharacterReset;
        resetAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnCharacterReset() => yaw = playerController.CurrentInput.HeadingYaw;

    void Update()
    {
        // A pointer reports a displacement; a stick reports a rate.
        Vector2 look = lookAction.ReadValue<Vector2>();
        look = lookAction.activeControl?.device is Pointer
            ? look * mouseSensitivity
            : new Vector2(look.x * stickYawSpeed, look.y * stickPitchSpeed) * Time.deltaTime;
        yaw = Mathf.Repeat(yaw + look.x, 360f);
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);

        // Between physics ticks, so the next tick measures the reset pose.
        if (resetAction.WasPressedThisFrame()) playerController.ResetCharacter();

        playerController.SetInput(new CharacterInput
        {
            Move = moveAction.ReadValue<Vector2>(),
            HeadingYaw = yaw,
            LookPitch = pitch,
        });
    }
}
