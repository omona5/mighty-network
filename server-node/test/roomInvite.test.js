const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const net = require('node:net');
const path = require('node:path');
const fs = require('node:fs');
const vm = require('node:vm');
const WebSocket = require('ws');

async function testClipboard() {
  const library = {};
  let copied, reported, replaced;
  const context = {
    LibraryManager: { library }, mergeInto: Object.assign, UTF8ToString: x => x,
    window: { location: { origin: 'http://localhost:3000', search: '?ws=ws://localhost:3000' },
      history: { replaceState: (_, __, url) => replaced = url } },
    navigator: { clipboard: { writeText: async text => { copied = text; } } },
    SendMessage: (_, method, result) => { assert.equal(method, 'OnInviteCopied'); reported = result; },
    document: { body: { appendChild() {} },
      createElement: () => ({ style: {}, select() {}, remove() {} }), execCommand: () => false }
  };
  vm.runInNewContext(fs.readFileSync(path.resolve(__dirname,
    '../../mighty-network-unity/Assets/Plugins/WebGL/RoomNavigation.jslib'), 'utf8'), context);
  library.MightySetPath('/room/ABCD');
  assert.equal(replaced, '/room/ABCD?ws=ws://localhost:3000');
  library.MightyCopyInvite('ABCD', 'NetworkManager');
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(copied, 'http://localhost:3000/room/ABCD');
  assert.equal(reported, 'success');
  context.window.location.origin = 'https://game.example';
  library.MightyCopyInvite('EFGH', 'NetworkManager');
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(copied, 'https://game.example/room/EFGH');
  context.navigator.clipboard.writeText = async () => { throw new Error('denied'); };
  library.MightyCopyInvite('ABCD', 'NetworkManager');
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(reported, 'failure');
  context.navigator.clipboard = null;
  context.document.execCommand = () => true;
  library.MightyCopyInvite('ABCD', 'NetworkManager');
  assert.equal(reported, 'success');
}

(async () => {
  await testClipboard();
  const reservation = net.createServer();
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const server = spawn(process.execPath, ['server.js'], {
    cwd: path.resolve(__dirname, '..'), env: { ...process.env, PORT: String(port) }, stdio: ['ignore', 'pipe', 'pipe']
  });
  const sockets = [];
  let diagnostics = '';
  server.stderr.on('data', data => diagnostics += data);
  try {
    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Startup timeout: ' + diagnostics)), 10000);
      server.stdout.on('data', data => {
        if (String(data).includes('WebSocket server running')) { clearTimeout(timeout); resolve(); }
      });
      server.on('error', reject);
    });
    const base = `http://127.0.0.1:${port}`;
    for (const route of ['/room/ABCD', '/room/abcd/', '/room/ABCD?ws=ws://localhost:3000', '/singleplayer', '/tutorial', '/multiplayer']) {
      const response = await fetch(base + route);
      assert.equal(response.status, 200, route);
      const html = await response.text();
      assert.match(html, /<base href="\/webgl\/">/);
      assert.match(html, /streamingAssetsUrl: new URL\('StreamingAssets', document.baseURI\).href/);
    }
    for (const route of ['/room/ABC', '/room/ABCDE', '/room/ABCD/extra', '/room/ABCD/Build/missing.js'])
      assert.equal((await fetch(base + route)).status, 404, route);
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
      return { wait, send: (type, data = {}) => ws.send(JSON.stringify({ type, data })) };
    };
    const host = await connect();
    const guest = await connect();
    for (const roomId of [null, 42, {}, '__proto__', 'ABC', 'ABCDE']) {
      guest.send('join_room', { roomId, nickname: 'guest' });
      assert.match((await guest.wait('error_message')).message, /4자리/);
    }
    guest.send('join_room', { roomId: '0000', nickname: 'guest' });
    assert.match((await guest.wait('error_message')).message, /찾을 수 없습니다/);
    host.send('create_room', { nickname: 'host' });
    const { roomId } = await host.wait('room_created');
    assert.equal((await fetch(base + '/room/' + roomId)).status, 200);
    guest.send('join_room', { roomId: roomId.toLowerCase(), nickname: 'guest' });
    assert.equal((await guest.wait('room_joined')).roomId, roomId);
    const players = [host, guest];
    for (let i = 2; i < 5; i++) {
      const player = await connect();
      player.send('join_room', { roomId, nickname: 'player' + i });
      await player.wait('room_joined');
      players.push(player);
    }
    const outsider = await connect();
    outsider.send('join_room', { roomId, nickname: 'extra' });
    assert.match((await outsider.wait('error_message')).message, /가득/);
    for (const player of players) player.send('ready');
    await host.wait('game_state', state => state.canStart);
    host.send('start_game');
    await host.wait('game_started');
    outsider.send('join_room', { roomId, nickname: 'extra' });
    assert.match((await outsider.wait('error_message')).message, /이미 시작/);
    outsider.send('create_room', { nickname: 'temporary' });
    const ended = await outsider.wait('room_created');
    outsider.send('leave_room');
    outsider.send('join_room', { roomId: ended.roomId, nickname: 'extra' });
    assert.match((await outsider.wait('error_message')).message, /찾을 수 없습니다/);
    outsider.send('create_room', { nickname: 'private', password: 'secret' });
    const locked = await outsider.wait('room_created');
    const visitor = await connect();
    visitor.send('join_room', { roomId: locked.roomId, nickname: 'visitor' });
    assert.match((await visitor.wait('error_message')).message, /비밀번호/);
    visitor.send('join_room', { roomId: locked.roomId, nickname: 'visitor', password: 'secret' });
    await visitor.wait('room_joined');
    console.log('Room invite: deep routes, malformed codes, join, full/started/ended/private rooms, URL update and clipboard branches passed.');
  } finally {
    for (const ws of sockets) ws.terminate();
    server.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
