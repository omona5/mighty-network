// Private, append-only game records. Never serve this directory over HTTP.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

function snapshot(room) {
  const fields = ['status', 'minBid', 'currentBidderIndex', 'highestBid', 'bids', 'passedClientIds',
    'declarerClientId', 'declaredTrump', 'noTrump', 'targetScore', 'ruleConfig',
    'friendType', 'friendCardId', 'friendClientId', 'friendRevealed', 'kitty', 'discardedKitty',
    'trickNumber', 'currentTurnIndex', 'tableCards', 'trickComplete', 'lastTrickWinner',
    'mightyRevealed', 'mightyPlayerClientId', 'jokerPlayed'];
  const state = Object.fromEntries(fields.map(key => [key, room[key] ?? null]));
  state.players = room.players.map((p, seat) => ({ seat, clientId: p.clientId, nickname: p.nickname,
    isBot: !!p.isBot, connected: !!p.connected,
    botControlled: !!(p.isBot || !p.connected), botPolicy: p.botPolicy || 'GreedyBot',
    hand: p.hand || [], wonCards: p.wonCards || [], sessionScore: p.sessionScore || 0 }));
  return state;
}

class GameRecorder {
  constructor({ directory = path.join(__dirname, '../data/games'), onError = console.error } = {}) {
    this.directory = path.resolve(directory);
    this.onError = onError;
    this.sessions = new WeakMap();
    this.errors = 0;
    this.lastErrorAt = null;
    this.writtenEvents = 0;
    this.runId = crypto.randomUUID();
  }

  begin(room) {
    this.end(room, 'redealt');
    // Tutorial deals are scripted and must not contaminate training data.
    if (room.tutorial) return;
    const gameId = crypto.randomUUID();
    const date = new Date().toISOString().slice(0, 10);
    this.sessions.set(room, { gameId, seq: 0,
      file: path.join(this.directory, date, `${gameId}.jsonl`) });
    this.record(room, 'deal', { initialHands: room.players.map((p, seat) => ({
      seat, clientId: p.clientId, cards: p.hand,
    })), initialKitty: room.kitty, policyVersion: 'heuristic-8-conservative-bid-mighty-v2',
    serverRevision: process.env.GAME_CODE_VERSION || null });
  }

  record(room, type, data = {}) {
    const session = this.sessions.get(room);
    if (!session || session.closed) return;
    const seq = ++session.seq;
    try {
      const event = { schemaVersion: 1, runId: this.runId, gameId: session.gameId,
        roomId: room.roomId, seq, at: new Date().toISOString(), type, data, state: snapshot(room) };
      const line = JSON.stringify(event) + '\n';
      fs.mkdirSync(path.dirname(session.file), { recursive: true });
      // Write before returning from an accepted action: no in-memory-only game log.
      fs.appendFileSync(session.file, line, { encoding: 'utf8', mode: 0o600 });
      this.writtenEvents++;
    } catch (error) {
      this.errors++;
      this.lastErrorAt = new Date().toISOString();
      this.onError(`[game-recording] ${session.gameId} event ${seq} (${type}): ${error.message}`);
    }
  }

  end(room, reason, result = null) {
    const session = this.sessions.get(room);
    if (!session || session.closed) return;
    this.record(room, 'game_end', { reason, result });
    session.closed = true;
  }

  health() {
    return { enabled: true, writtenEvents: this.writtenEvents,
      writeErrors: this.errors, lastErrorAt: this.lastErrorAt };
  }
}

module.exports = GameRecorder;
