# Stop-Motion Look — Guidebook

**Project:** Physical Walking Prototype, the drunk active-ragdoll character. This guide moves to the game repo
with the character.
**Version:** 0.7, 2026-10-30.
**Owner:** Alan makes every look decision and every asset. This guide explains the look, specifies the systems
and walks through building them. It gives exact values and steps, not finished code.

## Contents

- [How to use this guide](#how-to-use-this-guide) · [Units and scale](#units-and-scale) ·
  [Timeline](#timeline)
- **[Part I — The look](#part-i--the-look)**
  - [1. Summary](#1-summary)
  - [2. Principles](#2-principles)
  - [3. What makes stop motion read](#3-what-makes-stop-motion-read)
- **[Part II — Design](#part-ii--design)**
  - [4. The character today](#4-the-character-today)
  - [5. Puppet and clock](#5-puppet-and-clock)
  - [6. Body construction](#6-body-construction)
  - [7. Readability](#7-readability): eyes, mouth, hands, shoes, player identity
  - [8. Physical comedy](#8-physical-comedy): dents, getting up, cosmetics
  - [9. The clay material (`SG_Clay`)](#9-the-clay-material-sg_clay)
  - [10. Comfort settings](#10-comfort-settings)
  - [11. World, camera and post-processing](#11-world-camera-and-post-processing)
- **[Part III — Build plan](#part-iii--build-plan)**
  - [Phase 1 — Stop-motion prototype and playtest (30–40 h)](#phase-1--stop-motion-prototype-and-playtest-3040-h-4-weeks)
  - [Phase 2 — Maquette and style frame (12–18 h)](#phase-2--maquette-and-style-frame-1218-h-2-weeks)
  - [Phase 3 — Look-dev scene and clay material (22–32 h)](#phase-3--look-dev-scene-and-clay-material-2232-h-3-weeks)
  - [Phase 4A — Readability (25–35 h)](#phase-4a--readability-2535-h-3-weeks)
  - [Phase 4B — Dents, getting up, cosmetics (60–85 h)](#phase-4b--dents-getting-up-cosmetics-6085-h-79-weeks)
  - [Phase 5 — Learn Blender (30–45 h)](#phase-5--learn-blender-3045-h-4-weeks)
  - [Phase 6 — The body parts (40–70 h)](#phase-6--the-body-parts-4070-h-6-weeks-1530-h-on-a-fast-track)
  - [Phase 7 — Face, hands, shoes and cosmetics (24–36 h)](#phase-7--face-hands-shoes-and-cosmetics-2436-h-34-weeks)
  - [Phase 8 — Assemble and tune (12–20 h)](#phase-8--assemble-and-tune-1220-h-2-weeks)
  - [Phase 9 — World, camera and settings (18–26 h)](#phase-9--world-camera-and-settings-1826-h-2-weeks)
  - [Phase 10 (optional) — One skinned body (60–100 h)](#phase-10-optional--one-skinned-body-60100-h)
  - [12. Tests](#12-tests)
  - [13. Dials](#13-dials)
  - [14. Resources](#14-resources)
- **[Part IV — Records](#part-iv--records)**
  - [15. Decision log](#15-decision-log)
  - [16. Risks, open questions and checked facts](#16-risks-open-questions-and-checked-facts)
  - [17. Review record](#17-review-record)
  - [18. Sources](#18-sources)

## How to use this guide

- **Part I** explains the look: what makes stop motion read, and the rules this character follows.
- **Part II** specifies each system. Read the matching section before starting a phase.
- **Part III** is the build plan. Work the phases in order. Each step has an estimate in focused hours. Tick
  every **Done when** box before starting the next phase.
- **Part IV** holds the decision log, risks, review history and sources.
- **Log it.** When a step says "log it", add a dated row to section 15. A logged decision overrides this guide.
- **Dials.** Starting values live in section 13. Change them there and log why.
- **Play mode.** Never save a script while play mode is running; Unity stops play mode.

## Units and scale

- Sizes are in game metres unless marked "maquette".
- **Character:** 1.85 m tall, 62 kg. The head is a 0.36 m sphere, forearms are 0.09 × 0.30 m capsules, hands
  are 0.11 m spheres, and feet are 0.16 × 0.10 × 0.30 m boxes (width × height × length).
- **Maquette:** 20 cm tall, so the scale is 1 : 9.25. Maquette size × 9.25 = game size.
- **Pixels.** With the camera 5 m behind the character, a 60° vertical field of view and 1080p:

  | Distance | Who | 1 pixel covers | Character height |
  |---|---|---|---|
  | 5 m | your own character | 5.4 mm | 345 px |
  | 10 m | a nearby player | 11 mm | 170 px |
  | 15 m | a player across a room | 16 mm | 115 px |

  Anything smaller than about 1 pixel at the distance where it must read is invisible. That is why the face,
  hands and feet are oversized to read at 10–15 m, while fingerprints only matter under 5 m.

## Timeline

At 9 focused hours a week, new to 3D art. One person does everything, so phases run one after another.

| Phase | What | Hours | Weeks |
|---|---|---|---|
| 1 | Clock, puppet, key poses, debug window, blind playtest | 30–40 | 4 |
| — | **Gate:** keep pose holds only if a stepped variant passes the playtest (12.1) | | |
| 2 | References, maquette, style frame | 12–18 | 2 |
| 3 | Look-dev scene and clay material on the primitives | 22–32 | 3 |
| 4A | Face, voice-driven mouth, hands and feet on placeholders; readability test | 25–35 | 3 |
| 4B | Dents, get-up styles, knock-off cosmetics on placeholders | 60–85 | 8 |
| 5 | Learn Blender, five graded exercises | 30–45 | 4 |
| 6 | Make the body parts | 40–70 (15–30 fast track) | 6 (2–3) |
| 7 | Face pieces, hand poses, shoes, cosmetics | 24–36 | 3–4 |
| 8 | Assemble, fix gaps, match the style frame | 12–20 | 2 |
| 9 | Set lighting, anti-aliasing, settings menu, flash check | 18–26 | 2 |
| 10 | Optional: one continuous skinned body | 60–100 | 7–11 |

**Phases 1–9: about 275–405 hours, roughly 37 weeks.** With a phase 6 fast track (section 6.2): about
250–365 hours.

- **Phase 1 comes first** because it answers the riskiest question, whether stepped motion plays well, before
  any art.
- **The gate.** If no stepped variant passes, turn pose holds off and log it. Phases 2–9 still give the clay
  look; Harold Halibut shipped clay characters without stepped motion.
- **Phase 4B's three blocks are independent.** Any of them can wait until after phase 8.

---

# Part I — The look

## 1. Summary

The character becomes a plasticine puppet. Its 100 Hz physics doesn't change.

1. **A separate puppet.** A visual copy of the body (`Puppet`) copies the ragdoll's pose only on the beats of
   a shared clock, 12 a second, and holds still in between, like a puppet between camera exposures.
2. **Key poses.** Foot plants, grabs, stumbles, impacts and reversals force a beat at once and hold it one beat
   longer, so the held frames are the extremes.
3. **Readable at a distance.** An oversized, high-contrast face, mitten hands and shoes show gaze, grips, steps
   and who is talking at up to 15 m.
4. **Clay.** Thumb marks, fingerprints, an oily sheen over a matte base, boil on the parts that moved, dents
   from hard hits, staged get-ups and hats that fall off.
5. **Comfort.** Every stop-motion effect has its own setting, with an Off.

## 2. Principles

Check every choice against these. Change one only by logging why.

1. **Physics never steps.** The simulation and joint drives run every tick; only the drawn puppet holds.
2. **The camera never steps.** It follows the smooth physics at the full frame rate.
3. **Hold the poses that matter.** Plants, grabs, stumbles, impacts and reversals get a beat and an extra hold.
4. **Clay before judging steps.** Holds on smooth primitives look like lag. Judge the rate again once the clay
   material is on (phase 3).
5. **Imperfect, not random.** Chatter is a small pose error on parts that moved. The beat stays rhythmic,
   because rhythm sells slapstick.
6. **Clay remembers.** Squashes and dents last several beats. Boil shows only on parts that moved.
7. **Readable at 15 m.** Face, hands and feet are exaggerated, with at least 30 points of value contrast.
   Fingerprints fade out by 8 m.
8. **Visible voice.** You can tell who is talking at any distance you can hear them.
9. **Identity by hue.** Clothing hue and value tell players apart, even in greyscale and without a hat.
10. **Comedy stays clay.** Hits dent, get-ups look staged by an animator, hats are real physics props.
11. **Comfort is a setting.** Each effect can be turned down or off. Flicker never exceeds ±3% luminance.
12. **Your hand is in it.** Proportions, asymmetry, prints and palette come from your maquette, not asset
    packs.

## 3. What makes stop motion read

| Layer | What you see | Target here |
|---|---|---|
| Holds | The pose changes on beats and stays still between them. | 12 beats a second ("on twos", as in Morph and Gumby). Spider-Verse found holds of 3 frames or more (8 a second and below) choppy in fast action. |
| Chatter | Each moved part is slightly off each beat, where the animator's fingers touched it. | Up to 0.75° and 4 mm per beat, never adding up. Even sampling alone just reads as a low frame rate. |
| Key poses | Contacts and reversals are the held frames. | Guilty Gear Xrd's stepped 3D characters feel responsive at 60 fps because the holds land on contacts. |
| No blur | Every frame is a sharp photo. | No motion blur and no TAA, which blends beats together. |
| Surface | Thumb facets, prints, a waxy base, an oily sheen, dust. | A dial from heavy prints (Morph, Adam Elliot) to smooth (Aardman features). |
| Boil | The surface shifts only where the animator touched. | Boil scaled by each part's movement. A global timer looks like animated wallpaper. |
| Moving hold | An idle puppet still rocks slightly. | A small stepped breathing motion, so standing still doesn't look paused. |
| Flicker | Small exposure changes between frames. | ±1% luminance, Off by default, capped at ±3%. |
| Set scale | Close lamps, a warm key light, maybe shallow focus. | Shallow focus says tabletop miniature; deep focus says full-size set. Compared in phase 3. |

### Lessons from games

- **Harold Halibut** (Unity, scanned real puppets) tried 12 fps character motion and cut it: "Since camera
  movements still need to be very fluid, we felt the effect was more distracting than pleasing."
- **South of Midnight** (Unreal 4) shipped stepped characters at 60 fps, tuned per character and with a toggle,
  after stepping made some playtesters sick.
- **Spider-Verse** simulated cloth and hair on ones while characters moved on twos, because stepping a
  simulation directly makes it snap. This is an analogy: films have no live camera to fight.

### The main risk: judder at the centre of the screen

The camera moves smoothly. If the puppet holds its world position for 83 ms (one beat at 12 a second), the body
falls up to about 25 cm behind the camera's focus and snaps forward 12 times a second. That judder is the most
likely cause of motion sickness. Phase 1 tests two hold modes:

- **World hold** (`rootSmooth` off). Every part holds its world pose between beats. Most authentic, but it
  judders against the follow camera.
- **Root-smooth** (`rootSmooth` on). The pelvis follows the physics every frame; every other part holds its
  pose relative to the pelvis. The limbs still step, but the body doesn't judder against the camera.

---

# Part II — Design

## 4. The character today

[`Player.prefab`](../Assets/DrunkCharacter/Prefabs/Player.prefab) draws the character with 16 primitive meshes on
13 Rigidbodies. Every body is on layer 8, `PlayerBody`, and uses Rigidbody interpolation.

| Body | Mass, kg | Scale | Rotation | Drawn child (no Rigidbody) |
|---|---|---|---|---|
| Pelvis | 14 | (0.34, 0.20, 0.22) | none | |
| Torso | 13 | (0.40, 0.36, 0.24) | none | |
| Head | 4 | (0.36, 0.36, 0.36) | none | `Nose`, no collider. 0.198 m in front of the head centre and 0.018 m below it; world scale (0.0648, 0.0648, 0.108). |
| UpperArm_L, _R | 2 each | (0.10, 0.14, 0.10) | +90° Y (L), −90° Y (R) | |
| Forearm_L, _R | 1.5 each | (0.09, 0.15, 0.09) | none | `Hand_L`, `Hand_R`: a sphere collider on the forearm's Rigidbody, 0.18 m below the forearm centre; world scale 0.11. |
| Thigh_L, _R | 7 each | (0.18, 0.21, 0.18) | none | |
| Shin_L, _R | 3.5 each | (0.14, 0.20, 0.14) | none | |
| Foot_L, _R | 1.5 each | (0.16, 0.10, 0.30) | none | |

What the design relies on:

- **Flat hierarchy.** The bodies, `CameraTarget` and `Ghost` are direct children of `Player`. The root doesn't
  move after spawn.
- **Nothing reads the meshes.** No script uses `MeshRenderer` or `MeshFilter`, so the meshes can move to their
  own hierarchy.
- **Nothing under `Puppet` may have a Rigidbody or Collider.** `BalanceSensor.Awake` collects every Rigidbody
  under `Player` into `Bodies` (13), and `NetworkPose` drops any frame whose body count differs.
  `PlayerController` and `HandGrab` collect every Collider under `Player`. Create puppet objects as empty
  GameObjects and add the MeshFilter and MeshRenderer by hand; GameObject > 3D Object adds a collider.
- **Clients don't simulate.** `NetworkPlayer.OnNetworkSpawn` disables the simulation scripts on clients and
  makes every body kinematic, so `FootPlacement`, `HandGrab` and `PlayerController` events never fire there.
  `NetworkPose` places client bodies from `PoseFrame`s the host sends unreliably at 60 Hz, 142 bytes each.
- **Domain reload is off** when entering play mode, so static fields keep their values between runs.

## 5. Puppet and clock

### 5.1 How it fits together

1. Host sensors (`FootPlacement`, `HandGrab`, `PlayerController`, `ImpactSensor`) raise C# events.
2. `KeyPoseEvents`, on `Player`, turns them into key poses and counts each one in the `PoseFrame`.
3. Clients see the counters change and raise the same key poses.
4. On every key pose, `KeyPoseEvents` calls `StopMotionClock.RequestBeat(true)`.
5. On each beat, every `PuppetPart` copies its body's pose in `LateUpdate`.
6. The face, hands and feet swap meshes from key poses and from `VoiceLevel`.
7. The clock sets the global shader value `_StopMotionBeat`, which drives boil.

**Why a separate hierarchy.** It keeps the look fully apart from physics and networking, it supports
root-smooth, chatter, squash and mesh swaps, and culling and shadows work unchanged. Rejected: overriding the
body transforms around rendering (fragile with interpolation, colliders and shadow passes), and holding poses
on the GPU (breaks culling and attached objects).

### 5.2 Hierarchy

```
Player
├─ Pelvis, Torso, Head, …        13 physics bodies, colliders only
└─ Puppet                        Puppet
   ├─ P_Pelvis                   PuppetPart, scale 1
   │  └─ Mesh_Pelvis             MeshFilter + MeshRenderer, scale = baseScale
   ├─ P_Head
   │  ├─ Mesh_Head
   │  ├─ P_Nose                  PuppetPart (source = null), local (0, -0.018, 0.198)
   │  │  └─ Mesh_Nose
   │  ├─ Eyes, Mouth             phase 4A
   │  └─ Hat                     phase 4B
   ├─ P_Forearm_L
   │  ├─ Mesh_Forearm_L
   │  └─ P_Hand_L                PuppetPart (source = null), local (0, -0.18, 0)
   │     └─ Mesh_Hand_L
   └─ … one P_<Body> per body
```

- **One `P_<Body>` per physics body (13).** Each has a `PuppetPart` with `source` set to its body and stays at
  scale (1, 1, 1); it only sets position and rotation.
- **Its `Mesh_<Body>` child carries the scale.** `baseScale` is the body's `lossyScale` while the mesh is a
  Unity primitive, and (1, 1, 1) once it is a sculpt. Squash scales the mesh child, never `P_<Body>`. At rest
  the mesh child sits at local (0, 0, 0) with no rotation; the placeholder shoes in phase 4A are the one
  exception.
- **`P_Nose` and `P_Hand_L/R` are children of `P_Head` and `P_Forearm_L/R`, with a `PuppetPart` whose
  `source` is empty.** With no source, `PuppetPart` skips pose copying and chatter, so the nose and hands stay
  rigid to their parent and nothing separates at the wrist. It still runs the material, `_BoilMotion` (from its
  own world movement), squash and dents. Set their local position in metres: nose (0, −0.018, 0.198), hands
  (0, −0.18, 0). Don't copy the original child's `localPosition`: it is in the body's scaled space and would
  put the nose 55 cm in front of the face.
- **Face pieces and cosmetics go under `P_Head` or `P_Torso`,** not under a mesh child, so they keep their real
  size and don't shear when the part squashes.

### 5.3 `StopMotionClock`

One per scene.

| Feature | Behaviour |
|---|---|
| Rate | `beatsPerSecond` (12). The interval is 1 / `beatsPerSecond`. |
| Requested beat | `RequestBeat(holdExtra)` only sets a pending flag, so it is safe to call from `FixedUpdate`. On the next `Update`, if at least 1/24 s has passed since the last beat, beat now and schedule the next beat (1 + `extraHoldBeats`) intervals later when any request asked for the extra hold, or 1 interval later otherwise. |
| Timing variation | Multiply each interval by 1 + `Random.Range(-timingVariation, timingVariation)`. Default 0. |
| Hitches | At most one beat per rendered frame. Schedule from now (`next = Time.time + interval`) and never catch up. This also keeps benches that raise `Time.timeScale` working. |
| Holds off | Beat every frame. |
| Shader beat | On each beat, `Shader.SetGlobalFloat("_StopMotionBeat", Beat % 1200)`. 1200 divides by 2, 3, 4, 5, 6, 8, 10 and 12, so variant cycles never jump at the wrap. |
| Statics | `Instance`, `int Beat`, `event Action Beaten`, `static void RequestBeat(bool holdExtra)`. Reset them in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method, because domain reload is off. |

### 5.4 `PuppetPart` and `Puppet`

`PuppetPart` fields: `source` (its body, or null on `P_Nose` and `P_Hand_L/R`), `pelvis` (the Pelvis body, for
root-smooth), `mesh` (its mesh child) and `baseScale`.

| Feature | Behaviour |
|---|---|
| World hold | If `source != null`, on a beat `transform.SetPositionAndRotation(source.position, source.rotation)`. Read the Transform, which is interpolated, never `Rigidbody.position`. When `source == null`, leave `transform` untouched so it follows its parent `P_<Body>`. |
| Root-smooth | `P_Pelvis` copies the Pelvis every frame. Every other part with `source != null`, on a beat, stores its pose relative to the Pelvis in metres: `relPos = Quaternion.Inverse(pelvis.rotation) * (source.position - pelvis.position)` and `relRot = Quaternion.Inverse(pelvis.rotation) * source.rotation`. Every frame it applies them to `P_Pelvis`: position `P_Pelvis.position + P_Pelvis.rotation * relPos`, rotation `P_Pelvis.rotation * relRot`. Don't use `pelvis.InverseTransformPoint`: the Pelvis is scaled, so it isn't in metres. |
| Moved | A part moved if its world position changed more than 1 mm or its world rotation more than 0.5° since its last beat. |
| Chatter | On a beat, if `source != null` and the part moved, add a random rotation of up to `chatterDegrees` about a random axis and a random offset of up to `chatterMetres`. Each beat replaces the last one's chatter; it never adds up. |
| Moving hold | The character is idle when the pelvis has moved slower than 0.05 m/s for 0.5 s. On each idle beat, pitch `P_Torso` and `P_Head` by `idleAmplitude × sin(2π × 0.25 × Time.time)` degrees about the pelvis's right axis. Clear it on the first beat that isn't idle. |
| Squash | Scale the mesh child to `baseScale × (1.08, 0.85, 1.08)` for `squashBeats` beats, then restore it on a beat. On feet, also lower the mesh child by 15% of the distance from its origin to its sole (7.5 mm on the bare foot box), so the sole stays on the floor. |
| Spawn | After `OnEnable`, copy every frame until the first beat (when `source != null`). The network moves the character after `OnEnable`, and clients wait for their first frame. |
| Snap | When `KeyPoseEvents.Snapped` fires, copy at once instead of waiting for a beat. |
| Material | In `Awake`, take `renderer.material` for its mesh child and set `_PartSeed` and `_BaseScale` (= `baseScale`). In `OnDestroy`, `Destroy` that material. Don't use a MaterialPropertyBlock; it turns off the SRP Batcher. |

`Puppet` holds `List<PuppetPart> parts` (13) and `PuppetPart leftHand, rightHand, nose`. In `Awake` it fills
`parts` by taking each body in `BalanceSensor.Bodies` and finding the child named `P_<body name>`, so `parts[i]`
belongs to `Bodies[i]`, and caches `P_Hand_L`, `P_Hand_R` and `P_Nose`.

### 5.5 Key poses and networking

Create `KeyPoseEvents.cs` on `Player`. Visual scripts on host and clients listen only to it.

```csharp
public enum KeyPose : byte
{
    LandL, LandR, GripL, GripR, StumbleStarted, Impact, Reversal, RecoverStart, RecoverStand
}

public enum Expression : byte { Neutral, Grimace, Shock }

// On KeyPoseEvents:
public event Action<KeyPose> Raised;   // on host and clients
public event Action Snapped;           // spawn reset, StandAt, or a client seeing the reset count change
public byte HandFlags { get; }         // bit 0 left reach, 1 left hold, 2 right reach, 3 right hold
public Expression CurrentExpression { get; }
```

**Where the host's key poses come from**

| Key pose | Source | What to add |
|---|---|---|
| `LandL`, `LandR` | `FootPlacement.AdvanceSwing`, where it sets `LastLandTime` | `public event Action Landed;`, raised there. |
| `GripL`, `GripR` | `HandGrab.TryGrip`, after it sets `holdsBody` | `public event Action Gripped;`, raised there. It also fires when the hand grips the world. |
| `StumbleStarted` | `PlayerController.UpdateFallMeter` | `public event Action StumbleStarted;`, raised when `charge > 0` this tick and was 0 last tick. Keep last tick's charge in a field and zero it in `ClearFallMeter`. |
| `Impact` | New `ImpactSensor` on the bodies | While `IsFallen`, the first `OnCollisionEnter` with something else at an approach speed above 1 m/s (section 8.1) raises it, once per fall. `Fell` can't be used: it fires while the character is still upright. |
| `Reversal` | `KeyPoseEvents.FixedUpdate`, host only | Take the pelvis's horizontal velocity `v`. When `v.magnitude > 0.3` m/s: if `lastDir` is set, `Vector3.Dot(v, lastDir) < 0` and 0.3 s have passed since the last reversal, raise it. Then set `lastDir = v.normalized`. |
| `RecoverStart`, `RecoverStand` | Phase 4B | Section 8.2. |

**Host state, every tick**

- Reach bit: `CurrentInput.GrabLeft` (or `GrabRight`) and that hand's `HandGrab.IsHolding` is false.
- Hold bit: that hand's `IsHolding`.
- Expression: `Shock` while `IsFallen` and the pelvis moves faster than 1 m/s; `Grimace` while
  `FallMeterFraction > 0.3`; otherwise `Neutral`.

**To clients through `PoseFrame`**

- Add nine byte counters (one per key pose), `HandFlags` and `Expression`. The frame grows from 142 to 153
  bytes.
- **Host.** `KeyPoseEvents` adds 1 to a key pose's counter when it fires. `NetworkPose.SendPose` copies the
  counters, flags and expression into the frame.
- **Client,** in `NetworkPose.Update`, using the frame it is about to show:
  - On the first frame, or when the reset count changes, copy its counters as the baseline and raise
    nothing. A client that joins late then doesn't replay old events.
  - Otherwise, when the tick changes, compare each counter with the baseline. For each that changed, call
    `KeyPoseEvents.RaiseFromNetwork(pose)` and update the baseline. Because they are counters, a dropped
    packet doesn't lose the event.
  - Read `HandFlags` and `Expression` from the same frame.
- **Snap.** A client raises `Snapped` where `NetworkPose` already snaps across a reset
  (`from.ResetCount != to.ResetCount`). The host raises it on `CharacterReset`.

## 6. Body construction

### 6.1 Options

- **A. Segmented puppet (chosen).** One clay mesh per part (16), with rounded ball ends that overlap at the
  joints. Real puppets are built this way, with sleeves and collars over the joints. It maps one-to-one onto
  the ragdoll and needs no rigging, weights or UVs. Weak spot: a joint ball can show at extreme angles.
- **B. One skinned body (optional phase 10).** One mesh on a 13-bone armature. Seamless, but it needs
  retopology, UVs, baking and weight painting, and it stretches when a grab or a fall pulls the joints apart.
- **C. Volumetric blobs (MudBun, Clayxels).** Rejected: heavy on the GPU, hard to control when a hand grabs
  something, and the look belongs to the plugin.

### 6.2 Three ways to make the parts (phase 6)

| Route | How | Good | Watch for | Hours |
|---|---|---|---|---|
| 1. Freehand sculpt (baseline) | Duplicate each guide, voxel remesh, sculpt with Clay Strips and Clay Thumb against the maquette photos, decimate. | Teaches sculpting. | Slow for a beginner. | 40–70 |
| 2. Scan the maquette | Take 50–80 overlapping phone photos of each maquette piece (dust glossy clay with cornstarch). Reconstruct it in KIRI Engine or Polycam, then align it to its guide in Blender, remesh and decimate. | Uses your real clay. | Tiny pieces (eyes, mouths) are easier to sculpt. Scans need cleaning. Meshroom needs an NVIDIA GPU, so it won't run on this machine. | 15–25 |
| 3. Modifier stack | Block out with primitives or metaballs, then stack Remesh (voxel 0.004 m) → Displace (Clouds or Voronoi texture) → Decimate. Add a few Clay Thumb marks after applying. | Works with a mouse, stays editable, gaps are easy to fix. | Even lumps look procedural; mark the Head and hands by hand. | 15–30 |

**Recommended:** after phase 5, try route 3 or route 2 on the Torso and one forearm. Keep freehand sculpting
for the final marks.

## 7. Readability

Sizes are in game metres on the 0.36 m head. Pixel sizes are at 15 m and 1080p, where 1 px is about 16 mm.

### 7.1 Eyes

| Piece | Size and value | At 15 m | Behaviour |
|---|---|---|---|
| Whites | Two 8 cm spheres, centres 10 cm apart, on the upper front of the head. Value at least 90%. | 5 px | Fixed. |
| Pupils | 3.5 cm black discs. Value at most 10%. | 2 px | They lead the head: each beat, yaw = `Mathf.Clamp(headYawRate × 0.2f, -25f, 25f)`, with the rate in degrees per second, measured locally from `Head`. No network cost. |
| Lids | Open, Half and Closed meshes per eye, pivot at the eye centre. | 5 px | Blink every 2–6 s at random: Half, Closed, Closed, Half, Open, one per beat. Rest at Half when `NetworkPlayer.DrunkAmount` is at least 0.6, otherwise Open. |

`EyeDriver` runs the pupils and lids.

### 7.2 Mouth

One object under `P_Head`. Its `MeshFilter.sharedMesh` swaps between five pieces. Each piece has a raised lip
rim, so the join reads as a piece pressed onto the head. The inside has a value of at most 10% against skin of
at least 60%.

| Shape | Opening | Used when | At 15 m |
|---|---|---|---|
| Closed | 14 cm crease, 1.5 cm deep | silent and `Neutral` | 1 px |
| Mid | 16 × 8 cm | voice 0.15–0.55 | 5 px |
| Wide | 18 × 12 cm | voice above 0.55 | 7 px |
| Grimace | 18 × 5 cm, with a bright band of teeth | `Grimace` | 3 px |
| Shock | 9 × 11 cm upright oval | `Shock` | 7 px |

There is no small talking mouth: a 4 cm opening is under 2.5 px at 15 m.

**Voice**

- **`VoiceLevel`,** on `Player`, has `public float Level`, 0–1.
  - Phase 4A: `DebugVoice` makes a 5 Hz syllable wave (peaks 0.4–1.0) on one player. F8 cycles which player
    talks, in `OwnerClientId` order, then nobody.
  - Later: each client measures how loud each player's voice plays back, locally. No network traffic.
- **`MouthDriver`,** on the mouth:
  - Every frame, smooth `Level` into an envelope: attack 0.03 s, release 0.12 s.
  - On each beat, pick the shape: envelope below 0.15 shows the expression (`Neutral` is Closed); 0.15–0.55
    is Mid; above 0.55 is Wide.
  - Keep each shape for at least 1/12 s, even with pose holds on Light or Off, so it doesn't flutter.

### 7.3 Hands

- Mitten gloves with a thumb, 20 cm across (1.8 times the 11 cm sphere), centred on the hand.
- Value at least 30 points (on 0–100) away from both the body clay and the floor.
- Three poses, swapped on beats from `HandFlags`: **Relaxed**; **Reach**, an open hand while grab is held and
  nothing is held; **Grip**, a closed fist while `IsHolding`.
- On `GripL` or `GripR`, squash that hand's mesh for `squashBeats`.

### 7.4 Shoes

The foot colliders are tuned for the stairs. Shoes never change them.

| | Collider | Shoe, at most | Rule |
|---|---|---|---|
| Sole | 5 cm below the foot centre | flush | Never below the collider. |
| Width | 0.16 m | 0.22 m | 3 cm wider on each side. |
| Height | 0.10 m | 0.16 m | Upward only. |
| Length | 0.30 m | 0.36 m | 3 cm past the toe and heel. The toe curls up 2 cm so it doesn't clip stair risers. |

- Value at least 30 points away from the floor.
- On `LandL` or `LandR`, squash that foot's mesh for `squashBeats`, lowered so the sole stays down (5.4).

### 7.5 Player identity

- Four clothing hues at least 60° apart, set per player through material properties.
- **Greyscale check:** with Color Adjustments Saturation at −100, all four still differ from each other and
  from the floor and walls.
- `hueIndex = (int)(OwnerClientId % 4)`. No new network state.
- Identity lives on the clothing, never only on a hat, because hats fall off.

## 8. Physical comedy

### 8.1 Dents

A hard hit leaves a dent where it landed. It holds for 6 s, or until the character gets up, then smooths out
over 3 beats.

**Detecting it (host).** `ImpactSensor` goes on all 13 bodies in phase 4B. In `OnCollisionEnter`, against a
collider that isn't part of this `Player`:

```csharp
ContactPoint contact = collision.GetContact(0);
float approachSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
```

- **Use approach speed, not `collision.impulse` divided by mass.** On a jointed ragdoll the impulse includes
  joint and support forces, about 4 m/s on a standing foot.
- **Feet** dent only while `IsFallen`, or when another Rigidbody hits them. Stepping off the 0.5 m drop lands
  at about 3.1 m/s and would otherwise dent every step down.
- **Which mesh.** If `contact.thisCollider` is a `Hand_L` or `Hand_R` sphere, dent `Puppet.leftHand` or
  `Puppet.rightHand` and use the hand collider's Transform as `origin`. Otherwise dent the body's own
  `PuppetPart` and use the body's Transform.
- **Size.** Dent at 2.5 m/s or more. Depth is 1 cm at 2.5 m/s, rising linearly to 6 cm at 7 m/s, then clamped.
  Radius = `Mathf.Clamp(2.5f * depth, 0.06f, half the part's smallest size)`.
- **Where,** in metres along the origin's axes:

  ```csharp
  Vector3 centre  = Quaternion.Inverse(origin.rotation) * (contact.point - origin.position);
  Vector3 pushDir = Quaternion.Inverse(origin.rotation) * -contact.normal;
  ```

  The numbers mean the same on a scaled primitive and on a sculpt.
- **Flatten** at 5 m/s or more: as well as the dent, squash the hit `PuppetPart` for 3 beats, 0.75 along its
  local axis closest to `contact.normal` and 1.15 along the other two.

**Network.** Put a reliable `[Rpc(SendTo.NotServer)] void DentRpc(byte body, bool hand, Vector3 centre,
Vector3 pushDir, byte depthMm, byte radiusMm, bool flatten)` on `NetworkPose`. `body` indexes `Puppet.parts`,
which matches `BalanceSensor.Bodies`; when `hand` is true, route it to `Puppet.leftHand` or `Puppet.rightHand`.

**Timing.** Apply the dent on the next beat. Hold it 6 s, or until `RecoverStand`, then step it down on beats:
two thirds, one third, gone.

**Shader: the `SG_Dent` sub-graph.** Each material has two slots; a new dent replaces the older one.

- `_DentA` = (centre, depth) and `_DentADir` = (pushDir, radius); the same for `_DentB`. Subtract the mesh
  child's rest local position from `centre` before writing it (zero except on the placeholder shoes).
- Work in "part metres": object space multiplied by `_BaseScale`. That is how the dent was measured, it holds
  for primitives and sculpts alike, and it needs no tangents. Sculpts have no UVs, so they have no usable
  tangents.

1. `P = Position (Object) × _BaseScale` and `N = normalize(Normal (Object) / _BaseScale)`.
2. `Displace(p)`, with `d = length(p − C)`:
   - push in: `p += D × depth × (1 − saturate(d / radius))²`;
   - raised rim: `s = saturate(d / (1.4 × radius))`, then `p += N × depth × 0.3 × 4s(1 − s)`.
3. Recompute the normal from two nearby points:

   ```hlsl
   float3 helper = abs(N.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
   float3 T = normalize(cross(helper, N));
   float3 B = cross(N, T);
   float3 P0 = Displace(P);
   float3 N2 = normalize(cross(Displace(P + T * 0.005) - P0, Displace(P + B * 0.005) - P0));
   ```
4. Add the boil displacement (9.4) in part metres before converting back to object space:
   `P_final = (P0 + N2 × boilOffsetMetres) / _BaseScale`, and output Vertex Normal `normalize(N2 × _BaseScale)`.
   Adding boil in part metres means a 5 mm boil is 5 mm on both scaled primitives and unit-scale sculpts.

Dents need vertices 1–2 cm apart. Sculpts have that; primitives don't (section 11, phase 4B.1).

### 8.2 Getting up

Today a fallen character stays down until R puts it back at spawn. Phase 4B adds getting up where it lies, in
three styles you can switch live and compare.

**Input.** Add `public bool GetUp;` to `CharacterInput` and its `NetworkSerialize`. In `LocalPlayerInput`, bind
it to Space and the gamepad's south button.

**When.** The host starts a recovery when the owner presses GetUp at least 1.5 s (`minDownSeconds`) after
`Fell`, or by itself 8 s (`autoRecoverSeconds`) after the later of `Fell` and the last time another player held
this character.

**Never while held.** `IsHeld()` is true when any `HandGrab`'s `HeldBody` is one of this character's `Bodies`.
Check it before starting and every `FixedUpdate` during a recovery. If the character is grabbed, or
`CharacterReset` fires, cancel: destroy any style A joint and stay down (or reset).

**New methods**

- **`FootPlacement.PlantAt(Vector3 origin, Quaternion rotation)`.** Move the body of `PlantAtSpawn` into it,
  placing the foot at `spawnPos = origin + rotation * spawnLocalPosition` and `spawnRot = rotation *
  spawnLocalRotation`. `PlantAtSpawn` then calls it with the rig root's position and rotation. In `PlantAt`,
  find the ground under the foot with `Ground.At(spawnPos, spawnPos.y - FootGroundOffset, out Vector3 planted,
  out Vector3 normal)`, searching around the sole as `PlantUnderAnkle` does. When it hits, set
  `groundPoint = planted` and `groundNormal = normal`, and place the foot at `planted + Vector3.up *
  FootGroundOffset`, so a foot on a stair lands on its own step. When it misses, keep today's behaviour.
- **`PlayerController.StandAt(Vector3 pelvisPoint, float yaw)`,** used by styles B and C and by style A's
  timeout:
  1. `rot = Quaternion.Euler(0, yaw, 0)`. Pass the owner's heading (`CurrentInput.HeadingYaw`). Don't set
     `headingYaw`: `FixedUpdate` already copies it from `CurrentInput` every tick, even while fallen.
  2. `origin = pelvisPoint - (rot * spawnLocalPositions[pelvisIndex]).Flat()`. Find `pelvisIndex` and
     `footIndex` in `Bodies` once, in `Awake`.
  3. `groundY = Ground.HeightAt(pelvisPoint, pelvis.position.y)` and
     `origin.y = groundY - (spawnLocalPositions[footIndex].y - 0.05f)`. The foot centre sits 5 cm above its
     sole.
  4. `Physics.OverlapCapsule` over the standing pelvis-to-head space, ignoring the character's own colliders.
     If anything is in the way, give up, log it and stay down.
  5. Move each body to `origin + rot * spawnLocalPositions[i]` with rotation `rot * spawnLocalRotations[i]`,
     and zero its velocities, as `ResetCharacter` does.
  6. Call `PlantAt(origin, rot)` on both feet; clear the fall meter, momentum and sway; set `IsFallen = false`;
     invoke `CharacterReset` (this clears `NetworkPlayer.IsFallen` and bumps the reset count so clients snap);
     raise `RecoverStand`. Unlike `ResetCharacter`, keep `CurrentInput`.
- **`PlayerController.Stand()`,** used by style A after the set-down. No teleport:
  1. On both feet call a new `FootPlacement.PlantUnderAnkle()`, which sets `groundPoint` and `groundNormal` from
     `Ground.At(RealAnkle, RealSoleHeight, …)`, right under the real foot, so the legs don't snap.
  2. Clear the fall meter, momentum and sway; set `IsFallen = false`; raise `RecoverStand`.
  3. `Stand` doesn't invoke `CharacterReset`, so `NetworkPlayer` must also clear its fallen flag on
     `RecoverStand`. In the server block of `OnNetworkSpawn`, add `playerController.RecoverStand +=
     OnCharacterReset;`, and remove it in `OnNetworkDespawn`.

**When `RecoverStand` fires,** every machine calls `Puppet.BoostChatter(3f, 4)` (3 times the chatter for 4
beats) and repairs all dents. The host puts back any loose cosmetics (8.3).

**The styles**

| Style | What you see | How it works | Effort |
|---|---|---|---|
| C. Jump cut (baseline) | The fallen pose holds 3 beats, then pops upright with 4 beats of heavy chatter. | The host calls `StandAt` 3 beats after `RecoverStart`. | 2–3 h |
| A. Invisible hand | Lifted by the upper back, dangles, turns upright, is set down, wobbles free. Variant: a giant clay hand above. | The host adds a `ConfigurableJoint` from the Torso to the world. Raise `connectedAnchor` over 0.8 s, hold 0.4 s, lower at 0.6 m/s until both soles are within 2 cm of the ground, then call `Stand()` and destroy the joint. After 3 s, give up and call `StandAt`. | 8–12 h |
| B. Re-sculpt | Squashes into a ball of clay over 4 beats, then re-forms standing over 6. | Visual only, on each machine: the parts move and shrink into a lump at the pelvis. The host calls `StandAt` after 4 beats; the parts grow back out over 6. | 6–10 h |
| D. Scramble up | Rolls onto its knees and pushes itself up. | A multi-stage controller. Skip it unless A, B and C all fail. | 20+ h |

**Style A's joint:** X, Y and Z drives with `positionSpring` 4000 and `positionDamper` 1000; Rotation Drive
Mode Slerp with spring 800 and damper 80. Three traps:

1. A `JointDrive` made in code has `maximumForce` 0, which applies no force. Set `maximumForce =
   float.MaxValue`.
2. Set `autoConfigureConnectedAnchor = false` before setting `anchor` to (0, 0.5, 0), the top of the Torso box
   in its local space. With no connected body, `connectedAnchor` is a world position; move it each
   `FixedUpdate`.
3. A runtime `ConfigurableJoint` measures `targetRotation` relative to the body's rotation when the joint was
   created (`startRot = torso.rotation`). Without setting `targetRotation`, the Slerp drive holds the torso in
   its fallen horizontal pose. Set `desiredRot = Quaternion.Euler(0, CurrentInput.HeadingYaw, 0) *
   spawnLocalRotations[torsoIndex]` and `joint.targetRotation = Quaternion.Inverse(desiredRot) * startRot`.

### 8.3 Cosmetics

Slots: Head, Face, Neck, Back, Hands/Feet. Head and Face items (hats, glasses, moustaches) can be knocked off.
They become real networked props that anyone can grab and throw.

- **Worn.** A child of `P_Head` or `P_Torso`, with no collider and no Rigidbody. Hidden while its bit is set in
  `NetworkVariable<byte> detachedSlots` on `NetworkPlayer`.
- **When it comes off (host).** First, log the Head's peak angular speed and peak 3-tick acceleration,
  `(v_now − v_3_ticks_ago) / 0.03 s`, over 5 minutes of drunk walking (`drunkAmount` 1) without falling. Set
  each threshold to 1.5 times its peak. Until then: angular speed above 12 rad/s, acceleration above 60 m/s²,
  a head impact of 3 m/s or more, upside down past 110° for 0.2 s, or a grabbed head pulled faster than 3 m/s.
- **The loose item (`loosePrefab`).** The host spawns it at the scene root, never under `Player`, where
  `BalanceSensor` would count its Rigidbody. Put it on the `PlayerBody` layer: `Ground` ignores that layer, so
  nobody steps on a hat as if it were floor, and `HandGrab` can still grab it. Components: `NetworkObject`, a
  0.2–0.5 kg Rigidbody, a convex collider, server-authoritative `NetworkTransform` and `NetworkRigidbody`,
  `LooseCosmetic`, and a visual child with a world-hold `PuppetPart`. For 0.5 s it ignores collisions with its
  owner; launch it at `head.GetPointVelocity(point) + Vector3.up * 1f`.
- **Back on.** When the owner's Head or a hand touches it after 1 s, on `RecoverStand`, or after 60 s on the
  ground, the host despawns it and clears the bit.
- **Soft bits** (scarf ends, bobbles): a 3-segment spring chain (spring 60, damping 6), simulated every frame on
  each machine and drawn only on beats.

## 9. The clay material (`SG_Clay`)

One Shader Graph for every part, replacement piece, cosmetic and clay prop.

### 9.1 Light

Real plasticine is a matte, slightly waxy base with an oily sheen where fingers pressed it (Rowbotham's
report). URP Lit's Clear Coat gives that two-layer look.

- **Graph Settings:** Universal, Material Lit, Clear Coat ✓ (adds the Coat Mask and Coat Smoothness inputs),
  Fragment Normal Space Object. Object space needs no tangents, and sculpts have no UVs.
- **Values:** Smoothness 0.18; Coat Mask = fingerprint mask × 0.35; Coat Smoothness 0.65.
- **Shadow edge.** Lit's light-to-shadow edge is harder than real clay's. Soften it with the fill light and
  ambient light, not in the shader: anything added through Emission ignores the scene's lights and shadows.
- **Not the Custom Lighting sample.** Its Oren-Nayar model lights only from the main light, while look-dev and
  the set use spot lights, and every sample graph uses the Unlit target, which has no clear coat.

### 9.2 Colour

The vertex colour attribute `Col` holds variation, not hue:

| Channel | Meaning |
|---|---|
| R | Value streaks; 0.5 is neutral. |
| G | Grime and dark mouth insides; 1 is darkest. |
| B | Accent mask: 0 is the main colour, 1 the accent colour. |

- The hues (skin, and the player hue on clothing) are material properties.
- **Clothing edges** (cuffs, collars, waistbands) are separate, overlapping clay, or a second material slot
  (`M_Clay_Cloth`). Decimated vertices are 1–2 cm apart, so a painted edge would zigzag.
- **Specks, lint and dust** come from a tiling triplanar mask, `T_Specks_M`.
- Vertex colour is exported as sRGB. In the graph, pass Vertex Color through Colorspace Conversion (RGB to
  Linear).

### 9.3 Fingerprints

- **Puppet scale.** Prints are sized as on the 20 cm maquette, so they appear 9.25 times larger on the
  character. A 1.5 cm thumb smear becomes 14 cm, 26 px at 5 m. A texture of a 10 cm slab then covers 0.925 m,
  so set the Triplanar Tile to 1.08.
- **Triplanar node:** Type Normal, Input Space Object, Normal Output Space Object, Normal input wired to
  `normalize(Normal (Object) / _BaseScale)` so non-uniform primitive scales don't skew the projection weights.
- **Position input:** Position (Object) × `_BaseScale`. Don't use the Object node's Scale: it changes during a
  squash and the prints would slide.
- **Strength and distance fade:** `strength = _PrintNormalStrength × fade`, where `fade` runs from 1 at 3 m to
  0 at 8 m. Fade the Coat Mask by the same `fade`. Keep mipmaps on and Aniso Level at 4.
- **Blend in object space:** `normalize(lerp(Normal (Object), normalize(triplanarNormal × _BaseScale),
  strength))`. Don't use the Normal Strength node: it expects a tangent-space normal and returns (0, 0, 1) at
  strength 0, which in object space points every surface along +Z.
- **Textures:** start with a stock print normal map (cgbookcase, as used by joebinns/clay). Making your own is
  step 3.4.

### 9.4 Boil

- **`_StopMotionBeat`:** a Float in the Blackboard with Exposed unticked and Scope Global. An exposed property
  ignores `Shader.SetGlobalFloat`.
- **Three variants** of the print and noise textures: `variant = (_StopMotionBeat + floor(_PartSeed × 3)) mod 3`.
  Swap them; never scroll them.
- **`_BoilMotion`,** per part: on each beat, `PuppetPart` sets it to `Mathf.Clamp01(metres moved since the last
  beat / 0.05f)`. Only call `SetFloat` when it changes by more than 0.01.
- Multiply the variant blend and a 5 mm displacement along the part-metres normal (from a 3D noise sampled at
  `Position (Object) × _BaseScale`) by `_BoilMotion × _BoilStrength`, then divide by `_BaseScale` (as in 8.1
  step 4). Parts that didn't move stay still.
- Unity's Cube primitive (Pelvis, Torso, feet and nose until phase 6) has split normals that crack open when
  displaced. Keep `_DisplacementStrength` at 0 on cubes.

### 9.5 Triangle budget

| Part | Triangles, at most |
|---|---|
| Head | 8,000 |
| Torso | 4,000 |
| Pelvis | 2,500 |
| Each arm and leg segment (8) | 2,500 (20,000) |
| Each hand pose on show (2) | 2,000 (4,000) |
| Each shoe (2) | 1,500 (3,000) |
| Nose and face pieces | 5,500 |
| Worn cosmetics | 1,500 |
| **Per player** | **about 48,500, in about 25 renderers** |

## 10. Comfort settings

Stepping and flicker at 12 Hz sit in the 3–30 Hz band most likely to cause motion sickness or photosensitive
reactions. Every effect has its own setting: in the F9 debug window from phase 1, and in a settings menu from
phase 9.

| Setting | Options | Default |
|---|---|---|
| Pose holds | Off (every frame) / Light (18 beats a second, no extra hold, no chatter) / Full | Full, unless the playtest says otherwise |
| Beats per second | 8 / 12 / 18 / 24 (with Full) | 12 |
| Root-smooth | On / Off | Set by the playtest |
| Chatter | On / Off | On |
| Boil | On / Off | On |
| Flicker | Off / On (±1% luminance) | Off. Capped at ±3% in code; the flash threshold in WCAG 2.3.1 and Xbox guideline 117 is 10%. |
| Film grain | On / Off | On (Thin1, 0.15) |
| Tabletop focus | On / Off | Set in phase 3 |

## 11. World, camera and post-processing

- **No TAA or STP.** They blend frames and ghost every beat. In `PC_RPAsset`, set MSAA to 4× (it is off now), or
  use SMAA on the Main Camera.
- **No motion blur** in any volume profile.
- **Separate volume profiles.** `PC_RPAsset`'s default profile and `TestScene`'s Global Volume both use
  `SampleSceneProfile.asset`. Make `LookDevProfile` and `TestSceneProfile` rather than editing it.
- **Light it like a set.** Spot lights 3–5 m from the play area with Range at most 15 m, so the far side falls
  off by at least a stop. A 3200 K key light with soft shadows. Visible practical lamps.
- **Props.** Grabbable props get a world-hold `PuppetPart` child on the shared clock, so a held prop and the
  hand holding it step on the same frame.
- **Primitives are coarse.** The head sphere has about 5 cm between vertices and the cubes have only their
  corners. Dents and boil displacement look right only on sculpts.

---

# Part III — Build plan

## Phase 1 — Stop-motion prototype and playtest (30–40 h, 4 weeks)

Branch: `feature/stop-motion-prototype`. Build the clock, puppet and key poses on the current primitives, then
run a blind playtest.

### 1.1 `StopMotionClock` (3–4 h)

- Create `Assets/DrunkCharacter/Scripts/StopMotionClock.cs` as in 5.3. Put it on a new `StopMotionClock`
  GameObject in `TestScene`.
- Serialized fields, with the section 13 defaults: `holdsOn`, `beatsPerSecond`, `extraHoldBeats`,
  `timingVariation`, `rootSmooth`, `chatterDegrees`, `chatterMetres`, `idleAmplitude`, `squashBeats`.

**Done when** a log in `Beaten` shows 12 beats a second, and `RequestBeat(true)` from a test key gives an
immediate beat followed by a two-interval hold (about 0.167 s).

### 1.2 Host signals (4–6 h)

- Add `Landed`, `Gripped` and `StumbleStarted` as in 5.5.
- Create `ImpactSensor.cs`. Put it on Pelvis, Torso and Head for now; all 13 bodies get it in phase 4B.

**Done when**, as host, each event logs once per occurrence: walk, grab a cube, get shoved, fall.

### 1.3 `KeyPoseEvents` (3–4 h)

- Create `KeyPoseEvents.cs` on `Player` as in 5.5: listen to the host signals, detect reversals, set
  `HandFlags` and `CurrentExpression`, raise `Snapped` on `CharacterReset`, and call `RequestBeat(true)` on
  every key pose.

**Done when** the log shows each key pose with the right side, and `Reversal` when you strafe left, then right.

### 1.4 Network fields (3–4 h)

- Add the counters, `HandFlags` and `Expression` to `PoseFrame`. Fill them in `SendPose`; read them on clients
  in `NetworkPose.Update` (5.5).

**Done when**, in Multiplayer Play Mode, host and client log the same key poses for the same character, and a
client that joins logs no burst.

### 1.5 `Puppet` and `PuppetPart` (4–5 h)

- Create `Puppet.cs` and `PuppetPart.cs` as in 5.2 and 5.4.

### 1.6 Build the puppet in `Player.prefab` (2–3 h)

Open `Player.prefab` in Prefab Mode.

1. Add an empty child of `Player` named `Puppet`: position (0, 0, 0), rotation 0, scale 1. Add the `Puppet`
   component.
2. For each of the 13 bodies:
   1. Add an empty child of `Puppet` named `P_<Body>`, scale 1, at the body's position and rotation. The puppet
      then stands in place in edit mode, which the phase 6 export needs.
   2. Add `PuppetPart` and set `source` to the body and `pelvis` to Pelvis.
   3. Add an empty child named `Mesh_<Body>`: position 0, rotation 0, scale = the body's scale (section 4 table).
      Copy the body's MeshFilter and MeshRenderer onto it, and set `mesh` and `baseScale` on the `PuppetPart`.
3. **Nose:** under `P_Head`, add an empty `P_Nose` at local position (0, −0.018, 0.198), rotation 0, scale 1.
   Add `PuppetPart` with `source` left empty (None). Under it add `Mesh_Nose` with scale (0.0648, 0.0648, 0.108)
   and the Nose's MeshFilter and MeshRenderer, and set `mesh` and `baseScale` on `P_Nose`'s `PuppetPart`.
4. **Hands:** under each `P_Forearm_L/R`, add an empty `P_Hand_L/R` at local position (0, −0.18, 0), scale 1.
   Add `PuppetPart` with `source` left empty (None). Under it add `Mesh_Hand_L/R` with scale (0.11, 0.11, 0.11)
   and the hand's MeshFilter and MeshRenderer, and set `mesh` and `baseScale` on `P_Hand_L/R`'s `PuppetPart`.
5. Remove the MeshFilter and MeshRenderer from the 16 originals: the 13 bodies, Nose and both hands. Leave their
   colliders and Rigidbodies alone.

**Done when:**
- [ ] in Prefab Mode, every puppet mesh sits exactly on its collider;
- [ ] in play mode with holds off, the character looks the same as on `main`;
- [ ] `BalanceSensor.Bodies.Length` is still 13, and nothing under `Puppet` has a Collider or Rigidbody.

### 1.7 Debug window (2–3 h)

- An IMGUI window, toggled with F9, with every section 10 setting live.
- Keys 1–4 switch the four playtest variants (12.1). The screen shows only "Variant A–D", never the settings.

### 1.8 Latency check (2 h)

- Run MPPM Player 2 as a client on `NetworkSession`'s Mobile4G preset (100 ms ±20 ms, 4% loss; F10 cycles the
  presets).

**Done when**, stepping frame by frame (Ctrl+Alt+P) while grabbing a prop and then another player, the hand and
the held body change pose on the same frame on host and client.

### 1.9 Logger and playtest (8–12 h)

- Create `Assets/Sandbox/Debug/PlaytestLogger.cs`. It logs `Fell`, `StumbleStarted`, recovered stumbles (a
  stumble whose fall meter drains to 0 without `Fell`), grab attempts, `Gripped`, and the Stairs15 start and
  finish times to `Logs/playtest_<player>_<variant>.csv`.
- Run the playtest in 12.1.

### Phase 1 done when

- [ ] all four variants switch live in play mode;
- [ ] `SwayBench.Run` on seeds 1–5 gives fall times within ±1 s of the same runs on `main`, so the physics is
      unchanged;
- [ ] the playtest result, the default rate and the root-smooth default are logged;
- [ ] the gate is passed, or holds are set to Off and that is logged.

## Phase 2 — Maquette and style frame (12–18 h, 2 weeks)

Settle the proportions, surface and lighting in real clay before any shader or Blender work.

### 2.1 References (2 h)

- Collect 30–50 images and clips in `ArtSource/References/{films,games,puppets,drunk_poses}`, at least 5 per
  folder. Cover heavy-print clay (Morph, Adam Elliot) through smooth clay (Aardman features, Harold Halibut).

### 2.2 Materials (1 h; order early)

- **Clay:** 1 kg Chavant NSP Medium (sulphur-free, holds detail) for the maquette, and 450 g Newplast or Van
  Aken Plastalina to get the feel of animation plasticine.
- **Armature and tools:** 1.5 mm and 2 mm aluminium armature wire, 5 m each; loop and ball-stylus tools; flat
  and cone silicone shapers; petroleum jelly; a microfibre cloth.

### 2.3 Print the character (1 h)

- In the Scene view, take orthographic front and side screenshots of the standing character. Print them so
  crown to sole measures exactly 20.0 cm.

### 2.4 Armature and maquette (6–8 h)

1. Mark the 12 joint centres on the print. Bend 2 mm wire for the spine and legs and 1.5 mm wire for the arms,
   within 2 mm of the marks.
2. Sculpt the maquette in separate, overlapping pieces (option A) at these sizes:

   | Feature | Game | Maquette |
   |---|---|---|
   | Head | 36 cm | 3.9 cm |
   | Eye whites | 8 cm | 9 mm |
   | Wide mouth | 18 × 12 cm | 19 × 13 mm |
   | Gloves | 20 cm across | 22 mm across |
   | Shoes, at most (W × H × L) | 22 × 16 × 36 cm | 24 × 17 × 39 mm |

3. Choose the clothing (for example a coat, sweater, scarf or baggy trousers). Log it.

### 2.5 Style frame and photos (2–3 h)

1. Build a small cardboard and fabric tabletop corner. Light the maquette with three desk lamps:
   - **Key:** 45° front-left and above, a warm bulb of about 3000 K.
   - **Fill:** opposite the key, at about half its brightness.
   - **Rim:** behind and above, to catch the clay's edges.
2. With the phone on a tripod at maquette chest height and manual exposure, shoot `ArtSource/StyleFrame.jpg`.
   Write the camera distance and height in `ArtSource/StyleFrame.txt`.
3. Shoot flat-lit front and side photos on plain paper with a ruler in frame: `ArtSource/Maquette/Front.jpg`
   and `Side.jpg`.

### Phase 2 done when

- [ ] the maquette's joint centres are within 2 mm of the print;
- [ ] `StyleFrame.jpg`, `StyleFrame.txt`, `Maquette/Front.jpg` and `Maquette/Side.jpg` exist;
- [ ] the clothing is logged.

## Phase 3 — Look-dev scene and clay material (22–32 h, 3 weeks)

The goal: the primitive character already reads as photographed plasticine.

### 3.1 Shader Graph basics (3–4 h)

- Work through Unity Learn's "Get started with Shader Graph".

### 3.2 Look-dev scene (2–3 h)

1. Create `Assets/Settings/LookDevProfile.asset`: Tonemapping Neutral, no Motion Blur.
2. Create `Assets/Sandbox/LookDev.unity` with that profile. Add:
   - the `Player` prefab on a mid-grey floor (RGB 128, 128, 128);
   - one Reflection Probe;
   - three spot lights (key, fill, rim) matching `StyleFrame.jpg`;
   - two cameras: gameplay at 5 m and close-up at 1.5 m.

### 3.3 `SG_Clay` (8–12 h)

Check each step in `LookDev` before the next.

1. Create `SG_Clay` (Universal > Lit) with the Graph Settings in 9.1.
2. **Base colour:** `_BaseColor × lerp(0.85, 1.15, R)` from the linear vertex colour, darkened toward a grime
   colour by G. Add the speck mask.
3. **Prints:** the Triplanar node set up as in 9.3, with the stock print normal map.
4. **Sheen:** Smoothness 0.18; Coat Mask = print mask × 0.35; Coat Smoothness 0.65. Compare with the coat off
   and log which you keep.
5. **Fade:** prints and coat fade out from 3 m to 8 m, blended in object space as in 9.3.
6. **Boil** (9.4): `_StopMotionBeat`, `_PartSeed`, `_BoilMotion` and three variants. `PuppetPart` sets
   `_PartSeed` and `_BaseScale` in `Awake`, sets `_BoilMotion` on beats, and destroys its material in
   `OnDestroy`.
7. **Boil displacement:** 5 mm × `_BoilMotion` along the part-metres normal, divided by `_BaseScale` (9.4), on
   spheres and capsules only. Keep it at 0 on cubes.

### 3.4 Your own prints, style sheet, rate and scale (6–9 h)

1. **Your own three print variants** (optional now, or in phase 6). Pick one way:
   - **Bake in Blender.** Add a plane and scale it to 0.1 m. Add a Multiresolution modifier and subdivide about
     6 times. In Sculpt Mode, turn on Symmetry > Tiling for X and Y with the tile offset at 0.1 m, then press
     thumb marks with Clay Thumb. In Render Properties (Cycles) > Bake, tick Bake from Multires, choose
     Normals and bake to a 1024 × 1024 image. Repeat for three variants.
   - **Photograph real prints.** Press prints across a flat 10 cm slab. With a tripod, manual exposure, and a
     ruler and grey card in frame, take four shots from directly above, lit from north, east, south and west.
     In Blender's Compositor, combine them: `Nx = east − west`, `Ny = north − south`,
     `Nz = sqrt(max(0, 1 − Nx² − Ny²))`; normalise, then map −1…1 to 0…1. Make it tile in GIMP
     (Filters > Map > Tile Seamless). A single raking-light photo bakes its light into the map, so use all four.
2. Record an 8-second, 360° turntable of a clay sphere in `LookDev` with Unity Recorder.
3. Write `Docs/stop-motion-style-sheet.md`: 6–8 palette swatches as hex, the smoothness and coat values, and the
   mark sizes in game centimetres.
4. With the clay on, compare 8, 12 and 18 beats a second. Compare tabletop focus (Bokeh, 50 mm, f/2.8) with deep
   focus. Log both choices.

### Phase 3 done when

- [ ] a `LookDev` gameplay-camera screenshot sits next to `StyleFrame.jpg`;
- [ ] 2 of 3 people who don't know the project, asked "What is this made of?", answer clay or plasticine
      unprompted;
- [ ] the lighting, rate and scale-look choices are logged.

## Phase 4A — Readability (25–35 h, 3 weeks)

Prove the character reads at 5–15 m with placeholders before sculpting anything.

1. **Face (3–4 h).** Under `P_Head`, add collider-free placeholders at the sizes and values in 7.1 and 7.2:
   eye whites, pupils, the three lids and the five mouths.
2. **Voice (5–7 h).** `VoiceLevel`, `DebugVoice` (F8) and `MouthDriver` (7.2).
3. **Eyes (3–4 h).** `EyeDriver` (7.1).
4. **Gloves (3–4 h).** 20 cm high-contrast placeholder gloves on `Mesh_Hand_L/R`. `HandPoseDriver` swaps
   Relaxed, Reach and Grip on beats from `HandFlags`.
5. **Shoes (2–3 h).** Set each `Mesh_Foot_L/R` to the shoe size within 7.4: scale (0.22, 0.16, 0.36) at local
   position (0, 0.03, 0), so the extra height goes up and the sole stays at the collider's. Set `baseScale` to
   match. Walk Stairs15 and Stairs20: no shoe may clip more than 2 cm into a step.
6. **Squash (2 h).** A 2-beat squash on the foot mesh on `LandL/R` and on the hand mesh on `GripL/R` (5.4).
7. **Hues (2–3 h).** The four player hues and the greyscale check (7.5).
8. **Readability test (4–6 h).** Run 12.2.

### Phase 4A done when

- [ ] the readability test passes;
- [ ] the furthest distance with at least 80% talker accuracy is logged as the proximity-voice range;
- [ ] the stair check passes.

## Phase 4B — Dents, getting up, cosmetics (60–85 h, 7–9 weeks)

Three independent blocks on placeholders. Each can be paused after its checklist.

### 4B.1 Dents (12–16 h)

1. **`SG_Dent` (4–6 h)** as in 8.1. Test it on a sphere in `LookDev` first.
2. **Dents in `PuppetPart` (2 h).** `AddDent`, applying on beats, the 6 s hold and the 3-beat repair.
3. **Detection and `DentRpc` (4–5 h).** `ImpactSensor` on all 13 bodies (8.1).
4. **Check (2–3 h).** Drop the character from 2 m, shove it into a wall and walk the stairs. Judge dents on the
   Head and limbs; the cube parts can't show them until phase 6 (section 11).

**Done when:**
- [ ] a dent is visible from 5 m, on host and client;
- [ ] it repairs in 3 beats after 6 s, or when the character gets up;
- [ ] walking the stairs makes no foot dents.

### 4B.2 Getting up (30–40 h)

1. **Foundations (5–7 h).** The GetUp input; `PlantAt`, `PlantUnderAnkle`, `StandAt` and `Stand`; the
   `RecoverStand` hook in `NetworkPlayer` (8.2).
2. **`RecoveryController` and the styles (18–25 h).** The triggers, `IsHeld()` and cancelling,
   `BoostChatter`, dent repair and cosmetic return. Build style C, then A, then B. Add a style switch to the F9
   window.
3. **Playtest (4–6 h).** Run 12.3. Log the winner, or a fallback rule such as A with C on timeout.

**Done when:**
- [ ] all three styles switch live;
- [ ] none explodes on stairs or against a wall: a blocked `StandAt` stays down and logs it;
- [ ] being grabbed or pressing R cancels a recovery cleanly;
- [ ] after every style, `NetworkPlayer.IsFallen` clears on host and client;
- [ ] the winner is logged.

### 4B.3 Cosmetics (18–25 h)

1. **Data and worn items (5–7 h).** `CosmeticItem`, `CosmeticCatalogue`, a `worn` list and `detachedSlots`. A
   placeholder hat under `P_Head` and a scarf under `P_Torso`.
2. **Knocking off (8–11 h).** The calibration log, the host checks, `loosePrefab`, and putting it back by touch,
   on getting up and on timeout (8.3).
3. **Scarf chain (3–4 h).** The local spring chain, drawn on beats.

**Done when:**
- [ ] a hard fall knocks the hat off, on host and client;
- [ ] any player can grab and throw it;
- [ ] touching it or getting up puts it back on;
- [ ] `BalanceSensor.Bodies` is still 13 while it lies loose.

## Phase 5 — Learn Blender (30–45 h, 4 weeks)

Tutorial projects are practice: copy them exactly. Every character choice comes from your maquette.

### 5.1 Courses (20–30 h), in order

1. Grant Abbitt, "Get Started With Blender" (free on GameDev.tv; 8–12 h with practice). Fallback: the modelling
   and sculpting parts of Blender Guru's Blender 5.0 donut.
2. CGBoost, "Blender Sculpting for Beginners" (free; 8–10 h). Focus on voxel remesh and symmetry.
3. Ryan King Art, "Stylized Male Head", parts 1 and 2 (free; 4–6 h).
4. The Blender manual's sculpt brush pages: Clay, Clay Strips, Clay Thumb, Scrape, Smooth (1 h).

### 5.2 Exercises (8–12 h), saved in `ArtSource/Practice/`

1. **Navigation (1 h).** Orbit, pan, zoom, frame the selection (numpad .), and switch to front, side and top
   (numpad 1, 3, 7), ten times without thinking. No numpad: Edit > Preferences > Input > Emulate Numpad.
2. **`pebble.blend` (1 h).** A remeshed sphere, flat on one side, with three uneven lumps.
3. **`bean.blend` (1–2 h).** A bean with one crisp Clay Thumb gouge. If this takes more than 2 h with a mouse,
   buy a tablet (Wacom One S, XP-Pen Deco or Huion, about $40–70). Wacom tablets work on Sway through
   libinput; check pressure in Edit > Preferences > Input > Tablet.
4. **`forearm.blend` (2–3 h).** One forearm within 3 mm of the maquette side photo, at maquette scale.
5. **`head.blend` (3–5 h).** The head within 3 mm of the front and side photos.

### Phase 5 done when

- [ ] all five files are saved;
- [ ] `head.blend` is within 3 mm of the maquette's front and side outlines.

## Phase 6 — The body parts (40–70 h, 6 weeks; 15–30 h on a fast track)

Option A needs no UVs, retopology or rigging. Pick a route from 6.2; the steps below are for route 1, and routes
2 and 3 replace step 6.4.2.

### 6.1 Conventions (1 h)

- One Blender file holds every part: `ArtSource/Puppet/Puppet.blend`. Track it with Git LFS:
  `git lfs track "*.blend" "*.fbx"`.
- Each part exports to `Assets/DrunkCharacter/Art/Meshes/SM_Puppet_<Body>.fbx`, where `<Body>` is the Unity
  name (`Head`, `Forearm_L`, …). The Blender object has the same name.
- Materials `M_…` go in `Art/Materials`, textures `T_…` in `Art/Textures`.

### 6.2 Bring the puppet into Blender (2 h)

1. In Blender: Properties > Scene > Units: Unit System Metric, Unit Scale 1.000, Length Meters. Save.
2. In Unity: Window > Package Manager > + > Add package by name: `com.unity.formats.fbx`.
3. In a new empty scene, place `Player.prefab` at (0, 0, 0). Select `Puppet`, then GameObject > Export To FBX…:
   Export Format Binary, Include Model(s) Only, Object(s) Position World Absolute. Save as
   `ArtSource/Guide/Guide.fbx`.
4. In Blender: File > Import > FBX (.fbx) with the default settings. Select `Mesh_Head`: the N panel > Item >
   Dimensions reads 0.36 m on each axis.
5. Select all the imported objects, press Alt+P > Clear and Keep Transformation, and delete the empty `P_`
   objects. Press M > New Collection, named `Guide`. Set Object Properties > Viewport Display > Display As:
   Wire.

### 6.3 First round trip (2–3 h)

Test the whole pipeline on a throwaway head before sculpting anything real.

1. Duplicate `Mesh_Head` (Shift+D, Esc) and apply its scale (Ctrl+A > Scale). In Sculpt Mode, add a 5 cm bump
   on the front (the nose side) and one on the left (−X).
2. File > Export > FBX with these settings, then save them as an operator preset named `Unity Part` (the + at
   the top of the export dialog):
   - Limit to Selected Objects ✓; Object Types: Mesh only
   - Scale 1.00; Apply Scalings: FBX All; Forward −Z; Up Y; Apply Unit ✓; Apply Transform ✗
   - Smoothing: Face; Apply Modifiers ✓
   - Vertex Colors: sRGB; Prioritize Active Color ✓
   - Bake Animation ✗
3. In Unity, select the FBX, set these, and save them as a Preset:
   - Model: Scale Factor 1, Convert Units ✓, Bake Axis Conversion ✓, Import Cameras ✗, Import Lights ✗,
     Import BlendShapes ✗, Generate Colliders ✗, Normals: Import, Tangents: None
   - Rig: Animation Type None
   - Animation: Import Animation ✗
   - Materials: Material Creation Mode None
4. Expand the FBX in the Project window and assign its mesh to `Mesh_Head`'s MeshFilter. Set `Mesh_Head`'s
   scale and `P_Head`'s `baseScale` to (1, 1, 1).

**Done when:**
- [ ] both bumps point the same way as in Blender: the front bump toward the nose, the left one toward −X;
- [ ] the size matches the old sphere within 1 cm;
- [ ] the head holds between beats.

If an axis is wrong, change only Forward and Up, and repeat.

### 6.4 Each part

**Order:** Head, Nose (or sculpt it into the Head), Torso, Pelvis, UpperArm_L, Forearm_L, Thigh_L, Shin_L, then
the right-side copies (step 3). Hands and feet wait for phase 7.

1. **Start the part (Object Mode, 15 min).**
   1. Select its guide and duplicate it (Shift+D, Esc).
   2. Rename it with F2 to `SM_Puppet_<Body>` and move it to a collection named `Puppet` (M).
   3. Object > Apply > Scale only, never Rotation or Location. The origin and axes stay the body's.

   **Done when** the N panel's Scale reads 1, 1, 1 and its Rotation matches the guide's.
2. **Sculpt the form (2–6 h).**
   1. Mode menu (top left) > Sculpt Mode. In the header's Remesh popover, set Voxel Size to 0.005 m to block in
      and 0.002 m for detail (0.0015 m on the nose), with Preserve Volume ✓ and Preserve Color Attributes ✓.
   2. Press Remesh (Ctrl+R) whenever the brush stretches the mesh.
   3. Block in with Clay Strips against the maquette photos, loaded as reference images in the front (numpad 1)
      and side (numpad 3) views.
   4. Add marks with Clay Thumb at puppet scale (9.3). Hold Shift while brushing to smooth.
   5. **Joint balls.** End each piece at a joint in a rounded ball centred on the joint point, with a radius
      equal to the piece's own (arms about 4.5–5 cm, legs about 7–9 cm). The neighbour's end then stays inside
      it at any bend.
   6. **Clothing edges.** Where a sleeve, trouser leg or collar ends, sculpt an overlapping clay lip, or make
      it a separate piece and join it before export (9.2).

   **Done when** the front and side silhouettes match the maquette photos.
3. **Make the right side from the left (15 min each).** The right parts have their own axes (UpperArm_R is
   turned −90° about Y, UpperArm_L +90°), so the mirrored mesh goes into a copy of the right guide.
   1. Shift+C (cursor to world origin). Set Pivot Point to 3D Cursor.
   2. Duplicate the left part, then Object > Mirror > X Global.
   3. Duplicate the right guide. In Edit Mode, delete all its vertices (A, then X > Vertices). Back to Object
      Mode.
   4. Select the mirrored copy, then Shift-click the emptied guide so it is active, and press Ctrl+J. The joined
      mesh takes the right body's origin and axes.
   5. Rename it `SM_Puppet_<Body>` with the right body's name, and apply Scale.

   **Done when** Overlays > Face Orientation shows only blue. If any red shows: Edit Mode, A, Shift+N.
4. **Colour (30–60 min, before reducing).**
   1. Object Data Properties > Color Attributes > +: name `Col`, Domain Vertex, Data Type Byte Color.
   2. Set Viewport Shading > Solid > Color to Attribute.
   3. In Sculpt Mode, open the brush shelf's Paint category. Paint with Paint Hard; soften with Paint Soft or
      Blur. Follow the channels in 9.2.

   **Done when** a 50% grey patch matches a 50% grey Color property side by side in `LookDev`.
5. **Reduce (20–30 min).**
   1. Duplicate the painted sculpt, rename it `<name>_HP`, move it to a collection named `HighPoly` and hide it
      (H). Keep it: fixes and phase 10 need it.
   2. On the original: Properties > Modifiers > Add Modifier > Generate > Decimate, Collapse.
   3. Set Ratio = the 9.5 budget ÷ the current triangle count (Overlays > Statistics). Adjust until the
      modifier's Face Count is at or below the budget.
   4. Apply it (Ctrl+A over the modifier), then Object > Shade Smooth.

   **Done when** Solid shading with a MatCap shows no pinches or streaks.
6. **Export and swap in (15 min).**
   1. File > Export > FBX with the `Unity Part` preset, to
      `Assets/DrunkCharacter/Art/Meshes/SM_Puppet_<Body>.fbx`.
   2. In Prefab Mode, assign it to the part's `Mesh_<Body>` MeshFilter, and set that object's scale and the
      part's `baseScale` to (1, 1, 1).
   3. Check it in play mode before starting the next part.

   **Done when** it sits on its body with no gap, seen from the gameplay and close-up cameras.

### Phase 6 done when

- [ ] every body part except the hands and feet is a sculpt in the game;
- [ ] the triangle totals are within 9.5 (Statistics overlay with all parts selected);
- [ ] every `_HP` copy is kept in `Puppet.blend`.

## Phase 7 — Face, hands, shoes and cosmetics (24–36 h, 3–4 weeks)

1. **Eyes (4–6 h).** `SM_Puppet_Eye`, `SM_Puppet_Pupil` and `SM_Puppet_Lid_Open`, `_Half` and `_Closed`, pivots
   at the eye centre, at the 7.1 sizes.
2. **Mouths (5–7 h).** `SM_Puppet_Mouth_Closed`, `_Mid`, `_Wide`, `_Grimace` and `_Shock`, sharing one origin
   at the mouth centre. Give each a raised lip 3–4 mm thick that sits against the head, so a swap reads as a
   pressed-on piece. Paint the insides G = 1.
3. **Hands (5–8 h).** `SM_Puppet_Hand_Relaxed_L`, `_Reach_L` and `_Grip_L`, 20 cm across, origin at the hand
   sphere's centre. Mirror them into the `Mesh_Hand_R` guide (6.4 step 3).
4. **Shoes (3–5 h).** `SM_Puppet_Foot_L` within the 7.4 limits: the sole flat, 5 cm below the origin, and a
   2 cm toe curl. Mirror it to `SM_Puppet_Foot_R`. With the origin at the foot centre, the shoe's mesh child
   goes back to local position (0, 0, 0).
5. **Cosmetics (4–6 h).** At least one hat, one face item (glasses or a moustache), the scarf, one backpack,
   and `SM_Puppet_Lump` if style B won.
6. **Swap in and re-test (2–4 h).** Replace every placeholder, then run the stair check and the readability test
   (12.2) again.

### Phase 7 done when

- [ ] no placeholders remain on the character or its cosmetics;
- [ ] the readability test and the stair check pass with the final meshes.

## Phase 8 — Assemble and tune (12–20 h, 2 weeks)

1. **Worst poses (3–5 h).** Pause play mode (Ctrl+Shift+P) in five poses: a sprawled fall, a deep stumble, an
   overhead grab, carrying another player, and climbing Stairs20. Fix any gap by reshaping the `_HP` joint
   ball, then reduce and export again. Never fix a gap by moving an origin.
2. **Dials (3–5 h).** Tune the section 13 dials on the full character. Log any change.
3. **Style frame match (2–3 h).** In `LookDev`, place a Physical Camera with a 26 mm lens (a typical phone's main
   camera) at 9.25 times the phone distance in `StyleFrame.txt`. Lay a screenshot at 50% opacity over
   `StyleFrame.jpg`, and adjust the lights and material until the silhouette, joints and warmth match.
4. **Turntable (1 h).** An 8-second, 360° turntable of the full character.

### Phase 8 done when

- [ ] none of the five poses shows a gap;
- [ ] the overlay matches `StyleFrame.jpg` in silhouette and warmth.

## Phase 9 — World, camera and settings (18–26 h, 2 weeks)

1. **Anti-aliasing (30 min).** In `PC_RPAsset`, set MSAA to 4×. Keep the Main Camera's anti-aliasing at None or
   SMAA, never TAA or STP.
2. **Volume and flicker (3–4 h).**
   1. Create `Assets/Settings/TestSceneProfile.asset` and assign it to `TestScene`'s Global Volume.
   2. Film Grain: Thin1, intensity 0.15, response 0.8.
   3. Optional tabletop Depth of Field: Bokeh, 50 mm, f/2.8, focused on `CameraTarget`.
   4. Flicker: on each beat, set Post Exposure to a random value within ±0.0144 EV (±1%), clamped in code to
      ±0.0426 EV (±3%). Change it through `volume.profile`, the runtime copy, never `sharedProfile`.
3. **Set lighting (4–6 h).** Light the course with 3200 K spot lights (Range at most 15 m) and 2–3 emissive
   practical lamps. Give floors, walls and stairs matte, handmade-looking materials.
4. **Settings menu (4–6 h).** Every section 10 setting, saved in `PlayerPrefs`. Show a one-time photosensitivity
   notice before turning on flicker or boil.
5. **Flash check (1–2 h).** Record 2 minutes at 1080p and 60 fps with flicker at ±3% and boil on, and run it
   through EA IRIS (it builds on Linux with CMake and OpenCV).

### Phase 9 done when

- [ ] IRIS reports no flash or pattern failures;
- [ ] every section 10 setting is saved and works in a build.

## Phase 10 (optional) — One skinned body (60–100 h)

Only if the segment seams bother you after phase 9. It is a project of its own.

1. **Fuse.** Join the `_HP` parts and voxel remesh at 0.003 m to fuse the joints. Retopologise to 15,000–25,000
   quads (QuadriFlow, then clean up the loops at shoulders, hips and knees). Mark seams and unwrap. Bake Normal
   (`T_Puppet_N`) and Color Attribute (`T_Puppet_VC`) at 2048 × 2048 in Cycles.
2. **Armature.** 13 unparented bones at the body pivots, named after the bodies. Weight paint, testing the five
   phase 8 poses. Import with Animation Type Generic.
3. **In Unity.** Drive the bones on beats in `LateUpdate`, with position and rotation only, never the physics
   scale. Set `SkinnedMeshRenderer.updateWhenOffscreen = true`. Map prints from UV0 instead of Triplanar, with
   Fragment Normal Space Tangent and tangents calculated on import, now that there are UVs.

**Done when** the five phase 8 poses show no stretching you don't accept and the body stays within the 9.5
budget.

## 12. Tests

### 12.1 Motion playtest (the phase 1 gate)

- **Players:** 3–5 who don't know the project, at least one prone to motion sickness.
- **Variants,** blind on keys 1–4, in Latin-square order, 4 minutes each at the default `drunkAmount`:
  1. smooth (holds off);
  2. 12 beats a second, world hold;
  3. 12 beats a second, root-smooth;
  4. 8 beats a second, root-smooth.
- **Course:** `TerrainCourse` start to end and back, then up Stairs15.
- **Logged:** falls per minute, recovered ÷ started stumbles, Stairs15 time, gripped ÷ grab attempts.
- **After each round:** ask "Did anything feel laggy or broken?" before mentioning stop motion, then give the
  16-item Simulator Sickness Questionnaire.
- **A stepped variant passes when:**
  1. its falls per minute and stumble recovery are each within ±10% of smooth;
  2. most players describe the motion as a style rather than lag;
  3. no player's questionnaire total rises more than 20 points over their smooth round.

### 12.2 Readability test (phases 4A and 7)

- **Viewers:** 5 who don't know the project, watching 1080p Unity Recorder clips.
- **Talking:** 18 clips of three characters side by side, 6 each at 5, 10 and 15 m, 10 s each, with a random
  talker from `DebugVoice`. The viewer says who is talking within 3 s.
- **Contact:** 6 clips of one character at 10 m walking, grabbing and releasing a cube. The viewer presses a key
  at each foot plant and grab.
- **Passes when:**
  - at least 90% of talkers are named correctly at 10 m;
  - at least 80% of plants and grabs are marked within ±0.25 s of the logged event.
- Log the furthest distance with at least 80% talker accuracy as the proximity-voice range.

### 12.3 Recovery playtest (phase 4B)

- **Players:** 3–5, including a 2-player MPPM session with the client on Mobile4G.
- **Rounds:** 5 minutes per style (C, A, B) in Latin-square order on `TerrainCourse` at `drunkAmount` 1.
- **After each round:** rate delight from 1 to 5, and ask whether getting up got in the way.
- **Pick** the highest-rated style whose median time from `RecoverStart` to `RecoverStand` is under 2 s and that
  nobody says got in their way. Or combine A with C as its timeout fallback.

## 13. Dials

| Dial | Default | Range | Notes |
|---|---|---|---|
| `beatsPerSecond` | 12 | 8–24 | Judged again in phase 3. |
| `rootSmooth` | from the playtest | on / off | |
| `extraHoldBeats` | 1 | 0–2 | Extra intervals after a key-pose beat. |
| Minimum beat interval | 1/24 s | fixed | Stops key poses forcing a beat every frame. |
| `timingVariation` | 0 | 0–0.05 | Keep near 0; rhythm sells comedy. |
| `chatterDegrees` | 0.75° | 0–2° | On parts that moved. |
| `chatterMetres` | 0.004 m | 0–0.008 m | On parts that moved. |
| `idleAmplitude` | 0.5° | 0–2° | Breathing on `P_Torso` and `P_Head` at 0.25 Hz. |
| `squashBeats` | 2 | 1–3 | Scale (1.08, 0.85, 1.08); feet lowered by 15% of origin-to-sole. |
| `_BoilStrength` | 0.3 | 0–1 | Times `_BoilMotion`. |
| `_DisplacementStrength` | 0.005 m | 0–0.015 m | Boil displacement; 0 on cubes. |
| `_PrintNormalStrength` | 0.5 | 0–2 | Fades out from 3 m to 8 m. |
| Smoothness | 0.18 | 0.05–0.4 | Matte base. |
| Coat Mask / Coat Smoothness | 0.35 / 0.65 | 0–0.6 / 0.4–0.85 | Oily sheen on the prints. |
| Mouth attack / release | 0.03 s / 0.12 s | 0.01–0.1 / 0.05–0.3 s | |
| Mouth thresholds, Mid / Wide | 0.15 / 0.55 | | Shapes hold at least 1/12 s. |
| Pupil lead | 0.2 s, at most ±25° | 0–0.4 s | |
| Blinks | every 2–6 s, 5 beats | | Rest at Half when drunk ≥ 0.6. |
| Dent threshold, depth | 2.5 m/s; 1–6 cm | 1.5–4 m/s | 6 cm at 7 m/s; radius 2.5 × depth, at least 6 cm. |
| Dent hold, repair | 6 s; 3 beats | 2–15 s | Or until `RecoverStand`. |
| Flatten | 5 m/s; 3 beats | 3–8 m/s | 0.75 along the hit, 1.15 across. |
| Get-up minDown / autoRecover | 1.5 s / 8 s | 0.5–4 s / 4–20 s | Cancelled when held or reset. |
| Style A lift, timeout | 0.3 m clearance; 3 s | 0.1–0.6 m; 2.5–5 s | Falls back to `StandAt`. |
| Style A drives | 4000 / 1000 linear; 800 / 80 slerp | | `maximumForce = float.MaxValue`. |
| Hat thresholds | 12 rad/s, 60 m/s², 3 m/s, 110° for 0.2 s | calibrated | 1.5 × the drunk-walking peaks. |
| Loose cosmetic | 1 s grace, 60 s timeout | 15–180 s | |
| Scarf spring / damping | 60 / 6 | 20–150 / 2–15 | |
| Flicker | Off (±1%, ±0.0144 EV when on) | up to ±3% (±0.0426 EV) | Clamped in code. |
| Film grain | 0.15 (Thin1, response 0.8) | 0–0.4 | |

## 14. Resources

| Resource | Cost | Notes | Phase |
|---|---|---|---|
| [Grant Abbitt, Get Started With Blender](https://gamedev.tv/courses/get-started-blender) | Free | 8–12 h with practice | 5 |
| [Blender Guru, Blender 5.0 donut](https://www.blenderguru.com/posts/blender-donut-v5-tutorial) | Free | Fallback; modelling and sculpting parts | 5 |
| [CGBoost, Blender Sculpting for Beginners](https://www.cgboost.com/courses/blender-sculpting-for-beginners) | Free | 23 lessons, about 3.5 h of video | 5 |
| [Ryan King Art, Stylized Male Head](https://bazaar.blendernation.com/?p=15686) | Free | Two parts | 5 |
| [Unity Learn, Get started with Shader Graph](https://learn.unity.com/tutorial/get-started-with-shader-graph) | Free | | 3 |
| [Blender manual, sculpt brushes](https://docs.blender.org/manual/en/latest/sculpt_paint/sculpting/brushes/brushes.html) | Free | Reference | 5–7 |
| KIRI Engine, Polycam | Free tiers | Phone scanning for route 2 | 6 |
| [EA IRIS](https://github.com/electronicarts/IRIS) | Free | Flash analyser; builds on Linux | 9 |

---

# Part IV — Records

## 15. Decision log

Add a row whenever a direction or dial is settled.

| Date | Decision | Why |
|---|---|---|
| 2026-10-28 | Physics and camera never step; only the drawn puppet holds | Spider-Verse's simulation lesson; Harold Halibut's and South of Midnight's camera experience |
| 2026-10-28 | Segmented puppet (option A) first; skinned body is optional phase 10 | Authentic to real puppets, no rigging, beginner-friendly, maps one-to-one onto the ragdoll |
| 2026-10-28 | Every effect has a setting; flicker capped at ±3% and off by default | Player split and motion sickness in South of Midnight; WCAG and Xbox flash guidance |
| 2026-10-28 | Face, hands and feet oversized and high-contrast; mouth driven by voice level | Alan: tell who is talking in proximity voice; read where hands and feet are and what they touch |
| 2026-10-28 | Shoes never change the foot colliders | The stair physics is tuned around them |
| 2026-10-28 | Maquette scale 1:9.25; sizes in this guide are game metres | One scale rule for every measurement |
| 2026-10-28 | Vertex colour holds variation and masks, not hue; hue index = `OwnerClientId % 4` | Player hues are applied per player, with no new network state |
| 2026-10-28 | Beat modulo 1200 | Clean 2-, 3- or 4-variant swaps |
| 2026-10-28 | Hard hits dent the clay with shader dents, repaired over beats | Alan: impacts should look like clay; dents land where the hit is |
| 2026-10-28 | Get-up style not fixed; build C, A and B and compare | Alan: explore styles, the invisible hand among them |
| 2026-10-28 | Physical cosmetics; loose items are never under `Player`; identity comes from hue | Alan: a hat that falls off; `BalanceSensor` gathers every Rigidbody under `Player`; cosmetics can fall off |
| 2026-10-29 | Each puppet part is a scale-1 pose object with a scaled mesh child; nose and hands are plain children of their bodies' parts | Face pieces and hats keep their size from placeholders to sculpts and don't shear in a squash; nothing separates at the wrist |
| 2026-10-29 | `SG_Clay` is URP Lit with Clear Coat and object-space normals; dents are computed in part metres | Two-layer clay look with full URP lighting; sculpts have no UVs, so no tangents |

## 16. Risks, open questions and checked facts

**Risks**

- **Holds read as lag or cause sickness.** Mitigations: key-pose beats, chatter, root-smooth, judging after the
  clay is on, the settings, and the blind playtest gate.
- **Latency.** Holds add 0–83 ms (about 42 ms on average) at 12 a second. Key-pose beats remove it at the
  moments players react to.
- **Multiplayer.** Remote characters already lag by network interpolation, so test under Mobile4G.
- **Shoes clip stairs.** Mitigations: the size limits and toe curl in 7.4, and the stair check.
- **The mouth reads only at short range.** The readability test sets the proximity-voice range, not the other
  way round.
- **Seams in extreme poses.** Mitigations: joint balls, clothing, or phase 10.
- **Style A snags** on geometry or other players. Mitigation: the 3 s timeout falls back to `StandAt`.
- **Dents look faceted.** Mitigations: the 6 cm minimum radius and recomputed normals; raise the part's
  triangle budget if needed.
- **Loose cosmetics add network objects.** At most one per player per slot, and they time out after 60 s.
- **`.blend` and `.fbx` size in git.** Use Git LFS.
- **Losing the high-poly sculpts.** Keep the `_HP` copies.
- **Scope.** Phases 5–7 are long for a new 3D artist; the fast-track routes in 6.2 shorten phase 6. Phases 1–4B
  already give a playable, judgeable, readable look.

**Checked facts (2026-10-28 and 2026-10-29)**

- Unity 6000.6.0f1, URP 17.6.0, Shader Graph 17.6.0, Netcode for GameObjects 2.13.3.
- URP: Forward+, SRP Batcher on, GPU Resident Drawer on, MSAA off. `PC_RPAsset` and `TestScene` share
  `SampleSceneProfile.asset`.
- Enter Play Mode Options: on, with domain and scene reload off.
- `Player.prefab`: the scales, masses, rotations and child offsets in section 4; network tick rate 60.
- The Custom Lighting sample's `Lit Oren Nayar` uses only the main light, and all its graphs use the Unlit
  target (read in the package source).
- Blender 5.2.2 LTS: Sculpt Mode's brush shelf has a Paint category (Airbrush, Blend Hard, Blend Soft, Blend
  Square, Blur, Paint Blend, Paint Hard, Paint Hard Pressure, Paint Soft, Paint Soft Pressure, Paint Square,
  Sharpen, Smear). Paint Hard paints into the active Byte Color vertex attribute.

## 17. Review record

| Version | Perspective | Main changes |
|---|---|---|
| 0.1 → 0.2 | Unity programmer, art director, stop-motion animator, 3D art teacher, game designer | 13 bodies; puppet as a sibling, copied in `LateUpdate`; static reset; look-dev scene and style frame; maquette; key-pose beats; local boil; root-smooth; blind playtest |
| 0.2 → 0.3 | Literal followers; Alan's requirements | Network counters and `Snapped`; pixel sizes; scale rule; exact Blender and Unity settings; readable face and voice-driven mouth; hands and feet; dents; get-up styles; cosmetics |
| 0.3 → 0.4 | Gameplay, physics and network programmer | Approach-speed impacts; foot dent filter; `StandAt` with an overlap check; `Stand`; `IsHeld()`; detach calibration; loose items on `PlayerBody` |
| 0.4 → 0.5 | Gemini review | Pose object and mesh child split; Clear Coat instead of the Custom Lighting sample; object-space normals; dent normals without tangents; client counter baseline; `maximumForce`; `RecoverStand` hook; fast-track routes for phase 6 |
| 0.5 → 0.6 | Claude, checked against the code | Rewritten as a guidebook. Fixed: nose and hand offsets in metres; root-smooth offsets in metres; hand dents measured from the hand; dent maths in part metres; mesh children named `Mesh_<Body>` so the Blender guides are identifiable; foot squash for any shoe height; no Emission lighting term; no `TorsoLean` change; Meshroom replaced; relative links |
| 0.6 → 0.7 | Second Gemini review against URP 17.6 & codebase | `P_Nose` and `P_Hand_L/R` carry `PuppetPart` with `source = null` so they stay rigid to their parent while running material, boil, squash and dent logic; Object-Space Triplanar normal fade uses `Normalize(Lerp(Normal (Object), triplanarNormal × _BaseScale, strength))` instead of the Tangent-Space `Normal Strength` node; boil vertex displacement is added in part metres alongside `SG_Dent`; restored Style A's `ConfigurableJoint.targetRotation` formula so the fallen torso turns upright; added `headingYaw` update in `Stand()` and `Ground.At` placement in `PlantAt` |
| 0.7 check | Claude, checked against the code | Kept all of the above except the `headingYaw` writes, which `FixedUpdate` already does every tick. `PlantAt` searches around the sole, not the rig root. The print blend normalises the scaled triplanar normal before the lerp |

## 18. Sources

- [Spider-Verse animation technique (/Film)](https://www.slashfilm.com/562748/into-the-spider-verse-technique)
- [Harold Halibut developers on cutting stepped motion (Steam)](https://steamcommunity.com/app/924750/discussions/0/4208119923880796581)
- [Making Harold Halibut (Creative Bloq)](https://www.creativebloq.com/features/making-harold-halibut)
- [South of Midnight's visual language (ACM SIGGRAPH Blog)](https://blog.siggraph.org/2026/04/stop-motion-southern-gothic-and-games-the-visual-language-of-south-of-midnight.html/)
- [South of Midnight at 60 fps (GamesRadar)](https://www.gamesradar.com/games/action/yes-south-of-midnight-will-still-run-at-60fps-despite-its-stop-motion-animation-youre-still-in-this-crafted-world-without-making-it-you-know-too-nauseating/)
- [South of Midnight's stop-motion toggle (Gameranx)](https://gameranx.com/updates/id/517240/article/south-of-midnight-runs-on-unreal-4-xbox-targets-60-fps-stop-motion-can-be-toggled-off/)
- [Guilty Gear Xrd's art style (GDC Vault)](https://gdcvault.com/play/1022031/GuiltyGearXrd-s-Art-Style-The)
- [Rowbotham, Creating a Realistic Clay Shader for Digital Stop Motion](https://zar67.github.io/Portfolio/assets/ClayRendering/ClayRendering-Report.pdf)
- [Johansson, Clay Material in Unity](https://www.jonasjohansson.dev/blog/clay-material-in-unity/)
- [joebinns/clay (GitHub)](https://github.com/joebinns/clay)
- [The Advanced Art of Stop-Motion Animation: Building Puppets (AWN)](https://www.awn.com/animationworld/advanced-art-stop-motion-animation-building-puppets-part-1)
- [Aardman's Early Man puppets (VFX Voice)](https://www.vfxvoice.com/aardman-goes-back-to-stop-motion-basics-with-early-man/)
- [WCAG 2.3.1, Three Flashes or Below Threshold](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html)
- [Xbox Accessibility Guideline 117](https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/117)
- [EA IRIS (GitHub)](https://github.com/electronicarts/IRIS)
- [Blender to Unity export checklist (Cinevva)](https://app.cinevva.com/guides/blender-to-unity-export-checklist)
- [Miniature faking (Wikipedia)](https://en.wikipedia.org/wiki/Miniature_faking)
