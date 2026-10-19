using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Debug tool. Measures how long the local player's character takes to visibly respond to input on this
// screen: the heading is stepped by stepDegrees, and the timer stops once the rendered pelvis has turned
// thresholdDegrees toward it. On the host that is the physics response alone; on a client it adds the
// round trip and interpolation, so the difference between the two is the network's share. Resolution is
// one rendered frame. Add it to an empty GameObject at runtime and call Run, or press NetworkSession's
// measure key on a client for a sweep across connections. Results append to Logs/response_probe.txt. The
// local player's devices are ignored while it runs.
public class ResponseProbe : MonoBehaviour
{
    const float SettleTime = 1.5f;
    const float Timeout = 2f;
    // Below this centre-of-mass height the character is down and gets reset before the next trial.
    const float DownHeight = 0.6f;

    public bool IsRunning { get; private set; }
    public string Summary { get; private set; }

    public void Run(string label, int trials, float stepDegrees = 45f, float thresholdDegrees = 10f)
    {
        StartCoroutine(RunAll(label, trials, stepDegrees, thresholdDegrees));
    }

    // Runs the probe once per simulated connection, then restores the connection it started on.
    public void RunSweep(NetworkSession session, NetworkSession.SimulatedConnection[] connections, int trials)
    {
        StartCoroutine(Sweep(session, connections, trials));
    }

    IEnumerator Sweep(NetworkSession session, NetworkSession.SimulatedConnection[] connections, int trials)
    {
        IsRunning = true;
        NetworkSession.SimulatedConnection original = session.Connection;
        foreach (NetworkSession.SimulatedConnection connection in connections)
        {
            session.SimulateConnection(connection);
            // Lets the measured round-trip time settle on the new delay.
            yield return new WaitForSecondsRealtime(2f);
            yield return RunAll(connection.ToString(), trials, 45f, 10f);
            IsRunning = true;
        }
        session.SimulateConnection(original);
        IsRunning = false;
    }

    IEnumerator RunAll(string label, int trials, float stepDegrees, float thresholdDegrees)
    {
        IsRunning = true;
        NetworkPlayer localPlayer = null;
        foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            if (player.IsOwner) localPlayer = player;

        PlayerController playerController = localPlayer.GetComponent<PlayerController>();
        BalanceSensor balanceSensor = localPlayer.GetComponent<BalanceSensor>();
        LocalPlayerInput localInput = localPlayer.GetComponent<LocalPlayerInput>();
        Transform pelvis = localPlayer.transform.Find("Pelvis");
        bool inputWasEnabled = localInput.enabled;
        localInput.enabled = false;

        var milliseconds = new List<float>();
        int timeouts = 0, resets = 0;
        float yaw = playerController.CurrentInput.HeadingYaw;

        for (int i = 0; i < trials; i++)
        {
            playerController.SetInput(new CharacterInput { HeadingYaw = yaw });
            yield return new WaitForSecondsRealtime(SettleTime);

            if (balanceSensor.InterpolatedCenterOfMass().y < DownHeight)
            {
                resets++;
                localInput.ResetHandler?.Invoke();
                yaw = playerController.NeutralInput().HeadingYaw;
                continue;
            }

            float sign = i % 2 == 0 ? 1f : -1f;
            float startYaw = YawOf(pelvis);
            yaw = Mathf.Repeat(yaw + sign * stepDegrees, 360f);
            float start = Time.realtimeSinceStartup;
            playerController.SetInput(new CharacterInput { HeadingYaw = yaw });

            bool responded = false;
            while (Time.realtimeSinceStartup - start < Timeout)
            {
                yield return null;
                if (sign * Mathf.DeltaAngle(startYaw, YawOf(pelvis)) < thresholdDegrees) continue;
                milliseconds.Add((Time.realtimeSinceStartup - start) * 1000f);
                responded = true;
                break;
            }
            if (!responded) timeouts++;
        }

        localInput.enabled = inputWasEnabled;
        Summary = $"{label}: {Describe(milliseconds)}, timeouts {timeouts}, resets {resets}, RTT {RoundTripMs()} ms";
        Debug.Log($"ResponseProbe {Summary}", this);
        File.AppendAllText(Path.Combine(Application.dataPath, "..", "Logs", "response_probe.txt"),
                           $"{System.DateTime.Now:HH:mm:ss} {(NetworkManager.Singleton.IsServer ? "host" : "client")} {Summary}\n");
        IsRunning = false;
    }

    static float YawOf(Transform body)
    {
        Vector3 forward = body.forward;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    static string Describe(List<float> values)
    {
        if (values.Count == 0) return "no responses";
        values.Sort();
        float sum = 0f;
        foreach (float v in values) sum += v;
        return $"n {values.Count}, median {values[values.Count / 2]:F0} ms, mean {sum / values.Count:F0} ms, " +
               $"min {values[0]:F0} ms, max {values[values.Count - 1]:F0} ms";
    }

    // 0 on the host.
    static ulong RoundTripMs()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager.IsServer || manager.NetworkConfig.NetworkTransport is not UnityTransport transport) return 0;
        return transport.GetCurrentRtt(NetworkManager.ServerClientId);
    }
}
