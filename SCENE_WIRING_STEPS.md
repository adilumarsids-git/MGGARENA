# Project A Scene Wiring Steps (Manual)

## Quick Start (recommended)
1. In Unity menu click **ProjectA -> Build Complete Test Setup**.
2. This auto-creates:
   - `Assets/Scenes/Boot.unity`, `Lobby.unity`, `Game.unity`
   - `Assets/_ProjectA/Prefabs/NetworkPlayer_Character1..5.prefab`
   - `Assets/_ProjectA/Prefabs/ProjectA_NetworkRunner.prefab`
   - Game HUD single-canvas joysticks wired to `FusionInputProvider_MOST`
   - Build Settings scenes order: Boot -> Lobby -> Game
3. Open Boot scene and press Play for a full loop test.

## 0) One-time prefab generation
1. Open Unity editor.
2. Run menu: **ProjectA -> Generate Network Player Prefabs**.
3. Confirm prefabs exist in `Assets/_ProjectA/Prefabs/NetworkPlayer_Character1..5.prefab`.
4. Open each network prefab and assign `NetworkPlayer_MOST` references:
   - `freeMovement` -> `CharacterModel/MOST_FreeMovement`
   - `shootAim` -> `Shoot Manager/MOST_Aim`
   - `throwAim` -> `Throw Manager/MOST_Aim`
   - `shootGenerator` -> `Shoot Manager/MOST_ProjectileGenerator`
   - `throwGenerator` -> `Throw Manager/MOST_ProjectileGenerator`
   - `damage` -> `CharacterModel/MOST_Damage`
   - `action` -> `CharacterModel/MOST_Action`
   - `localInputCanvas` -> prefab root Canvas
   - `localCamera` -> character follow camera (if any)

## 1) Boot Scene
1. Create `Bootstrap` GameObject.
2. Add `MggBackendClient` and set backend base URL.
3. Add `BootController`.
4. Drag same `MggBackendClient` to BootController `backendClient`.
5. Set `lobbySceneName` to your Lobby scene.

## 2) Lobby Scene
1. Create `LobbyRoot` GameObject.
2. Add `LobbyController`.
3. Reference persistent `MggBackendClient` (from DontDestroyOnLoad root).
4. Configure room id (e.g. `room_ffa_6` or `room_team_3v3`) and entry fee.
5. Wire your Play button to `LobbyController.OnPlayClicked()`.
6. Optional: call `RefreshConfig()` on scene open to populate room/item UI.

## 3) Game Scene
1. Add `NetworkRunner` prefab to scene or assign a runner prefab in launcher.
2. Add `FusionInputProvider_MOST` and drag 3 MOST joysticks:
   - Move joystick -> `moveJoystick`
   - Shoot joystick -> `shootJoystick`
   - Throw joystick -> `throwJoystick`
3. Add `FusionSessionLauncher`:
   - Set `runnerPrefab`.
   - Set `inputProvider` to the provider object above.
4. Add `NetworkGameManager` GameObject with `NetworkObject` + `NetworkGameManager`.
5. Set `characterPrefabs` list to 5 generated `NetworkPlayer_Character*` prefabs.
6. Set `botPrefab` to one of those prefabs (or a dedicated bot variant).
7. Assign `spawnPoints` transforms (6 points).
8. Drag `MggBackendClient` into `backendClient` field.

## 4) Local vs remote player presentation
`NetworkPlayer_MOST` auto calls `SetLocalPresentation(Object.HasInputAuthority)` during spawn.
- Local player: canvas/camera enabled.
- Remote players: canvas/camera disabled.

## 5) MGG call order behavior
1. Lobby Play -> `/client/match/start`
2. Match actually starts (host) -> `/client/game/start`
3. Match ends (host) -> `/client/game/result`

## 6) Host+Client test checklist
1. Build WebGL and run two browser sessions.
2. Enter queue from both clients.
3. Verify not-full lobby auto-fills bots to 6 players.
4. Close one client mid-match.
5. Verify bot is spawned to replace leaver slot.
6. End timer and verify host sends `/client/game/result` once.
7. Replay same room_sequence result submission to validate duplicate-safe response from backend.

## If you do not see any character spawn
1. In **Game** scene, click the **Start Session** button (this is required).
2. Ensure `NetworkingRoot` has `FusionSessionLauncher` with a valid `runnerPrefab`.
3. Ensure `NetworkGameManager.characterPrefabs` has at least 1 network character prefab assigned.
4. Check Console for `Failed to start Fusion shared session` logs.
