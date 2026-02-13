# Project A - Fusion Shared Mode Wiring

This guide wires the baseline Fusion networking (Shared mode only).

## Added files

- `Assets/_ProjectA/Networking/Scripts/FusionBootstrap.cs`
- `Assets/_ProjectA/Networking/Scripts/FusionGameMode.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkPlayer.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkPlayerInputData.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkInputProvider.cs`
- `Assets/_ProjectA/Networking/Scripts/NetworkSpawnPoints.cs`
- `Assets/_ProjectA/Networking/Prefabs/NetworkPlayer.prefab`

## 1) Runner setup steps

1. Open your **Lobby** scene (or a bootstrap networking scene).
2. Create empty object: `FusionBootstrap`.
3. Add component: `ProjectA.Networking.FusionBootstrap`.
4. On the same object add:
   - `NetworkRunner` (Fusion)
   - `NetworkSceneManagerDefault` (Fusion)
   - `NetworkInputProvider` (ProjectA)
5. In `FusionBootstrap` inspector assign:
   - **Network Player Prefab** -> `Assets/_ProjectA/Networking/Prefabs/NetworkPlayer.prefab`
   - **Spawn Points** -> scene object containing `NetworkSpawnPoints` (create in next section)
6. Keep mode as `FFA` or switch to `Teams`.

> `FusionBootstrap` uses **GameMode.Shared** only.
> `UIRoot.prefab` now includes `FusionBootstrap` and is `DontDestroyOnLoad`, so Start from Lobby works from persistent UI.

## 2) Prefab checklist

`NetworkPlayer.prefab` should have:
- `NetworkObject`
- `NetworkPlayer` (NetworkBehaviour)

Optional but recommended after opening prefab in Unity:
- Add `NetworkTransform` component for transform sync smoothing.

If Unity shows a missing Fusion component on prefab, re-add `NetworkObject` manually and apply.

## 3) Scene object placement

### A) Spawn points
1. In match/lobby scene create empty object: `NetworkSpawnPoints`.
2. Add `ProjectA.Networking.NetworkSpawnPoints`.
3. Configure spawn arrays:
   - **FFA Spawns**: default 4 corners (expandable)
   - **Team A Spawns** / **Team B Spawns**: placeholder lists

### B) Start/Leave hooks
From your UI buttons (recommended):
- Start/Queue button -> call `UIFlowController.StartSharedMatch()`
- Leave button -> call `UIFlowController.LeaveSharedMatchToLobby()`

Direct Fusion calls are also available:
- `FusionBootstrap.StartSelectedModeFromUI()`
- `FusionBootstrap.Instance.LeaveSessionAndReturnToLobby()`

## 4) Shared-mode behavior

- On start: runner tries to join a random compatible shared session.
- If none is available: creates one (`EnableClientSessionCreation = true`).
- On player joined: shared master client spawns `NetworkPlayer` using deterministic spawn index from `PlayerRef.RawEncoded`.

## 5) Input mapping (current keyboard baseline)

- Move: `WASD`
- Jump button bit: `Space`
- Action button bit: `Left Mouse`

(Only movement is currently applied to player movement; button bits are included for future gameplay.)

## 6) 2-client test checklist

## Option A: Editor + Windows build
1. Build Windows client from same project.
2. Start one client in Editor.
3. Start second client from Windows build.
4. Click your wired start action on both.
5. Verify both join same shared session and each sees moving cubes.

## Option B: 2 editors (if your setup supports it)
1. Open same project in two Editor instances.
2. Play both.
3. Trigger start on both.
4. Verify replication.

## 7) Troubleshooting notes

- **Cannot start session / immediate shutdown**
  - Check Photon/Fusion App ID in Fusion project config.
  - Ensure internet access and matching Fusion SDK version on both clients.

- **Player prefab fails to spawn**
  - Verify `NetworkPlayer.prefab` is assigned in `FusionBootstrap`.
  - Ensure prefab has `NetworkObject` component.

- **No movement**
  - Ensure `NetworkInputProvider` exists on runner object.
  - Confirm `runner.ProvideInput = true` (set by `FusionBootstrap`).

- **Players spawn on top of each other**
  - Expand spawn arrays in `NetworkSpawnPoints`.

- **Session leaves but scene does not return**
  - Ensure target scene name `Lobby` exists in Build Settings or pass your lobby scene name to `LeaveSessionAndReturnToLobby(...)`.
