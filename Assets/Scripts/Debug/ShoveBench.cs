using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Debug tool. Shoves the character from a still stance, optionally holding a key through the recovery, and
// records each trial to Logs/catch_<prefix>_<n>.csv. Add it to an empty GameObject at runtime and call Run.
// Resets with ResetCharacter rather than a scene reload, so runtime tuning carries across trials.
public class ShoveBench : MonoBehaviour
{
    // Seconds to settle after a reset before the shove.
    const float SettleTime = 1.5f;

    public bool IsRunning { get; private set; }

    // shoves: velocity added to every body, m/s, world space. keys: held from the shove to the trial's
    // end, one per shove (Key.None for none).
    public void Run(string prefix, Vector3[] shoves, Key[] keys, float seconds, float timeScale)
    {
        StartCoroutine(RunAll(prefix, shoves, keys, seconds, timeScale));
    }

    IEnumerator RunAll(string prefix, Vector3[] shoves, Key[] keys, float seconds, float timeScale)
    {
        IsRunning = true;
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        Rigidbody[] bodies = playerController.GetComponent<BalanceSensor>().Bodies;
        CatchRecorder recorder = playerController.GetComponent<CatchRecorder>();
        if (recorder == null) recorder = playerController.gameObject.AddComponent<CatchRecorder>();
        Time.timeScale = timeScale;

        for (int i = 0; i < shoves.Length; i++)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            playerController.ResetCharacter();
            yield return new WaitForSeconds(SettleTime);

            Key key = keys != null && i < keys.Length ? keys[i] : Key.None;
            if (key != Key.None) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));

            yield return new WaitForFixedUpdate();
            foreach (Rigidbody body in bodies)
                body.AddForce(shoves[i], ForceMode.VelocityChange);

            recorder.Record($"{prefix}_{i}", seconds);
            while (recorder.IsRecording) yield return null;
        }

        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        Time.timeScale = 1f;
        IsRunning = false;
        Debug.Log($"ShoveBench: {prefix} done, {shoves.Length} trials");
    }
}
