# Project A - Combat & Ability Wiring (Fusion Shared + MOST)

This phase adds a combat framework on top of the networked MOST character.

## Added assets

### Scripts
- `Assets/_ProjectA/Networking/Scripts/AbilitySystem.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkProjectile.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkCombatHUD.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkCharacter.cs` (extended)
- `Assets/_ProjectA/Networking/Scripts/NetworkInputProvider_MOST.cs` (extended)
- `Assets/_ProjectA/Networking/Scripts/NetworkPlayerInputData.cs` (extended)
- `Assets/_ProjectA/Networking/Scripts/FusionBootstrap.cs` (spawn ownership fix)

### Prefabs
- `Assets/_ProjectA/Networking/Prefabs/NetworkProjectile.prefab`
- `Assets/_ProjectA/Networking/Prefabs/NetworkedCharacter.prefab` (updated)

---

## Host/Client joystick issue fix (important)

`FusionBootstrap` now spawns a character for the **local player on each client** (`player == runner.LocalPlayer`) instead of only host/master spawning everyone.

This ensures each client gets its own input-authority character, so both peers have local joystick control.

---

## Ability system

`AbilitySystem` contains 3 abilities:
- Basic
- Active
- Ultimate

Each has:
- cooldown
- damage
- projectile speed

Cooldowns are networked with `TickTimer` and consumed only by `StateAuthority`.

### Input mapping (current baseline)
- Move: MOST move joystick (fallback WASD)
- Basic: MOST shoot joystick magnitude > threshold (fallback Mouse0)
- Active: `Q`
- Ultimate: `E`

---

## Projectile networking strategy (Fusion Shared)

`NetworkProjectile` uses:
- `NetworkObject`
- networked direction/speed/damage/lifetime fields
- movement + collision checks in `FixedUpdateNetwork` on **StateAuthority only**

When projectile hits another `NetworkCharacter`, it calls `ApplyDamage()`.

### Damage authority approach
- `NetworkCharacter.Health` is `[Networked]`
- damage mutates health at target `StateAuthority`
- non-authority callers use RPC request (`RpcTargets.StateAuthority`)

This keeps damage consistent in Shared mode.

---

## MOST bridge behavior

`NetworkInputProvider_MOST` reads only `NetworkCharacter.LocalCharacter` inputs.
Remote characters do not read local joystick input.

`NetworkCharacter` disables MOST controller components for non-input-authority instances.

---

## Prefab checklist

## NetworkedCharacter.prefab
Root must have:
- `NetworkObject`
- `AbilitySystem`
- `NetworkCharacter`

`NetworkCharacter` fields:
- `Projectile Prefab` -> `NetworkProjectile.prefab`
- `Move Joystick` / `Shoot Joystick` (optional; auto-wire by name works, manual assignment preferred)

## NetworkProjectile.prefab
Root must have:
- `NetworkObject`
- `NetworkProjectile`
- Collider (trigger is okay for helper checks)

---

## Scene hookups

### Match scene
- Ensure `NetworkSpawnPoints` exists.
- Keep colliders/ground for movement and hit tests.

### UI/HUD
To show simple health + cooldowns:
1. Create HUD texts (Health, Basic CD, Active CD, Ult CD).
2. Add `NetworkCombatHUD` to a UI object.
3. Assign text references in inspector.

---

## 2-player smoke test

1. Run 2 clients (Editor + build or 2 editors).
2. Start from Lobby (`UIFlowController.StartSharedMatch()`).
3. Verify both players can move with their local controls.
4. Verify both can trigger basic/active/ultimate projectile shots.
5. Verify health decreases on hit and replicates on both screens.
6. Verify cooldown text updates for local player.

---

## Troubleshooting

- **Only host can move**
  - Confirm new `FusionBootstrap.OnPlayerJoined` logic is present (local-player spawn per peer).
  - Confirm each client spawns exactly one local character.

- **No projectiles spawning**
  - Check `NetworkCharacter.projectilePrefab` assigned.
  - Check ability cooldowns are not locking tests (watch HUD cooldown).

- **Damage not syncing**
  - Ensure target has `NetworkCharacter` + `NetworkObject`.
  - Ensure RPC to StateAuthority is not stripped by compile errors.

- **No joystick fire integration**
  - Manually assign move/shoot `MOST_Controller` refs in prefab to avoid name mismatch.
