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
    cwd: path.resolve(__dirname, '..'), env: { ...process.env, PORT: String(port) }, stdio: ['ignore','pipe','pipe']
  });
  const sockets = [];
  let diagnostics = '';
  server.stderr.on('data', data => diagnostics += data);
  try {
    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('server startup timeout: ' + diagnostics)), 10000);
      server.stdout.on('data', data => { if (String(data).includes('WebSocket server running')) { clearTimeout(timeout); resolve(); } });
      server.on('error', reject);
    });
    const connect = async () => {
      const ws = new WebSocket(`ws://127.0.0.1:${port}`);
      sockets.push(ws);
      const messages = [];
      ws.on('message', raw => messages.push(JSON.parse(raw)));
      const wait = async (type, predicate = () => true) => {
        const deadline = Date.now() + 5000;
        while (Date.now() < deadline) {
          const index = messages.findIndex(m => m.type === type && predicate(m.data));
          if (index >= 0) return messages.splice(index, 1)[0].data;
          await new Promise(resolve => setTimeout(resolve, 10));
        }
        throw new Error('Timed out: ' + type + ' ' + diagnostics);
      };
      await wait('welcome');
      return { ws, messages, wait, send: (type, data = {}) => ws.send(JSON.stringify({ type, data })) };
    };
    let c = await connect();
    c.send('start_tutorial');
    const joined = await c.wait('room_created');
    let lesson = await c.wait('tutorial_state', s => s.step === 'intro');
    // Ordinary bots must not advance while a lesson remains open.
    await new Promise(resolve => setTimeout(resolve, 2200));
    assert.equal(c.messages.filter(m => m.type === 'game_state').at(-1).data.tableCards.length, 0);
    c.ws.close();
    await new Promise(resolve => c.ws.once('close', resolve));
    c = await connect();
    c.send('reconnect', { reconnectToken: joined.reconnectToken });
    await c.wait('reconnected');
    lesson = await c.wait('tutorial_state');
    assert.equal(lesson.step, 'intro');
    assert.equal(lesson.paused, true);
    c.send('ping_from_client', { message: 'connection-check' });
    assert.equal((await c.wait('pong_from_server')).message, 'pong');
    const acknowledge = async () => {
      c.send('tutorial_ack', { revision: lesson.revision });
      await c.wait('tutorial_state', s => s.revision === lesson.revision && !s.paused);
    };
    const next = async step => lesson = await c.wait('tutorial_state', s => s.step === step && s.paused);
    await acknowledge(); await next('team');
    await acknowledge(); await next('follow');
    await acknowledge(); await next('mighty'); await acknowledge();
    c.send('play_card', { cardId: 'S_A' });
    await next('reveal'); await acknowledge(); await next('call'); await acknowledge();
    c.send('play_card', { cardId: 'C_3', activateJokerCall: true });
    await next('round_end'); await acknowledge(); await next('bid'); await acknowledge();
    c.send('bid', { targetScore: 13, trumpSuit: 'HEART', noTrump: false });
    await next('discard'); await acknowledge();
    c.send('discard_kitty', { cardIds: ['S_2','D_2','C_2'] });
    await next('friend'); await acknowledge();
    c.send('choose_friend', { friendCardId: 'S_A' });
    await next('lead'); await acknowledge();
    c.send('play_card', { cardId: 'H_A' });
    await next('complete');
    assert.equal(lesson.complete, true);
    const outsider = await connect();
    outsider.send('join_room', { roomId: joined.roomId, nickname: 'observer' });
    assert.match((await outsider.wait('error_message')).message, /튜토리얼/);
    c.send('leave_room');
    c.send('create_room', { nickname: 'normal' });
    await c.wait('room_created');
    assert.equal((await c.wait('game_state', s => s.status === 'waiting')).players.length, 5);
    console.log('Tutorial WebSocket: both rounds, paused bots, reconnect, private room and normal room exit passed.');
  } finally {
    for (const ws of sockets) ws.terminate();
    server.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
