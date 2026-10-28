using Unity.Netcode;
using UnityEngine;

// Everything BalanceHUD draws, captured where the simulation runs. The host captures it live; a client
// receives its own character's from the host (NetworkPlayer). Vectors are in the heading's frame.
public struct BalanceReadout : INetworkSerializable
{
    public Vector2 Sway;
    public Vector2 MoveInput;
    public Vector2 Momentum;
    public float MomentumLimit;
    public float FallMeterFraction;
    public float MomentumCharge;
    public float TiltCharge;
    public float DropCharge;
    public float PelvisTilt;
    public float HeightAboveSupport;
    public float CounterSteer;

    public Vector2 NetForce => Sway + MoveInput;

    public static BalanceReadout Capture(PlayerController playerController, BalanceController balanceController) => new()
    {
        Sway = playerController.Sway,
        MoveInput = playerController.MoveInput,
        Momentum = playerController.Momentum,
        MomentumLimit = playerController.MomentumLimit,
        FallMeterFraction = playerController.FallMeterFraction,
        MomentumCharge = playerController.MomentumCharge,
        TiltCharge = playerController.TiltCharge,
        DropCharge = playerController.DropCharge,
        PelvisTilt = playerController.PelvisTilt,
        HeightAboveSupport = playerController.HeightAboveSupport,
        CounterSteer = balanceController != null ? balanceController.CounterSteer : 0f,
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Sway);
        serializer.SerializeValue(ref MoveInput);
        serializer.SerializeValue(ref Momentum);
        serializer.SerializeValue(ref MomentumLimit);
        serializer.SerializeValue(ref FallMeterFraction);
        serializer.SerializeValue(ref MomentumCharge);
        serializer.SerializeValue(ref TiltCharge);
        serializer.SerializeValue(ref DropCharge);
        serializer.SerializeValue(ref PelvisTilt);
        serializer.SerializeValue(ref HeightAboveSupport);
        serializer.SerializeValue(ref CounterSteer);
    }
}
