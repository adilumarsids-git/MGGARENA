# Network + MOST Wiring Guide (ProjectA)

This guide explains exactly how the current networking/MOST integration is set up, why movement desync happened, what was changed, and how to wire your scene/prefabs safely.

---

## 1) Root cause of the movement desync

Your character prefab still contains built-in MOST movement components (`MOST_FreeMovement` / `MOST_GridMovement`) and joystick event links that can move the transform directly.

In a Fusion game, direct transform movement outside `FixedUpdateNetwork()` causes desync:

- local player appears to move,
- remote player does not receive the same authoritative state,
- camera appears broken because observed target state is inconsistent.

---

## 2) Current architecture (after fixes)

### Authoritative movement path
- Input is captured by `NetworkInputProvider_MOST`.
- Fusion sends that input as `NetworkPlayerInputData`.
- `NetworkCharacter.FixedUpdateNetwork()` applies movement to `NetPosition`/`NetRotation` on authority.
- `NetworkCharacter.Render()` applies replicated state for visuals.

### Why this is important
Only one movement system should own the character transform in multiplayer.

---

## 3) What was changed

## A. `NetworkCharacter` now disables built-in MOST movement scripts
In `Spawned()`, `DisableBuiltInMostMovement()` is called.

It disables on each spawned network character:
- `MOST_FreeMovement`
- `MOST_GridMovement`

So movement is now network-only.

## B. `NetworkInputProvider_MOST` was made resilient
`NetworkInputProvider_MOST` now:
- supports explicit serialized joystick refs,
- auto-finds joysticks by name if refs are not assigned,
- falls back to scene joystick raw values if `LocalCharacter` wiring is not ready,
- falls back to keyboard controls when needed.

This avoids “no movement at all” when joystick references are not under the network character hierarchy.

---

## 4) Required prefab wiring checklist

## `Assets/_ProjectA/Networking/Prefabs/NetworkedCharacter.prefab`
- Keep `NetworkCharacter` component.
- Keep `NetworkObject` component.
- Keep `LocalPlayerCamera` as child camera (manual MOST wiring target).
- Do **not** rely on `MOST_FreeMovement` or `MOST_GridMovement` for transform movement in network play.

## UIRoot / Bootstrap object
- Ensure `FusionBootstrap` exists once.
- Ensure `NetworkInputProvider_MOST` exists once.
- (Optional but recommended) Assign Move/Shoot/Throw joysticks directly in inspector on `NetworkInputProvider_MOST`.

---

## 5) Joystick naming convention (for auto-wire fallback)

If you do not assign refs manually, auto-wire uses lowercase name matching:
- contains `move` => move joystick
- contains `shoot` => shoot joystick
- contains `throw` => throw joystick

Examples:
- `JoyStick Move`
- `JoyStick Shoot`
- `JoyStick Throw`

---

## 6) Camera ownership rules

- Child `Camera`/`AudioListener` under each `NetworkedCharacter` are enabled only for input-authority local character.
- Remote clones have those disabled.
- This prevents camera steal/switch when player 2 joins.

---

## 7) Single manager rule

- Runtime should have one `NetworkGameManager`.
- Guarded spawn and singleton self-protection are active to prevent duplicate manager clones.

---

## 8) If you still see no movement

1. Confirm `NetworkInputProvider_MOST` is present and enabled.
2. Assign joystick refs manually in inspector (recommended).
3. Verify joystick GameObjects are active.
4. Verify `NetworkCharacter` is spawned and has state authority on local owner.
5. Verify no other script writes `transform.position` on the character root.

---

## 9) What to avoid in network mode

- Don’t use `MOST_FreeMovement` / `MOST_GridMovement` to move the networked character root.
- Don’t run multiple movement systems on same transform.
- Don’t enable remote clone cameras/listeners.

---

## 10) Future extension (if you want full network-native MOST modules)

If you want, next step can be a full dedicated set of wrappers:
- `NetworkMOST_Aim`
- `NetworkMOST_ProjectileGenerator`
- `NetworkMOST_Damage`
- `NetworkMOST_ActionAuthority`
- `NetworkMOST_HealthBarSync`

These wrappers would keep MOST authoring workflow while routing state-changing logic through Fusion authority/RPC.


## 11) Implemented network-native MOST wrapper components

These wrappers are now implemented under `Assets/_ProjectA/Networking/Scripts/`:

- `NetworkMOST_ActionAuthority`
  - Enables/Disables MOST runtime components by authority.
  - Keeps `MOST_Controller`/`MOST_Action` local-only.
  - Keeps `Camera`/`AudioListener` local-only.
  - Disables built-in MOST gameplay drivers (`MOST_FreeMovement`, `MOST_GridMovement`, `MOST_Aim`, `MOST_ProjectileGenerator`, `MOST_Damage`) so they cannot fight Fusion simulation.

- `NetworkMOST_HealthBarSync`
  - Synchronizes networked health values to `HealthBar` safely.

- `NetworkMOST_Aim`
  - Resolves aim direction from MOST shoot/throw joysticks in a network-safe read-only way.

- `NetworkMOST_ProjectileGenerator`
  - Spawns `NetworkProjectile` through Fusion authority path using a consistent wrapper entry point.

- `NetworkMOST_Damage`
  - Utility bridge for routing damage application through networked character authority logic.

`NetworkCharacter` now auto-wires these wrappers at runtime and uses them as primary hooks.
