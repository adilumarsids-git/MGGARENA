# Project A - MOST Character Networking Wiring

This guide wires your `_MyCharacters` prefabs into Fusion Shared networking.

## Added assets

- `Assets/_ProjectA/Networking/Scripts/NetworkCharacter.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkInputProvider_MOST.cs`
- `Assets/_ProjectA/Networking/Prefabs/NetworkedCharacter.prefab`

## Authority model used for damage (Shared mode)

- Movement + health state is controlled by the spawned object's **State Authority**.
- Damage requests are sent via RPC to the target's State Authority:
  - `RPC_RequestDamage(int damage)` with `RpcTargets.StateAuthority`
- Health is replicated through `[Networked] int Health` on `NetworkCharacter`.

This keeps damage application consistent in Shared mode and avoids each remote peer directly mutating another player's health.

---

## Prefab checklist

Open `Assets/_ProjectA/Networking/Prefabs/NetworkedCharacter.prefab` and verify:

1. Root has:
   - `NetworkObject`
   - `NetworkCharacter`
2. Character visual child exists (nested from `_MyCharacters/Character1.prefab`).
3. In `NetworkCharacter`:
   - `Move Speed` set as desired
   - `Max Health` set as desired
   - `Action Damage` / `Action Range` optional for test
4. Optional manual joystick assignment (recommended):
   - Drag **JoyStick Move** controller to `moveJoystick`
   - Drag **JoyStick Shoot** controller to `shootJoystick`

`NetworkCharacter` auto-detects these if left empty, but explicit assignment is safer.

---

## FusionBootstrap inspector hookups

On persistent `UIRoot` object (or wherever `FusionBootstrap` lives):

1. `Network Player Prefab` -> `NetworkedCharacter.prefab`
2. `Input Provider` -> leave empty (auto-adds `NetworkInputProvider_MOST`) OR manually add `NetworkInputProvider_MOST` on same object.
3. `Match Scene Name` = `Match`
4. `Lobby Scene Name` = `Lobby`

---

## Scene wiring

You said you wire scenes yourself. Minimum required scene setup:

### Match scene
- One object with `NetworkSpawnPoints`
- Colliders/ground for movement context (if needed)

### Lobby scene
- Start button should call `UIFlowController.StartSharedMatch()`

### Exit/Back button
- Call `UIFlowController.LeaveSharedMatchToLobby()`

---

## Local vs remote joystick behavior

Implemented behavior in `NetworkCharacter`:

- **Local player (`HasInputAuthority`)**
  - Keeps MOST joystick/controller scripts active
  - `NetworkInputProvider_MOST` reads local character joystick values
- **Remote players**
  - MOST controller scripts are disabled
  - joystick control GameObjects are disabled
  - remote movement is driven only by received network input/state

---

## 2-player smoke test

1. Run 2 clients (Editor + Build, or 2 editors if supported).
2. Start from Lobby using `StartSharedMatch()`.
3. Confirm both players spawn in Match scene.
4. Move both players from their own joystick/inputs:
   - each can move self
   - each sees the other move
5. Trigger action/damage test (action input):
   - bring players close
   - hold/shoot action input
   - target health should replicate across both clients

---

## Troubleshooting

- **Character spawns but no joystick control**
  - Open `NetworkedCharacter.prefab` and manually assign move/shoot MOST_Controller refs.
  - Ensure only local player's controllers are active.

- **Health not changing on remote**
  - Verify target object has `NetworkCharacter` + `NetworkObject`.
  - Confirm RPC path reaches StateAuthority (Shared mode peer ownership).

- **Wrong prefab still spawning (cube/old)**
  - Re-check `FusionBootstrap.networkPlayerPrefab` points to `NetworkedCharacter.prefab`.

- **Spawn points not used**
  - Ensure `NetworkSpawnPoints` is present in Match scene.
