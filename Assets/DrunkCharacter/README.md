# Drunk Character

An active-ragdoll player character: a physical ragdoll that follows an animated ghost rig, balances by
stepping, and has a drunkenness slider. Networked with Netcode for GameObjects, host-authoritative: the
host simulates every character; clients get kinematic copies placed by the pose stream.

Everything the character needs is in this folder (assembly `DrunkCharacter`). The test bed that drives it
here (`Assets/Sandbox`: the session bootstrap and the debug benches) is not part of it.

## Moving it into another project

Copy this folder **with its `.meta` files**, so the prefab's references keep their GUIDs. Then:

### Packages

| Package | Version here |
|---|---|
| `com.unity.netcode.gameobjects` | 2.13.3 |
| `com.unity.animation.rigging` | 6.6.0 |
| `com.unity.cinemachine` | 6.6.0 (only `FollowLocalPlayer` uses it) |
| `com.unity.inputsystem` | 1.20.0 |
| `com.unity.render-pipelines.universal` | 17.6.0 (the body uses URP's Lit material) |

### Project settings

These live in `ProjectSettings/`, not in assets, so they don't come with the folder. Set them by hand;
don't copy the settings files over the game's own.

- **Layer `PlayerBody`** (layer 8 here). The prefab's bodies are on it, and `Ground` treats every layer
  except it as ground. The prefab stores the layer's number, so if 8 is taken, re-assign the prefab's
  bodies to whichever number it gets.
- **Layer collision matrix:** `PlayerBody` collides with everything, itself included (characters bump into
  each other; a character's own parts ignore each other in code). Any layer the character should walk on
  must collide with `PlayerBody`.
- **Physics:** fixed timestep 0.01 s (100 Hz), default solver iterations 16, velocity iterations 4,
  gravity −9.81. The drives and balance were tuned at these.

### Scene setup

1. A `NetworkManager` with `Player.prefab` as its player prefab (or in its network prefabs list).
2. A `CinemachineCamera` with `FollowLocalPlayer` on it. It follows the local character's camera target
   once that spawns. The camera here uses ThirdPersonFollow and ThirdPersonAim; avoid collisions with
   the `Default` layer only, so it never collides with the character.
3. Start a session as host or client; the character spawns at the position the connection approval gives.

## Hooks for the game

On `NetworkPlayer`:

- `NetworkPlayer.Local` / `LocalPlayerSpawned`: this instance's own character.
- `CameraTarget`: where a camera should follow and look.
- `IsFallen` and `FallenChanged`: the same on every side.
- `DrunkAmount`: the same on every side. Set it on the host with `PlayerController.SetDrunkAmount`.

Input comes from `LocalPlayerInput` on the owner and travels to the host as a `CharacterInput`; anything
else can drive a character on the host with `PlayerController.SetInput`.
