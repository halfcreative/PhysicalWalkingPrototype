using System;
using Unity.Netcode;
using UnityEngine;

// Splits a character between host and clients. The host simulates every character from its owner's
// CharacterInput. Clients run none of the simulation: their bodies are kinematic copies placed by
// NetworkPose. The owner gets the local input, the camera target and the HUD; a client owner's HUD shows the
// readout the host sends it. StepTester only works on the host.
//
// The game hooks in here, not into the scene: a camera follows CameraTarget once LocalPlayerSpawned fires
// (FollowLocalPlayer does that for a CinemachineCamera), and IsFallen, FallenChanged and DrunkAmount read
// the same on every side. Drunkenness is set on the host, with PlayerController.SetDrunkAmount.
[RequireComponent(typeof(PlayerController), typeof(LocalPlayerInput), typeof(PlayerCameraTarget))]
public class NetworkPlayer : NetworkBehaviour
{
    // Everything that simulates the character: controllers, drives and the ghost rig. Host only.
    [SerializeField] Behaviour[] simulation;

    readonly NetworkVariable<bool> isFallen = new();
    // The host's PlayerController.DrunkAmount, copied each network tick; it's only sent when it changes.
    readonly NetworkVariable<float> drunkAmount = new();

    PlayerController playerController;
    BalanceController balanceController;
    LocalPlayerInput localInput;
    PlayerCameraTarget cameraTarget;
    BalanceHUD balanceHUD;
    StepTester stepTester;

    // Input sequence numbers, so a late unreliable packet can't replace a newer one on the host.
    uint lastSentInput;
    uint lastAppliedInput;
    // The latest HUD readout from the host, on a client owner.
    BalanceReadout readout;

    // This instance's own character while it's spawned, and when it spawns.
    public static NetworkPlayer Local { get; private set; }
    public static event Action<NetworkPlayer> LocalPlayerSpawned;

    // Whether this character is down, on every side, and when that changes: true on a fall, false on a reset.
    public bool IsFallen => isFallen.Value;
    public event Action<bool> FallenChanged;
    public float DrunkAmount => drunkAmount.Value;
    // Where a third-person camera should follow and look; placed only on the owner.
    public Transform CameraTarget => cameraTarget.Target;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        balanceController = GetComponent<BalanceController>();
        localInput = GetComponent<LocalPlayerInput>();
        cameraTarget = GetComponent<PlayerCameraTarget>();
        balanceHUD = GetComponent<BalanceHUD>();
        stepTester = GetComponent<StepTester>();
    }

    public override void OnNetworkSpawn()
    {
        // NGO moves the root to its spawn point after Awake, so the character re-anchors there: the host
        // resets the bodies and feet, and every side takes the spawn heading before input is enabled.
        if (IsServer) playerController.ResetCharacter();
        else playerController.SetInput(playerController.NeutralInput());

        foreach (Behaviour component in simulation)
            component.enabled = IsServer;

        isFallen.OnValueChanged += OnFallenChanged;
        if (IsServer)
        {
            playerController.Fell += OnFell;
            playerController.CharacterReset += OnCharacterReset;
            NetworkManager.NetworkTickSystem.Tick += CopyDrunkAmount;
            if (!IsOwnedByServer) NetworkManager.NetworkTickSystem.Tick += SendReadout;
        }
        else
        {
            foreach (Rigidbody body in GetComponent<BalanceSensor>().Bodies)
            {
                body.isKinematic = true;
                // NetworkPose places the bodies; Rigidbody interpolation on top would fight it.
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        localInput.enabled = IsOwner;
        cameraTarget.enabled = IsOwner;
        if (stepTester != null) stepTester.enabled = IsOwner && IsServer;
        if (balanceHUD != null)
        {
            balanceHUD.enabled = IsOwner;
            if (IsOwner && !IsServer) balanceHUD.ReadoutSource = () => readout;
        }

        if (!IsOwner) return;
        localInput.ResetHandler = RequestReset;
        if (!IsServer) NetworkManager.NetworkTickSystem.Tick += SendInput;
        Local = this;
        LocalPlayerSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
        isFallen.OnValueChanged -= OnFallenChanged;
        if (IsServer)
        {
            playerController.Fell -= OnFell;
            playerController.CharacterReset -= OnCharacterReset;
        }

        NetworkTickSystem ticks = NetworkManager.NetworkTickSystem;
        if (ticks == null) return;
        ticks.Tick -= CopyDrunkAmount;
        ticks.Tick -= SendReadout;
        ticks.Tick -= SendInput;
    }

    void OnFallenChanged(bool _, bool fallen) => FallenChanged?.Invoke(fallen);

    void CopyDrunkAmount() => drunkAmount.Value = playerController.DrunkAmount;

    // --- Input: owning client → host, once per network tick --------------------------------------------

    void SendInput() => SubmitInputRpc(++lastSentInput, playerController.CurrentInput);

    // Unreliable: the input is absolute, so a lost packet is replaced by the next one.
    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
    void SubmitInputRpc(uint sequence, CharacterInput input)
    {
        if (sequence <= lastAppliedInput) return;
        lastAppliedInput = sequence;
        playerController.SetInput(input);
    }

    // --- HUD and fall state: host → owner -----------------------------------------------------------------

    void SendReadout() => ReceiveReadoutRpc(BalanceReadout.Capture(playerController, balanceController));

    // Unreliable: each readout is complete, so a lost one is replaced by the next.
    [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable)]
    void ReceiveReadoutRpc(BalanceReadout latest) => readout = latest;

    void OnFell()
    {
        isFallen.Value = true;
        if (!IsOwnedByServer) ShowFallRpc();
    }

    void OnCharacterReset() => isFallen.Value = false;

    // Reliable: a one-off event, unlike the readout. On the host, the HUD already flashed on Fell.
    [Rpc(SendTo.Owner)]
    void ShowFallRpc()
    {
        if (balanceHUD != null) balanceHUD.ShowFall();
    }

    // --- Reset: only the host moves bodies --------------------------------------------------------------

    void RequestReset()
    {
        if (IsServer) ResetOnHost();
        else RequestResetRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void RequestResetRpc() => ResetOnHost();

    void ResetOnHost()
    {
        playerController.ResetCharacter();
        ResetInputRpc();
    }

    // The owner's input re-syncs to the spawn heading, or it would turn the reset character straight back.
    // On the host, ResetCharacter has already done this.
    [Rpc(SendTo.Owner)]
    void ResetInputRpc()
    {
        if (IsServer) return;
        playerController.SetInput(playerController.NeutralInput());
        localInput.SyncHeading();
    }
}
