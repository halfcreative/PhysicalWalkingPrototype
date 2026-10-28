using System.Collections;
using System.IO;
using UnityEngine;

// Debug tool. Walks the character along routes over the terrain course: reset, a sober settle, then steer
// through each route's waypoints with full forward input, stopping at the last. Logs one line per route to
// Logs/terrain_bench.txt: whether it fell, whether it arrived, the lowest height above its support, and the
// support heights the sensor measured (which should follow the terrain). Run it on the host: add it to an
// empty GameObject at runtime and call Run or RunLanes.
public class TerrainBench : MonoBehaviour
{
    const float SettleTime = 1.5f;
    // Close enough to a waypoint to head for the next one, m.
    const float WaypointReach = 0.5f;
    // Steering rate, degrees per second. Gentle: at 60 a sober character walking at full input toppled on
    // flat ground while turning toward a lane, which says nothing about the terrain.
    const float TurnRate = 30f;
    // Heading error, degrees, at which forward input has eased to zero: it slows to turn instead of arcing wide.
    const float StopToTurnAngle = 60f;

    public bool IsRunning { get; private set; }

    // Each lane is a route from just before startZ straight along x to endZ.
    public void RunLanes(string label, float[] laneXs, float startZ, float endZ, float seconds, float drunkAmount = 0f,
                         float timeScale = 1f, bool record = false)
    {
        var routes = new Vector3[laneXs.Length][];
        for (int i = 0; i < laneXs.Length; i++)
            routes[i] = new[] { new Vector3(laneXs[i], 0f, startZ - 1f), new Vector3(laneXs[i], 0f, endZ) };
        Run(label, routes, seconds, drunkAmount, timeScale, record);
    }

    // record: also write each route to Logs/catch_<label>_<n>.csv with CatchRecorder, for taking a fall apart.
    // startAtRoute: reset each run standing on the route's first waypoint, facing the second, instead of at
    // the spawn point; for routes far from it.
    public void Run(string label, Vector3[][] routes, float seconds, float drunkAmount = 0f, float timeScale = 1f,
                    bool record = false, bool startAtRoute = false)
    {
        StartCoroutine(RunAll(label, routes, seconds, drunkAmount, timeScale, record, startAtRoute));
    }

    IEnumerator RunAll(string label, Vector3[][] routes, float seconds, float drunkAmount, float timeScale, bool record,
                       bool startAtRoute)
    {
        IsRunning = true;
        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        BalanceSensor balanceSensor = playerController.GetComponent<BalanceSensor>();
        LocalPlayerInput localInput = playerController.GetComponent<LocalPlayerInput>();
        bool inputWasEnabled = localInput != null && localInput.enabled;
        if (localInput != null) localInput.enabled = false;
        CatchRecorder recorder = null;
        if (record)
        {
            recorder = playerController.GetComponent<CatchRecorder>();
            if (recorder == null) recorder = playerController.gameObject.AddComponent<CatchRecorder>();
        }
        Time.timeScale = timeScale;
        // ResetCharacter poses the bodies relative to the root, so moving the root moves the spawn.
        Transform root = playerController.transform;
        Vector3 spawnPosition = root.position;
        Quaternion spawnRotation = root.rotation;

        for (int run = 0; run < routes.Length; run++)
        {
            Vector3[] waypoints = routes[run];
            if (startAtRoute && waypoints.Length > 1)
            {
                Vector3 facing = (waypoints[1] - waypoints[0]).Flat();
                root.SetPositionAndRotation(new Vector3(waypoints[0].x, spawnPosition.y, waypoints[0].z),
                                            Quaternion.LookRotation(facing, Vector3.up));
            }
            playerController.SetDrunkAmount(0f);
            playerController.ResetCharacter();
            yield return new WaitForSeconds(SettleTime);
            playerController.SetDrunkAmount(drunkAmount);
            if (recorder != null) recorder.Record($"{label}_{run}", seconds);

            int next = 0;
            bool arrived = false;
            float fellAt = -1f, minHeight = float.MaxValue, minSupport = float.MaxValue, maxSupport = float.MinValue;
            float yaw = startAtRoute ? root.eulerAngles.y : playerController.CurrentInput.HeadingYaw;
            float start = Time.time;

            while (Time.time - start < seconds)
            {
                yield return new WaitForFixedUpdate();
                Vector3 com = balanceSensor.CenterOfMass;
                Vector3 toWaypoint = (waypoints[next] - com).Flat();
                if (toWaypoint.magnitude < WaypointReach || Passed(waypoints, next, com))
                {
                    if (next == waypoints.Length - 1) arrived = true;
                    else toWaypoint = (waypoints[++next] - com).Flat();
                }

                float targetYaw = Mathf.Atan2(toWaypoint.x, toWaypoint.z) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, TurnRate * Time.fixedDeltaTime);
                float forward = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) / StopToTurnAngle);
                playerController.SetInput(new CharacterInput
                {
                    HeadingYaw = yaw,
                    Move = arrived ? Vector2.zero : Vector2.up * forward,
                });

                if (playerController.IsFallen)
                {
                    fellAt = Time.time - start;
                    break;
                }
                minHeight = Mathf.Min(minHeight, balanceSensor.HeightAboveSupport);
                minSupport = Mathf.Min(minSupport, balanceSensor.SupportCentre.y);
                maxSupport = Mathf.Max(maxSupport, balanceSensor.SupportCentre.y);
                if (arrived) break;
            }
            // Stand a moment at the end, so the final support height is the settled one.
            if (arrived)
            {
                playerController.SetInput(new CharacterInput { HeadingYaw = yaw });
                yield return new WaitForSeconds(1f);
            }
            // A run that ended early still records to the end, so the fall's aftermath is in the file.
            while (recorder != null && recorder.IsRecording) yield return null;

            Vector3 end = waypoints[^1];
            string outcome = fellAt >= 0f ? $"FELL at {fellAt:F1}s" : arrived ? "arrived" : "timed out";
            string line = $"{label} route {run} to ({end.x}, {end.z}): {outcome} at waypoint {next + 1}/{waypoints.Length}, " +
                          $"final support y {balanceSensor.SupportCentre.y:F2}, min height above support {minHeight:F2} m, " +
                          $"support y {minSupport:F2}..{maxSupport:F2}";
            Debug.Log("TerrainBench " + line, this);
            File.AppendAllText(Path.Combine(Application.dataPath, "..", "Logs", "terrain_bench.txt"),
                               $"{System.DateTime.Now:HH:mm:ss} {line}\n");
        }

        if (startAtRoute)
        {
            root.SetPositionAndRotation(spawnPosition, spawnRotation);
            playerController.ResetCharacter();
        }
        playerController.SetInput(playerController.NeutralInput());
        if (localInput != null) localInput.enabled = inputWasEnabled;
        Time.timeScale = 1f;
        IsRunning = false;
    }

    // Beyond the last waypoint along the leg leading to it: a walk that missed it by more than WaypointReach
    // would otherwise turn back and circle it. Turns keep the radius, so routes take their corners the same.
    static bool Passed(Vector3[] waypoints, int next, Vector3 com) =>
        next > 0 && next == waypoints.Length - 1 &&
        Vector3.Dot((waypoints[next] - waypoints[next - 1]).Flat(), (com - waypoints[next]).Flat()) > 0f;
}
