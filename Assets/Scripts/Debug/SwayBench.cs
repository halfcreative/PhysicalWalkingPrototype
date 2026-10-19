using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Debug tool. Replays the drunk sway from fixed seeds, reloading the scene for each run, and records each to
// Logs/catch_<prefix>_s<seed>.csv. Add it to an empty GameObject at runtime and call Run; it survives the
// reloads. Time-scaled: physics still steps at the fixed rate, so runs only finish sooner.
public class SwayBench : MonoBehaviour
{
    // Seconds to settle, sober, before the sway starts.
    const float SettleTime = 1.5f;

    public bool IsRunning { get; private set; }

    public void Run(string prefix, float drunkAmount, int[] seeds, float seconds, float timeScale)
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(RunAll(prefix, drunkAmount, seeds, seconds, timeScale));
    }

    IEnumerator RunAll(string prefix, float drunkAmount, int[] seeds, float seconds, float timeScale)
    {
        IsRunning = true;
        foreach (int seed in seeds)
        {
            // Wait for the load to finish, or the old scene's player is still found.
            yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
            Time.timeScale = timeScale;
            // A reload doesn't release held keys.
            if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);

            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            playerController.SetDrunkAmount(0f);
            yield return new WaitForSeconds(SettleTime);

            BalanceSensor balanceSensor = playerController.GetComponent<BalanceSensor>();
            if (balanceSensor.CenterOfMass.y < 0.75f)
                Debug.LogWarning($"SwayBench: seed {seed} starts already down (CoM {balanceSensor.CenterOfMass.y:F2} m)");

            playerController.SetDrunkAmount(drunkAmount);
            playerController.ResetSway(seed);
            CatchRecorder recorder = playerController.gameObject.AddComponent<CatchRecorder>();
            recorder.Record($"{prefix}_s{seed}", seconds);
            while (recorder.IsRecording) yield return null;
        }
        Time.timeScale = 1f;
        IsRunning = false;
        Debug.Log($"SwayBench: {prefix} done, {seeds.Length} runs");
    }
}
