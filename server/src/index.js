import express from 'express';
import dotenv from 'dotenv';
import jwt from 'jsonwebtoken';
import { nanoid } from 'nanoid';
import { db, initDb } from './db.js';
import { MggClient } from './mggClient.js';

dotenv.config();

const app = express();
app.use(express.json());

const PORT = Number(process.env.PORT || 8080);
const SESSION_SECRET = process.env.SESSION_SECRET || 'change-me';
const ADMIN_API_KEY = process.env.MGG_API_KEY || '';
const mggClient = new MggClient({
  baseUrl: process.env.MGG_BASE_URL || 'https://api.mgg.example',
  apiKey: ADMIN_API_KEY
});

const makeServerTime = () => new Date().toISOString();

function authSession(req, res, next) {
  const token = req.body?.session_jwt || req.headers.authorization?.replace('Bearer ', '');
  if (!token) return res.status(401).json({ error: 'session_jwt_required' });
  try {
    req.session = jwt.verify(token, SESSION_SECRET);
    return next();
  } catch (_e) {
    return res.status(401).json({ error: 'invalid_session_jwt' });
  }
}

function authAdmin(req, res, next) {
  const header = req.headers.authorization || '';
  if (header !== `Bearer ${ADMIN_API_KEY}`) {
    return res.status(401).json({ error: 'invalid_admin_api_key' });
  }
  return next();
}

function upsertById(arr, idKey, payload) {
  const idx = arr.findIndex((x) => x[idKey] === payload[idKey]);
  if (idx >= 0) arr[idx] = { ...arr[idx], ...payload }; else arr.push(payload);
}

app.get('/healthz', (_req, res) => res.json({ ok: true, server_time: makeServerTime() }));

app.get('/client/config', async (_req, res) => {
  await db.read();
  return res.json({ rooms: db.data.rooms, items: db.data.items, server_time: makeServerTime() });
});

app.post('/client/verify', async (req, res) => {
  const { game_token, uid, nickname, access_token, refresh_token } = req.body || {};
  const payload = { game_token, uid, nickname, access_token, refresh_token };

  const mgg = await mggClient.call('/auth/login', { method: 'POST', body: payload, accessToken: access_token, refreshToken: refresh_token });
  if (!mgg.response.ok) {
    return res.status(400).json({ is_verified: false, error: mgg.data || 'mgg_login_failed' });
  }

  const sessionPayload = {
    sid: nanoid(),
    uid: uid || mgg.data?.uid,
    nickname: nickname || mgg.data?.nickname,
    game_token,
    access_token: mgg.refreshedTokens?.access_token || access_token,
    refresh_token: mgg.refreshedTokens?.refresh_token || refresh_token,
    created_at: Date.now()
  };

  const session_jwt = jwt.sign(sessionPayload, SESSION_SECRET, { expiresIn: '12h' });

  await db.read();
  db.data.sessions.push({ ...sessionPayload, session_jwt });
  await db.write();

  return res.json({
    is_verified: true,
    emgg_balance: mgg.data?.emgg_balance ?? mgg.data?.balance ?? 0,
    server_time: makeServerTime(),
    session_jwt,
    tokens: mgg.refreshedTokens || null
  });
});

app.post('/client/match/start', authSession, async (req, res) => {
  const { room_id, entry_fee } = req.body || {};
  const mgg = await mggClient.call('/games/match/start', {
    method: 'POST',
    body: {
      room_id,
      entry_fee,
      nickname: req.session.nickname,
      uid: req.session.uid
    },
    accessToken: req.session.access_token,
    refreshToken: req.session.refresh_token
  });
  if (!mgg.response.ok) return res.status(400).json({ ok: false, error: mgg.data || 'match_start_failed' });
  return res.json({ ok: true, eligibility: mgg.data, server_time: makeServerTime(), tokens: mgg.refreshedTokens || null });
});

app.post('/client/game/start', authSession, async (req, res) => {
  const { room_id, player_count, user_count, bot_count, nicknames, game_start_time } = req.body || {};
  await db.read();
  const next = (db.data.roomSequences[room_id] || 0) + 1;
  db.data.roomSequences[room_id] = next;

  const startRecord = {
    key: `${room_id}:${next}`,
    room_id,
    room_sequence: next,
    player_count,
    user_count,
    bot_count,
    nicknames,
    game_start_time,
    host_nickname: req.session.nickname,
    created_at: makeServerTime()
  };
  db.data.gameStarts.push(startRecord);
  await db.write();

  const mgg = await mggClient.call('/games/start', {
    method: 'POST',
    body: startRecord,
    accessToken: req.session.access_token,
    refreshToken: req.session.refresh_token
  });
  if (!mgg.response.ok) return res.status(400).json({ error: mgg.data || 'game_start_failed' });

  return res.json({ room_sequence: next, total_pot: mgg.data?.total_pot ?? 0, server_time: makeServerTime(), tokens: mgg.refreshedTokens || null });
});

app.post('/client/game/result', authSession, async (req, res) => {
  const payload = req.body || {};
  await db.read();

  const key = `${payload.room_id}:${payload.room_sequence}`;
  const cached = db.data.gameResults.find((x) => x.key === key);
  if (cached) return res.json({ ok: true, duplicate: true, rewards: cached.rewards, server_time: makeServerTime() });

  const mgg = await mggClient.call('/games/result', {
    method: 'POST',
    body: payload,
    accessToken: req.session.access_token,
    refreshToken: req.session.refresh_token
  });

  if (!mgg.response.ok) {
    const maybeDuplicate = JSON.stringify(mgg.data || {}).toLowerCase();
    if (maybeDuplicate.includes('duplicate') || maybeDuplicate.includes('already')) {
      const backup = db.data.gameResults.find((x) => x.key === key);
      return res.json({ ok: true, duplicate: true, rewards: backup?.rewards || null, server_time: makeServerTime() });
    }
    return res.status(400).json({ ok: false, error: mgg.data || 'game_result_failed' });
  }

  const record = { key, payload, rewards: mgg.data, created_at: makeServerTime() };
  db.data.gameResults.push(record);
  await db.write();
  return res.json({ ok: true, duplicate: false, rewards: mgg.data, server_time: makeServerTime(), tokens: mgg.refreshedTokens || null });
});

app.post('/client/items/purchase', authSession, async (req, res) => {
  const { item_id, item_count } = req.body || {};
  const mgg = await mggClient.call('/items/purchase', {
    method: 'POST',
    body: { item_id, item_count },
    accessToken: req.session.access_token,
    refreshToken: req.session.refresh_token
  });

  if (!mgg.response.ok) return res.status(400).json({ ok: false, error: mgg.data || 'purchase_failed' });
  return res.json({ ok: true, remaining_balance: mgg.data?.remaining_balance, purchase: mgg.data, tokens: mgg.refreshedTokens || null });
});

// Admin Sync Endpoints
app.post('/rooms', authAdmin, async (req, res) => {
  await db.read();
  upsertById(db.data.rooms, 'room_id', req.body);
  await db.write();
  res.json({ ok: true });
});
app.put('/rooms/:room_id', authAdmin, async (req, res) => {
  await db.read();
  upsertById(db.data.rooms, 'room_id', { ...req.body, room_id: req.params.room_id });
  await db.write();
  res.json({ ok: true });
});
app.delete('/rooms/:room_id', authAdmin, async (req, res) => {
  await db.read();
  db.data.rooms = db.data.rooms.filter((x) => `${x.room_id}` !== req.params.room_id);
  await db.write();
  res.json({ ok: true });
});
app.patch('/rooms/:room_id/status', authAdmin, async (req, res) => {
  await db.read();
  const room = db.data.rooms.find((x) => `${x.room_id}` === req.params.room_id);
  if (!room) return res.status(404).json({ ok: false, error: 'room_not_found' });
  room.status = req.body?.status;
  await db.write();
  res.json({ ok: true });
});

app.post('/rewards', authAdmin, async (req, res) => {
  await db.read();
  db.data.rewards.push(req.body);
  await db.write();
  res.json({ ok: true });
});
app.put('/rewards/:room_id', authAdmin, async (req, res) => {
  await db.read();
  db.data.rewards = db.data.rewards.filter((x) => `${x.room_id}` !== req.params.room_id || `${x.player_count}` !== `${req.body.player_count}`);
  db.data.rewards.push({ ...req.body, room_id: req.params.room_id });
  await db.write();
  res.json({ ok: true });
});
app.delete('/rewards/:room_id/:player_count', authAdmin, async (req, res) => {
  await db.read();
  db.data.rewards = db.data.rewards.filter((x) => `${x.room_id}` !== req.params.room_id || `${x.player_count}` !== req.params.player_count);
  await db.write();
  res.json({ ok: true });
});

app.post('/items', authAdmin, async (req, res) => {
  await db.read();
  upsertById(db.data.items, 'item_id', req.body);
  await db.write();
  res.json({ ok: true });
});
app.put('/items/:item_id', authAdmin, async (req, res) => {
  await db.read();
  upsertById(db.data.items, 'item_id', { ...req.body, item_id: req.params.item_id });
  await db.write();
  res.json({ ok: true });
});
app.delete('/items/:item_id', authAdmin, async (req, res) => {
  await db.read();
  db.data.items = db.data.items.filter((x) => `${x.item_id}` !== req.params.item_id);
  await db.write();
  res.json({ ok: true });
});

initDb().then(() => {
  app.listen(PORT, () => {
    // eslint-disable-next-line no-console
    console.log(`ProjectA server listening on :${PORT}`);
  });
});
