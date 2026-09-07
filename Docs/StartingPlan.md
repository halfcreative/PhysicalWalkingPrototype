# Starting Plan — Active Self-Balancing Ragdoll

The goal for this prototype is an **active ragdoll**: a fully physics-simulated body whose
joints are motor-driven, so it holds itself up rather than being posed. Everything else in the
README — steps, drunkenness, grabbing, multiplayer, the claymation look — sits on top of that
and is deliberately out of scope until the body stands and recovers.

---

## 1. Where things stand (2026-09-06)

The body exists in `Assets/Scenes/TestScene.unity` under `Player`. Eight rigidbodies, seven
`ConfigurableJoint`s, no scripts yet. It holds a pose and falls over.

Each bone is a **single GameObject** — MeshFilter, MeshRenderer, Collider and Rigidbody all on
it, sized by transform scale with the collider left at unit dimensions. No child visual object.

```
Player
  Pelvis    scale (0.340, 0.200, 0.220)   14 kg    box
  Torso     scale (0.400, 0.360, 0.240)   24 kg    box      -> Pelvis    waist
  Thigh_L   scale (0.180, 0.210, 0.180)    7 kg    capsule  -> Pelvis    hip
  Thigh_R   scale (0.180, 0.210, 0.180)    7 kg    capsule  -> Pelvis    hip
  Shin_L    scale (0.140, 0.200, 0.140)   3.5 kg   capsule  -> Thigh_L   knee
  Shin_R    scale (0.140, 0.200, 0.140)   3.5 kg   capsule  -> Thigh_R   knee
  Foot_L    scale (0.160, 0.100, 0.300)   1.5 kg   box      -> Shin_L    ankle
  Foot_R    scale (0.160, 0.100, 0.300)   1.5 kg   box      -> Shin_R    ankle
```

Total 62 kg. No head or arms yet — they are cosmetic for balance and their mass would only
confuse tuning. They go on once it stands.

### Joint configuration

All joints: linear motion `Locked` on all three axes, angular `Limited` on all three,
`rotationDriveMode = Slerp`, `enablePreprocessing = false`,
`projectionMode = PositionAndRotation`, `targetRotation = identity`.

| Joint | Drive spring / damper / maxForce | X limits | Y | Z |
|---|---|---|---|---|
| Waist | 3000 / 144 / 6000 | −25 … 25 | 25 | 20 |
| Hip   | 3500 / 168 / 7000 | −90 … 45 | 30 | 30 |
| Knee  | 2500 / 120 / 5000 | −120 … 0 | 5 | 5 |
| Ankle | 1500 / 72 / 3000  | −35 … 35 | 15 | 15 |

The rig was assembled **standing**, and a Slerp drive pulls toward the pose its joint was
configured in — which is why every target is identity and it holds itself without any code.

### Project settings that matter

- `Fixed Timestep` = **0.01** (100 Hz). Stiff drives are a stiff system; at 50 Hz they buzz.
- `m_DefaultSolverIterations` = **16**, `m_DefaultSolverVelocityIterations` = **4**.
  `Rigidbody.solverIterations` has no serialized field — it is a runtime-only override of the
  project default, so per-body values never survive a domain reload. Set it project-wide.
- `PlayerBody` (layer 8) self-collision disabled in the collision matrix.

### Anchors are scale-relative

`ConfigurableJoint.anchor` lives in local space, so **transform scale divides it**. The hip
reads `-0.3235` on the pelvis, not `-0.11`, because 0.11 ÷ 0.34 = 0.3235. Anchors were computed
as `worldOffset / lossyScale` and verified to resolve to the same world point from both ends
(0.00000 m mismatch). **Rescaling a bone in the inspector moves its anchors** — redivide.

---

## 2. Measured baseline

```
COM                      (0, 0.930, 0.0024)
foot support (z)         [-0.100 .. 0.200]
heel margin               0.102 m
toe margin                0.198 m
COM height above ankle    0.830 m
gravity tipping torque     480 N*m per radian of lean
both ankle drives         3000 N*m/rad   ->  6.24x margin
```

---

## 3. Two defects to fix first

**Foot is not centred on the ankle.** The ankle sits at z=0 but the foot runs −0.10 to +0.20,
so there is half as much margin backward as forward. It falls backward for this reason.
Fix: set `Foot_L`/`Foot_R` z from `0.05` to `0.0` for a symmetric ±0.15.

**The knee rests exactly on its limit.** `xHigh = 0` with `targetRotation` at identity puts the
knee flush against the boundary of its allowed range, so the limit and the drive are both live
at zero deflection and the knee can only ever deviate one way. Fix: `xHigh = 5`, or give the
rest pose a few degrees of knee flex (also more natural).

Both are inspector edits. Neither makes it stand — they remove a bias that would otherwise
pollute every measurement taken afterwards.

---

## 4. Why it falls, and what would stop it

What exists now is **pose-holding**, not balance. Joint drives hold *relative* rotation between
adjacent bodies. That is not the same as staying upright.

While the foot is flat, leaning does rotate the shin against the foot, so the ankle drive
resists — and it does so by pressing the toe down, which shifts the **centre of pressure**
forward. This works until the CoP reaches the edge of the foot. Past that the foot pivots on its
heel as a rigid unit with the leg, the ankle angle stops changing, and no drive anywhere can
produce a useful torque. Hence a fall that is slow and then unstoppable rather than springy.

The consequence worth internalising: **the usable ankle authority is set by foot size, not by
drive strength.**

```
tau_max = m * g * (ankle-to-foot-edge distance)
backward:  59 * 9.81 * 0.10 =  58 N*m
forward:   59 * 9.81 * 0.20 = 116 N*m
```

Against a 3000 N·m/rad spring, those are the numbers that actually bind. Raising springs does
nothing once the CoP has saturated. Bigger feet mean literally more balance authority — which
happens to agree with the README's big-feet art direction.

Standing is an unstable equilibrium. Without feedback correcting error, solver noise alone
eventually walks the COM out of the support polygon.

---

## 5. The balance controller

### The signal that matters: the capture point

Reacting to COM *position* is always too late. The quantity to control is where the COM will
come to rest if nothing changes — for a linear inverted pendulum of height h:

```
omega = sqrt(g / h)                  = sqrt(9.81 / 0.93) = 3.25 rad/s
xi    = com_horizontal + comVel_horizontal / omega
```

For this rig `1/omega` is **0.308 s** of lead time. `xi` inside the support polygon means the
ankles can still recover; `xi` outside means a step is required. That boundary is the whole
control law, and it is the direct analogue of a "how close am I to falling" readout.

### Two strategies, in order

**Ankle strategy** — while `xi` is inside the polygon, drive the ankle `targetRotation`
proportional to the error, clamped so the commanded torque never exceeds `tau_max` above.
Beyond that clamp the foot rolls onto its edge and the command is a lie.

**Stepping** — when `xi` leaves the polygon, place a foot at or beyond it. This is where the
procedural stepping work begins, and it is the natural seam to the rest of the README.

### The targetRotation inversion

Now that targets stop being identity, this matters. Cache the rest pose once, then:

```
startLocalRotation = child.localRotation            // in Awake

joint.targetRotation = Quaternion.Inverse(desiredLocalRotation) * startLocalRotation
```

Assigning the desired rotation directly does something *almost* right, which is exactly why it
costs an afternoon to spot.

---

## 6. Next session

1. **Fix the two defects above.** ~15 min.
2. **`BalanceSensor`** — compute COM, COM velocity, `omega`, the capture point, and the support
   polygon from the grounded feet each `FixedUpdate`. Read-only; no forces. ~40 min.
3. **Draw all of it** — support polygon, COM ground projection, capture point, CoP. ~30 min.
   Do this *before* the controller. A controller whose inputs you cannot see is untunable, and
   the first thing to confirm is that the capture point leaves the polygon just before it falls.
   If that does not line up, the sensor is wrong and no amount of gain tuning will save it.
4. **Ankle strategy** — error to ankle target, clamped at `tau_max`. ~40 min.

Sequence matters: sensor, then visualisation, then control. Steps 2 and 3 are the session's
real deliverable; step 4 is where it starts standing.
