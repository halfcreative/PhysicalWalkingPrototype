using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

// Starts a session and places joining players. In the editor, the main instance hosts and every extra
// Multiplayer Play Mode instance joins it as a client, through a simulated connection so the host's own
// player isn't the only feel being judged. Otherwise, or with autoStartInEditor off, it shows Host and
// Client buttons.
[RequireComponent(typeof(NetworkManager))]
public class NetworkSession : MonoBehaviour
{
    // Network Simulator presets: one-way delay, jitter and packet loss.
    public enum SimulatedConnection
    {
        None,
        HomeFiber,       // 10 ms
        HomeCable,       // 25 ms
        HomeBroadband,   // 32 ms
        Congested,       // 50 ms ±50, 1% loss
        Satellite,       // 100 ms ±10
        Mobile4G,        // 100 ms ±20, 4% loss
        Mobile3_5G,      // 160 ms ±30, 6% loss
        Mobile3G,        // 360 ms ±30, 7% loss
    }

    // Players spawn in a row along +X from here, facing +Z.
    [SerializeField] Vector3 firstSpawn = Vector3.zero;
    [SerializeField] float spawnSpacing = 1.5f;
    [SerializeField] bool autoStartInEditor = true;
    // Applied to extra Multiplayer Play Mode instances only; the cycle key steps through the rest live.
    [SerializeField] SimulatedConnection clientConnection = SimulatedConnection.Satellite;
    [SerializeField] InputAction cycleConnectionAction = new("Cycle Connection", InputActionType.Button, "<Keyboard>/f10");
    // Runs ResponseProbe across a range of connections on a simulating client.
    [SerializeField] InputAction measureAction = new("Measure Response", InputActionType.Button, "<Keyboard>/f11");

    static readonly SimulatedConnection[] MeasuredConnections =
    {
        SimulatedConnection.None, SimulatedConnection.HomeBroadband, SimulatedConnection.Satellite,
        SimulatedConnection.Mobile3_5G,
    };

    NetworkManager networkManager;
    int playersSpawned;
    bool simulating;
    ResponseProbe probe;

    public SimulatedConnection Connection => clientConnection;

    void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = Approve;
    }

    void Start()
    {
#if UNITY_EDITOR
        if (!autoStartInEditor) return;
        if (CurrentPlayer.IsMainEditor)
        {
            networkManager.StartHost();
        }
        else
        {
            SimulateConnection(clientConnection);
            networkManager.StartClient();
        }
#endif
    }

    void OnEnable()
    {
        cycleConnectionAction.Enable();
        measureAction.Enable();
    }

    void OnDisable()
    {
        cycleConnectionAction.Disable();
        measureAction.Disable();
    }

    void Update()
    {
        if (!simulating || (probe != null && probe.IsRunning)) return;

        if (cycleConnectionAction.WasPressedThisFrame())
        {
            int count = System.Enum.GetValues(typeof(SimulatedConnection)).Length;
            SimulateConnection((SimulatedConnection)(((int)clientConnection + 1) % count));
        }

        if (measureAction.WasPressedThisFrame())
        {
            if (probe == null) probe = gameObject.AddComponent<ResponseProbe>();
            probe.RunSweep(this, MeasuredConnections, 10);
        }
    }

    // Changes this instance's simulated connection; takes effect immediately, mid-session included.
    public void SimulateConnection(SimulatedConnection connection)
    {
        if (!TryGetComponent(out NetworkSimulator simulator))
            simulator = gameObject.AddComponent<NetworkSimulator>();
        simulator.ConnectionPreset = connection switch
        {
            SimulatedConnection.HomeFiber => NetworkSimulatorPresets.HomeOptical,
            SimulatedConnection.HomeCable => NetworkSimulatorPresets.HomeCable,
            SimulatedConnection.HomeBroadband => NetworkSimulatorPresets.HomeBroadband,
            SimulatedConnection.Congested => NetworkSimulatorPresets.HomeCongestedNetwork,
            SimulatedConnection.Satellite => NetworkSimulatorPresets.HomeSatellite,
            SimulatedConnection.Mobile4G => NetworkSimulatorPresets.Mobile4G,
            SimulatedConnection.Mobile3_5G => NetworkSimulatorPresets.Mobile3_5G,
            SimulatedConnection.Mobile3G => NetworkSimulatorPresets.Mobile3G,
            _ => NetworkSimulatorPresets.None,
        };
        clientConnection = connection;
        simulating = true;
        Debug.Log($"NetworkSession: simulating {simulator.ConnectionPreset.Name}", this);
    }

    // Runs on the host for every connection, its own included.
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = firstSpawn + Vector3.right * (spawnSpacing * playersSpawned++);
        response.Rotation = Quaternion.identity;
    }

    void OnGUI()
    {
        if (networkManager.IsListening)
        {
            if (simulating)
                GUI.Label(new Rect(10f, 10f, 700f, 22f), probe != null && probe.IsRunning
                    ? $"Measuring response on {clientConnection}…"
                    : $"Simulated connection: {clientConnection} ({cycleConnectionAction.GetBindingDisplayString()} for next, " +
                      $"{measureAction.GetBindingDisplayString()} to measure)");
            return;
        }

        GUILayout.BeginArea(new Rect(10f, 10f, 160f, 80f));
        if (GUILayout.Button("Host")) networkManager.StartHost();
        if (GUILayout.Button("Client")) networkManager.StartClient();
        GUILayout.EndArea();
    }
}
