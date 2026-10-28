using Unity.Cinemachine;
using UnityEngine;

// On the scene's player camera: follows and looks at this instance's own character once it spawns.
[RequireComponent(typeof(CinemachineCamera))]
public class FollowLocalPlayer : MonoBehaviour
{
    CinemachineCamera virtualCamera;

    void Awake() => virtualCamera = GetComponent<CinemachineCamera>();

    void OnEnable()
    {
        NetworkPlayer.LocalPlayerSpawned += Follow;
        if (NetworkPlayer.Local != null) Follow(NetworkPlayer.Local);
    }

    void OnDisable() => NetworkPlayer.LocalPlayerSpawned -= Follow;

    void Follow(NetworkPlayer player)
    {
        virtualCamera.Follow = player.CameraTarget;
        virtualCamera.LookAt = player.CameraTarget;
    }
}
