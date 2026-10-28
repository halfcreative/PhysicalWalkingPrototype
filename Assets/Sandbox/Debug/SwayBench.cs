using System.Collections;
using UnityEngine;

// Debug tool. Replays the drunk sway from fixed seeds with neutral input, and records each run to
// Logs/catch_<prefix>_s<seed>.csv. Each run starts from ResetCharacter and a sober settle. Run it on the
// host: add it to an empty GameObject at runtime and call Run. Time-scaled: physics still steps at the
// fixed rate, so runs only finish sooner. A seed replays its fall to within about a second.
public class SwayBench : MonoBehaviour
{
    // Seconds to settle, sober, before the sway starts.
    const float SettleTime = 1.5f;

    public bool IsRunning { get; private set; }

    public void Run(string prefix, float drunkAmount, int[] seeds, float seconds, float timeScale)
    {
        StartCoroutine(RunAll(prefix, drunkAmount, seeds, seconds, timeScale));
    }

    IEnumerator RunAll(string prefix, float drunkAmount, int[] seeds, float seconds, float timeScale)
    {
        IsRunning = true;
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        BalanceSensor balanceSensor = playerController.GetComponent<BalanceSensor>();
        CatchRecorder recorder = playerController.GetComponent<CatchRecorder>();
        if (recorder == null) recorder = playerController.gameObject.AddComponent<CatchRecorder>();
        // The local player's devices are ignored; ResetCharacter leaves the input neutral.
        LocalPlayerInput localInput = playerController.GetComponent<LocalPlayerInput>();
        bool inputWasEnabled = localInput != null && localInput.enabled;
        if (localInput != null) localInput.enabled = false;
        Time.timeScale = timeScale;

        foreach (int seed in seeds)
        {
            playerController.SetDrunkAmount(0f);
            playerController.ResetCharacter();
            yield return new WaitForSeconds(SettleTime);

            if (balanceSensor.HeightAboveSupport < 0.75f)
                Debug.LogWarning($"SwayBench: seed {seed} starts already down (CoM {balanceSensor.HeightAboveSupport:F2} m above support)");

            playerController.SetDrunkAmount(drunkAmount);
            playerController.ResetSway(seed);
            recorder.Record($"{prefix}_s{seed}", seconds);
            while (recorder.IsRecording) yield return null;
        }

        if (localInput != null) localInput.enabled = inputWasEnabled;
        Time.timeScale = 1f;
        IsRunning = false;
        Debug.Log($"SwayBench: {prefix} done, {seeds.Length} runs");
    }
}
