using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Streams a character's pose from the host to every client: one PoseFrame per network tick, all bodies
// together, so a remote character's limbs always come from the same physics step. Unreliable, since the
// next frame replaces a lost one. Clients play it back slightly in the past, interpolating between frames;
// the delay is one tick plus a multiple of the measured arrival jitter, so it grows on a jittery connection
// and stays short on a clean one.
[RequireComponent(typeof(BalanceSensor), typeof(PlayerController))]
public class NetworkPose : NetworkBehaviour
{
    const int BufferSize = 32;
    // How quickly the jitter estimate follows new arrivals, per frame.
    const double JitterSmoothing = 0.05;
    // How quickly the arrival baseline creeps up after the connection slows, per frame.
    const double BaselineCreep = 0.002;

    // Playback delay beyond the fastest arrival: this many ticks, plus jitterMultiplier × measured jitter.
    [SerializeField] float baseDelayTicks = 1f;
    [SerializeField] float jitterMultiplier = 2f;

    Rigidbody[] bodies;
    PlayerController playerController;
    byte resetCount;

    // Client playback. Frames are kept in tick order.
    readonly List<PoseFrame> frames = new();
    // Local receive time minus frame time for the fastest frame seen recently, and the spread around it.
    double arrivalBaseline = double.MaxValue;
    double jitter;

    double TickInterval => 1.0 / NetworkManager.NetworkConfig.TickRate;
    double PlaybackDelay => baseDelayTicks * TickInterval + jitterMultiplier * jitter;

    void Awake()
    {
        bodies = GetComponent<BalanceSensor>().Bodies;
        playerController = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.NetworkTickSystem.Tick += SendPose;
        playerController.CharacterReset += CountReset;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        if (NetworkManager.NetworkTickSystem != null) NetworkManager.NetworkTickSystem.Tick -= SendPose;
        playerController.CharacterReset -= CountReset;
    }

    void CountReset() => resetCount++;

    // --- Host ---------------------------------------------------------------------------------------------

    void SendPose()
    {
        // Only the host is connected: nobody to send to.
        if (NetworkManager.ConnectedClientsIds.Count <= 1) return;

        var frame = new PoseFrame
        {
            Tick = NetworkManager.ServerTime.Tick,
            ResetCount = resetCount,
            Positions = new Vector3[bodies.Length],
            Rotations = new Quaternion[bodies.Length],
        };
        // The physics pose, not the interpolated transform.
        for (int i = 0; i < bodies.Length; i++)
        {
            frame.Positions[i] = bodies[i].position;
            frame.Rotations[i] = bodies[i].rotation;
        }
        ReceivePoseRpc(frame);
    }

    // --- Client -------------------------------------------------------------------------------------------

    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    void ReceivePoseRpc(PoseFrame frame)
    {
        if (frame.Positions.Length != bodies.Length) return;

        double arrival = Time.unscaledTimeAsDouble - frame.Tick * TickInterval;
        arrivalBaseline = arrival < arrivalBaseline ? arrival : arrivalBaseline + (arrival - arrivalBaseline) * BaselineCreep;
        jitter += (arrival - arrivalBaseline - jitter) * JitterSmoothing;

        // In tick order; a late duplicate or a frame older than the whole buffer is dropped.
        int index = frames.Count;
        while (index > 0 && frames[index - 1].Tick > frame.Tick) index--;
        if (index > 0 && frames[index - 1].Tick == frame.Tick) return;
        if (index == 0 && frames.Count >= BufferSize) return;
        frames.Insert(index, frame);
        if (frames.Count > BufferSize) frames.RemoveAt(0);
    }

    void Update()
    {
        if (!IsSpawned || IsServer || frames.Count == 0) return;

        double playbackTime = Time.unscaledTimeAsDouble - arrivalBaseline - PlaybackDelay;
        double playbackTick = playbackTime / TickInterval;

        // Hold the newest frame if playback has caught up with it, the oldest if it's still behind.
        if (playbackTick >= frames[^1].Tick) { Apply(frames[^1], frames[^1], 0f); return; }
        if (playbackTick <= frames[0].Tick) { Apply(frames[0], frames[0], 0f); return; }

        int next = 1;
        while (frames[next].Tick < playbackTick) next++;
        PoseFrame from = frames[next - 1], to = frames[next];
        float t = (float)((playbackTick - from.Tick) / (to.Tick - from.Tick));

        // Across a reset, snap rather than sweep the body through the teleport.
        if (from.ResetCount != to.ResetCount) Apply(to, to, 0f);
        else Apply(from, to, t);

        // Frames behind playback are no longer needed.
        if (next > 1) frames.RemoveRange(0, next - 1);
    }

    void Apply(PoseFrame from, PoseFrame to, float t)
    {
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].transform.SetPositionAndRotation(
                Vector3.Lerp(from.Positions[i], to.Positions[i], t),
                Quaternion.Slerp(from.Rotations[i], to.Rotations[i], t));
    }
}
