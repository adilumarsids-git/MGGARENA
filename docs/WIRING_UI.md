# Project A UI Wiring Guide

This document explains exactly how to wire the scene/UI skeleton produced for Project A.

## Created assets

### Scenes
- `Assets/_ProjectA/Scenes/Boot.unity`
- `Assets/_ProjectA/Scenes/Title.unity`
- `Assets/_ProjectA/Scenes/Lobby.unity`
- `Assets/_ProjectA/Scenes/Match.unity`
- `Assets/_ProjectA/Scenes/Results.unity`

### UI Prefabs
- `Assets/_ProjectA/UI/Prefabs/UIRoot.prefab` (persistent root, `DontDestroyOnLoad`)
- `Assets/_ProjectA/UI/Prefabs/BootPanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/TitlePanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/LobbyPanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/QueuePanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/HUDPanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/ResultsPanel.prefab`
- `Assets/_ProjectA/UI/Prefabs/ErrorModal.prefab`

### Scripts
- `Assets/_ProjectA/UI/Scripts/AppState.cs`
- `Assets/_ProjectA/UI/Scripts/ProjectAPanel.cs`
- `Assets/_ProjectA/UI/Scripts/BootPanel.cs`
- `Assets/_ProjectA/UI/Scripts/TitlePanel.cs`
- `Assets/_ProjectA/UI/Scripts/LobbyPanel.cs`
- `Assets/_ProjectA/UI/Scripts/QueuePanel.cs`
- `Assets/_ProjectA/UI/Scripts/HUDPanel.cs`
- `Assets/_ProjectA/UI/Scripts/ResultsPanel.cs`
- `Assets/_ProjectA/UI/Scripts/ErrorModal.cs`
- `Assets/_ProjectA/UI/Scripts/UIRoot.cs`
- `Assets/_ProjectA/UI/Scripts/UIFlowController.cs`

---

## 1) Build Settings

1. Open **File → Build Settings**.
2. Add scenes in this order:
   1. `Boot`
   2. `Title`
   3. `Lobby`
   4. `Match`
   5. `Results`

The scene names must match the defaults in `UIFlowController`.

---

## 2) Boot scene (persistent root)

Open `Boot.unity` and do this once:

1. Drag `UIRoot.prefab` into the scene hierarchy.
2. Under `UIRoot`, create a `Canvas` (Screen Space - Overlay).
3. Add an `EventSystem` object if scene doesn’t have one.
4. As children of `Canvas`, drag these prefabs:
   - `BootPanel`
   - `TitlePanel`
   - `LobbyPanel`
   - `QueuePanel`
   - `HUDPanel`
   - `ResultsPanel`
   - `ErrorModal`
5. Select `UIRoot` object → `UIFlowController` component.
6. Drag references in inspector:
   - `Boot Panel` → BootPanel instance
   - `Title Panel` → TitlePanel instance
   - `Lobby Panel` → LobbyPanel instance
   - `Queue Panel` → QueuePanel instance
   - `Hud Panel` → HUDPanel instance
   - `Results Panel` → ResultsPanel instance
   - `Error Modal` → ErrorModal instance

> Keep scene fields as: Boot/Title/Lobby/Match/Results unless you rename scenes.

---

## 3) What to place in each scene

Because `UIRoot` is `DontDestroyOnLoad`, only one persistent UI root is needed.

- **Boot scene**
  - Place `UIRoot.prefab` + Canvas + panel children (as above).
- **Title scene**
  - No required Project A object.
  - Optional: environment visuals/camera only.
- **Lobby scene**
  - No required Project A object.
  - Optional: lobby background/camera only.
- **Match scene**
  - No required Project A object.
  - Optional: match world/camera only.
- **Results scene**
  - No required Project A object.
  - Optional: results background/camera only.

If you prefer, you can place a copy of `UIRoot.prefab` in every scene; duplicates auto-destroy at runtime due to singleton guard in `UIRoot`.

---

## 4) UI content expected inside each panel

Add real UI components under each panel prefab:

- **BootPanel**: status text
- **TitlePanel**: Start button
- **LobbyPanel**: nickname display, mode select, Play/Queue button
- **QueuePanel**: searching spinner + Cancel button
- **HUDPanel**: timer + score
- **ResultsPanel**: rank list + Exit button
- **ErrorModal**: title + message + Retry/Close buttons

Current prefabs are skeleton containers intended for your manual UI composition.

---

## 5) Runtime flow hooks

`UIFlowController` methods you can call from buttons/code:

- `SetState(AppState.Boot)`
- `SetState(AppState.Title)`
- `SetState(AppState.Lobby)`
- `SetState(AppState.Queue)`
- `SetState(AppState.Match)`
- `SetState(AppState.Results)`
- `StartSharedMatch()` (recommended for Lobby Start button; will queue UI and invoke Fusion bootstrap)
- `LeaveSharedMatchToLobby()`
- `ShowErrorModal(true/false)`

No Photon/Fusion integration is included yet.

---

## 6) Playmode checklist

1. Enter Play Mode from `Boot` scene.
2. Confirm only one `UIRoot` exists after scene transitions.
3. Call each `SetState(...)` path from temporary test buttons:
   - Boot -> Title -> Lobby -> Queue -> Match -> Results.
4. Confirm expected scene switches:
   - Queue stays in `Lobby` scene.
5. Confirm only active panel is visible for the current app state.
6. Confirm `ShowErrorModal(true)` overlays and `ShowErrorModal(false)` hides it.
7. Stop Play Mode and verify no missing references on `UIFlowController`.
