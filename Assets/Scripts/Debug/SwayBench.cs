using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Debug tool. Replays the drunk sway from fixed seeds, reloading the scene for each so every run starts
// from the same quiet stance, and records each with CatchRecorder to Logs/catch_<prefix>_s<seed>.csv.
// Add it to an empty GameObject at runtime and call Run; it survives the reloads. Never in the scene.
//
// Time-scaled to save waiting: physics still steps at the fixed rate, so the runs are the same, only
// sooner. A seed replays its fall to within about a second; the start isn't bit-exact.
public class SwayBench : MonoBehaviour
{
    // Spawn settles in about 1 s with the legs live; the sway starts after it.
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
            // Waited out in full: a frame after LoadScene the old scene's player can still be found, and
            // setting it sober left the new one settling at whatever drunkenness the scene holds.
            yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
            Time.timeScale = timeScale;
            // No held keys carried in: a reload doesn't clear the keyboard, and a W left down by an earlier
            // test turned a whole idle benchmark into a drunk walk.
            if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);

            // Sober while it settles, whatever the scene says.
            PlayerController controller = FindAnyObjectByType<PlayerController>();
            controller.SetDrunkAmount(0f);
            yield return new WaitForSeconds(SettleTime);

            BalanceSensor sensor = controller.GetComponent<BalanceSensor>();
            if (sensor.CenterOfMass.y < 0.75f)
                Debug.LogWarning($"SwayBench: seed {seed} starts already down (CoM {sensor.CenterOfMass.y:F2} m)");

            controller.SetDrunkAmount(drunkAmount);
            controller.ResetSway(seed);
            CatchRecorder recorder = controller.gameObject.AddComponent<CatchRecorder>();
            recorder.Record($"{prefix}_s{seed}", seconds);
            while (recorder.IsRecording) yield return null;
        }
        Time.timeScale = 1f;
        IsRunning = false;
        Debug.Log($"SwayBench: {prefix} done, {seeds.Length} runs");
    }
}
