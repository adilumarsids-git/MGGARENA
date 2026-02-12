import { Low } from 'lowdb';
import { JSONFile } from 'lowdb/node';
import path from 'node:path';

const dbFile = path.resolve(process.cwd(), 'server/data/db.json');

const adapter = new JSONFile(dbFile);
export const db = new Low(adapter, {
  rooms: [],
  rewards: [],
  items: [],
  roomSequences: {},
  gameStarts: [],
  gameResults: [],
  sessions: []
});

export async function initDb() {
  await db.read();
  db.data ||= {
    rooms: [], rewards: [], items: [], roomSequences: {}, gameStarts: [], gameResults: [], sessions: []
  };
  await db.write();
}
