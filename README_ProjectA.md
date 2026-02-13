# README - Project A Foundation

This repo now includes a clean Project A foundation in Unity and a stub backend.

## Folder layout

- `Assets/_ProjectA/Core`
- `Assets/_ProjectA/Networking`
- `Assets/_ProjectA/UI`
- `Assets/_ProjectA/Gameplay`
- `Assets/_ProjectA/Characters`
- `Server/`

## Config system (ScriptableObject)

Config is defined in:

- Script: `Assets/_ProjectA/Core/Scripts/ProjectAConfig.cs`
- Asset: `Assets/Resources/ProjectA/ProjectAConfig.asset`

Set values in the `ProjectAConfig` asset:

- `backendBaseUrl` (BackendBaseUrl)
- `environment` (Dev / Stage / Prod)
- `fusionAppId` (placeholder)
- `fusionRegion` (placeholder)
- `rooms` list with:
  - `roomId`
  - `entryFee`
  - `maxPlayers`
  - `mode`

Runtime loader:

- `Assets/_ProjectA/Core/Scripts/ProjectAConfigLoader.cs`

## Logging helper

Use `ProjectALogger` in:

- `Assets/_ProjectA/Core/Scripts/ProjectALogger.cs`

Supported levels:

- `Info`
- `Warn`
- `Error`

Example:

```csharp
ProjectALogger.Info("Boot complete");
ProjectALogger.Warn("Using fallback region");
ProjectALogger.Error("Backend unreachable");
```

## Run backend locally (stub)

From repo root:

```bash
cd Server
npm install
npm run dev
```

Default URL: `http://localhost:8080`

Available stub endpoints:

- `GET /health`
- `GET /rooms`
