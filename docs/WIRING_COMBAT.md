# Project A - Combat Wiring (MOST Shoot/Throw + Fusion Shared)

This version removes the separate `AbilitySystem` and uses your existing MOST setup:
- `JoyStick Shoot` = basic attack trigger
- `JoyStick Throw` = ultimate trigger

## What changed

- Removed standalone `AbilitySystem` and `NetworkCombatHUD`.
- Combat is handled directly in `NetworkCharacter` using:
  - networked cooldown timers for **basic** and **ultimate**
  - network projectile spawning
  - existing `NetworkCharacter.ApplyDamage(...)` for hit damage
- Health UI is synced through existing MOST `HealthBar` component (already under character canvas).

---

## Prefabs

### 1) NetworkedCharacter.prefab
`Assets/_ProjectA/Networking/Prefabs/NetworkedCharacter.prefab`

Root has:
- `NetworkObject`
- `NetworkCharacter`

`NetworkCharacter` fields:
- `Projectile Prefab` -> `NetworkProjectile.prefab`
- `Health Bar` -> (optional) auto-wires from child
- `Move Joystick` -> (optional) auto-wires `JoyStick Move`
- `Shoot Joystick` -> (optional) auto-wires `JoyStick Shoot`
- `Throw Joystick` -> (optional) auto-wires `JoyStick Throw`

### 2) NetworkProjectile.prefab
`Assets/_ProjectA/Networking/Prefabs/NetworkProjectile.prefab`

Has:
- `NetworkObject`
- `NetworkProjectile`
- trigger collider

---

## Input bridge behavior

`NetworkInputProvider_MOST` now sends:
- Move axis from `JoyStick Move`
- Basic button from `JoyStick Shoot`
- Ultimate button from `JoyStick Throw`

Remote players do not read local joysticks.

---

## Health sync behavior

`NetworkCharacter.Health` is `[Networked]`.

On every render tick, `NetworkCharacter` updates the existing `HealthBar` component:
- sets/reset max when needed
- calls `HealthBar.UpdateHealth(Health)` when value changes

So your existing in-character health canvas is reused.

---

## Shared-mode projectile & damage strategy

- Projectile simulation/hit checks run on projectile `StateAuthority`.
- On hit, projectile calls `target.ApplyDamage(...)`.
- Damage is finalized at target `StateAuthority` via RPC fallback in `NetworkCharacter`.

---

## Fix for "only host moves"

`FusionBootstrap.OnPlayerJoined` now spawns each player's own object when:
- joined player == `runner.LocalPlayer`

This ensures each client gets input authority for its own character (and therefore local joystick control).

---

## Scene wiring checklist

1. `UIRoot` has `FusionBootstrap`.
2. `FusionBootstrap.networkPlayerPrefab` -> `NetworkedCharacter.prefab`.
3. Match scene has `NetworkSpawnPoints`.
4. Build Settings contains `Lobby` and `Match` scene names matching bootstrap fields.

---

## 2-client smoke test

1. Start client A and client B.
2. Join from Lobby on both.
3. Verify each client can move their own character with `JoyStick Move`.
4. Verify `JoyStick Shoot` fires basic projectile.
5. Verify `JoyStick Throw` fires ultimate projectile.
6. Verify hit damage updates health bars on both clients.

