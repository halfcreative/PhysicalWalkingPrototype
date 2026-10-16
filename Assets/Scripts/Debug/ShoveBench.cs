using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Debug tool. Shoves the character from a still stance, the same shove every time, optionally with a key
// held through the recovery, and records each trial with CatchRecorder to Logs/catch_<prefix>_<n>.csv.
// For measuring how far the body can tip and still come back, and what input helps. Add it to an empty
// GameObject at runtime and call Run. Never in the scene.
//
// Resets with PlayerController.ResetCharacter rather than a scene reload, so values changed at runtime
// (say, a breach threshold set out of reach to watch recoveries play out) carry across trials.
public class ShoveBench : MonoBehaviour
{
    // Settle after a reset before the shove: the reset pose is still, but the drives take a moment.
    const float SettleTime = 1.5f;

    public bool IsRunning { get; private set; }

    // shoves: velocity added to every body, m/s, world space. keys: held from the shove to the trial's
    // end, Key.None for none, one per shove.
    public void Run(string prefix, Vector3[] shoves, Key[] keys, float seconds, float timeScale)
    {
        StartCoroutine(RunAll(prefix, shoves, keys, seconds, timeScale));
    }

    IEnumerator RunAll(string prefix, Vector3[] shoves, Key[] keys, float seconds, float timeScale)
    {
        IsRunning = true;
        PlayerController controller = FindAnyObjectByType<PlayerController>();
        Rigidbody[] bodies = controller.GetComponentsInChildren<Rigidbody>();
        CatchRecorder recorder = controller.GetComponent<CatchRecorder>();
        if (recorder == null) recorder = controller.gameObject.AddComponent<CatchRecorder>();
        Time.timeScale = timeScale;

        for (int i = 0; i < shoves.Length; i++)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            controller.ResetCharacter();
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
