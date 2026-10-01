const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const net = require('node:net');
const path = require('node:path');
const WebSocket = require('ws');

(async () => {
  const reservation = net.createServer();
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const server = spawn(process.execPath, ['server.js'], {
    cwd: path.resolve(__dirname, '..'),
    env: { ...process.env, PORT: String(port), TURN_TIMEOUT_MS: '150' },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let diagnostics = '';
  server.stderr.on('data', data => diagnostics += data);
  const clients = [];
  try {
    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Startup: ' + diagnostics)), 10000);
      server.stdout.on('data', data => {
        if (String(data).includes('WebSocket server running')) { clearTimeout(timeout); resolve(); }
      });
      server.on('error', reject);
    });
    async function connect() {
      const ws = new WebSocket(`ws://127.0.0.1:${port}`);
      const messages = [];
      const c = { ws, messages, send: (type, data = {}) => ws.send(JSON.stringify({ type, data })) };
      c.wait = async (type, predicate = () => true) => {
        const end = Date.now() + 20000;
        while (Date.now() < end) {
          const i = messages.findIndex(m => m.type === type && predicate(m.data));
          if (i >= 0) return messages.splice(i, 1)[0].data;
          await new Promise(resolve => setTimeout(resolve, 10));
        }
        throw new Error('Timed out: ' + type + ' ' + diagnostics);
      };
      ws.on('message', raw => {
        const message = JSON.parse(raw);
        messages.push(message);
        if (message.type === 'game_started') c.send('deal_animation_complete', { dealId: message.data.dealId });
      });
      clients.push(c);
      await c.wait('welcome');
      return c;
    }
    const host = await connect();
    host.send('create_room', { nickname: 'Host' });
    const joined = await host.wait('room_created');
    for (let i = 1; i < 5; i++) {
      const c = await connect();
      c.send('join_room', { roomId: joined.roomId, nickname: 'Human' + i });
      c.token = (await c.wait('room_joined')).reconnectToken;
    }
    for (const c of clients) c.send('ready');
    await host.wait('game_state', s => s.canStart);
    host.send('start_game');
    await host.wait('game_state', s => s.status === 'bidding');
    host.send('bid', { targetScore: 13, trumpSuit: 'HEART' });
    await host.wait('game_state', s => s.status === 'playing');
    const dropped = clients.splice(2, 1)[0];
    dropped.ws.terminate();
    await new Promise(resolve => dropped.ws.once('close', resolve));
    const restored = await connect();
    restored.send('reconnect', { reconnectToken: dropped.token });
    await restored.wait('reconnected');
    // Remain connected, answering pings but making no gameplay input, as in a
    // background tab. All bidding/discard/friend/play decisions must progress.
    const result = await host.wait('game_finished');
    assert.equal(result.declarerTeamScore + result.defenderTeamScore, 20);
    for (const c of clients.slice(1)) assert.deepEqual(await c.wait('game_finished'), result);
    for (const c of clients) {
      for (const message of c.messages.filter(m => m.type === 'game_state' && m.data.status === 'playing')) {
        const s = message.data;
        const isDeclarer = c === host; // Host won the only submitted bid.
        assert.equal(Object.hasOwn(s, 'kittyScore'), isDeclarer);
        assert.equal(s.pointsNeeded, Math.max(0, s.targetScore - s.declarerTeamScore));
        const publicPoints = s.players.filter(p => p.clientId === s.declarerClientId
          || p.clientId === s.friendClientId).reduce((sum, p) => sum + p.score, 0);
        assert.equal(s.declarerTeamScore, publicPoints + (isDeclarer ? s.kittyScore : 0));
      }
      const state = await c.wait('game_state', s => s.status === 'finished');
      assert.equal(state.declarerTeamScore, result.declarerTeamScore);
      assert.equal(state.defenderTeamScore, result.defenderTeamScore);
      assert.ok(state.players.every(p => p.handCount === 0));
      assert.equal(c.messages.filter(m => m.type === 'your_hand').at(-1).data.cards.length, 0);
      assert.equal(c.messages.filter(m => m.type === 'error_message').length, 0);
    }
    for (const c of clients) c.messages.length = 0;
    host.send('return_to_lobby');
    const waiting = await host.wait('game_state', s => s.status === 'waiting');
    assert.ok(waiting.players.every(p => !p.isReady));
    const id = waiting.hostClientId;
    for (const ready of [true, false]) {
      for (const c of clients) c.messages.length = 0;
      host.send('ready');
      for (const c of clients) await c.wait('game_state', s => s.status === 'waiting'
        && s.players.find(p => p.clientId === id).isReady === ready);
    }
    console.log('Idle sockets: five humans, reconnect during play, complete idle game, identical results/hands, lobby ready toggles passed.');
  } finally {
    for (const c of clients) c.ws.terminate();
    server.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
