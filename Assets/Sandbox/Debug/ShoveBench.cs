using System.Collections;
using UnityEngine;

// Debug tool. Shoves the character from a still stance, optionally holding a move input through the
// recovery, and records each trial to Logs/catch_<prefix>_<n>.csv. Add it to an empty GameObject at runtime
// and call Run. Resets with ResetCharacter rather than a scene reload, so runtime tuning carries across
// trials. The local player's devices are ignored while it runs.
public class ShoveBench : MonoBehaviour
{
    // Seconds to settle after a reset before the shove.
    const float SettleTime = 1.5f;

    public bool IsRunning { get; private set; }

    // shoves: velocity added to every body, m/s, world space. moves: held from the shove to the trial's end,
    // heading frame (x right, y forward), one per shove; null or missing entries hold still.
    public void Run(string prefix, Vector3[] shoves, Vector2[] moves, float seconds, float timeScale)
    {
        StartCoroutine(RunAll(prefix, shoves, moves, seconds, timeScale));
    }

    IEnumerator RunAll(string prefix, Vector3[] shoves, Vector2[] moves, float seconds, float timeScale)
    {
        IsRunning = true;
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        Rigidbody[] bodies = playerController.GetComponent<BalanceSensor>().Bodies;
        CatchRecorder recorder = playerController.GetComponent<CatchRecorder>();
        if (recorder == null) recorder = playerController.gameObject.AddComponent<CatchRecorder>();
        LocalPlayerInput localInput = playerController.GetComponent<LocalPlayerInput>();
        if (localInput != null) localInput.enabled = false;
        Time.timeScale = timeScale;

        for (int i = 0; i < shoves.Length; i++)
        {
            // Also makes the input neutral.
            playerController.ResetCharacter();
            yield return new WaitForSeconds(SettleTime);

            CharacterInput input = playerController.NeutralInput();
            if (moves != null && i < moves.Length) input.Move = moves[i];
            playerController.SetInput(input);

            yield return new WaitForFixedUpdate();
            foreach (Rigidbody body in bodies)
                body.AddForce(shoves[i], ForceMode.VelocityChange);

            recorder.Record($"{prefix}_{i}", seconds);
            while (recorder.IsRecording) yield return null;
        }

        playerController.SetInput(playerController.NeutralInput());
        if (localInput != null) localInput.enabled = true;
        Time.timeScale = 1f;
        IsRunning = false;
        Debug.Log($"ShoveBench: {prefix} done, {shoves.Length} trials");
    }
}
