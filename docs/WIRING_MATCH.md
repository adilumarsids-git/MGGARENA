# Project A - Match Rules Wiring (FFA 6 / Team 3v3)

This phase adds networked match timing, scoring, and ranking result data for Results UI.

## Added components

- `NetworkGameManager` (`Assets/_ProjectA/Networking/Scripts/NetworkGameManager.cs`)
- `MatchResultEntry` + `MatchResultsData` (`Assets/_ProjectA/Networking/Scripts/MatchResultModels.cs`)

## Updated components

- `FusionBootstrap` now spawns `NetworkGameManager` (master peer) and configures mode/duration.
- `NetworkCharacter` now:
  - uses `NetworkGameManager.IsMatchLocked` to freeze gameplay when match ends
  - reports eliminations for scoring
  - tracks eliminated state
- `NetworkProjectile` now forwards attacker player id for score attribution.

---

## Supported modes

- `FusionGameMode.FFA` -> intended for up to 6 players
- `FusionGameMode.Teams` -> intended for 3v3

> Team assignment in this baseline is deterministic by player id parity (`RawEncoded % 2`).

---

## Networked GameManager behavior

`NetworkGameManager` tracks:

- match timer (`MatchTimer`)
- match lock/end state (`MatchLocked`)
- team scores (`TeamAScore`, `TeamBScore` for team mode)
- mode (`ModeValue`)

When timer expires:

1. `MatchLocked = true`
2. input/damage/projectiles stop
3. ranking is generated with consecutive ranks `1..N`
4. result entries are published to `MatchResultsData`

`MatchResultsData.Entries` is what your `ResultsPanel` should read.

---

## Prefab / inspector wiring

### UIRoot -> FusionBootstrap

In `Assets/_ProjectA/UI/Prefabs/UIRoot.prefab` (or scene instance), verify:

- `Network Player Prefab` -> `NetworkedCharacter.prefab`
- `Network Game Manager Prefab` -> `NetworkGameManager.prefab`
- `Match Duration Seconds` -> set desired duration
- `Selected Mode`:
  - `FFA` for 6-player free-for-all
  - `Teams` for 3v3

### Match scene

- Keep `NetworkSpawnPoints` object present.
- For Teams mode, tune Team A / Team B spawn sets in `NetworkSpawnPoints`.

Important:
- Do **not** make `NetworkGameManager` a child of `UIRoot` in scene hierarchy.
- `FusionBootstrap` spawns it automatically when a match starts.

---

## ResultsPanel data integration

At end match, read:

- `MatchResultsData.Mode`
- `MatchResultsData.Entries` (list of `{ nickname, rank, score }`)

Example render logic per row:

- `#{entry.rank}  {entry.nickname}  Score: {entry.score}`

---

## Scoring rules in this baseline

- Score increments by +1 when a player's projectile eliminates another player.
- Eliminated players cannot move/attack for remaining match time.
- Damage is ignored after match is locked.

---

## Quick validation checklist

1. Start two clients and join match.
2. Verify timer runs and eventually locks match.
3. Verify when match locked:
   - no movement
   - no projectile spawn
   - no damage updates
4. Verify results list contains consecutive ranks from `1..N`.


## Local camera note
- `NetworkCharacter` now auto-creates a local camera for the input-authority player if no `Camera.main` exists.
- This avoids the `No cameras rendering` screen in Match scene.
