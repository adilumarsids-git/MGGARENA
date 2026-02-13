# Project A Game Server (MGG proxy)

Thin backend for Unity WebGL client.

## Why this exists
- Keeps `MGG_API_KEY` server-side (never in WebGL client).
- Proxies required MGG call order: `match/start -> games/start -> games/result`.
- Handles token refresh (`401 -> auth/refresh -> retry original`).
- Maintains `room_sequence` as unique incrementing integer per `room_id`.
- Exposes Admin sync endpoints with `Authorization: Bearer {MGG_API_KEY}`.

## Run locally
```bash
cd server
cp .env.example .env
npm install
npm start
```

## Endpoints
Client-facing:
- `POST /client/verify`
- `POST /client/match/start`
- `POST /client/game/start`
- `POST /client/game/result`
- `POST /client/items/purchase`
- `GET /client/config`

Admin sync:
- `POST /rooms`
- `PUT /rooms/:room_id`
- `DELETE /rooms/:room_id`
- `PATCH /rooms/:room_id/status`
- `POST /rewards`
- `PUT /rewards/:room_id`
- `DELETE /rewards/:room_id/:player_count`
- `POST /items`
- `PUT /items/:item_id`
- `DELETE /items/:item_id`

## Persistence
Data is persisted in `server/data/db.json`.

## Deploy notes
- Use HTTPS in production.
- Rotate `SESSION_SECRET` and `MGG_API_KEY` with your secret manager.
- Put this behind your API gateway/WAF.
- Add structured logging and metrics if running at scale.
