// soft disconnect / reconnect / bot control 단위 테스트
const assert = require("assert");
const RoomManager = require("../src/RoomManager");
const { RECONNECT_GRACE_MS } = RoomManager;

const rooms = new RoomManager();
const fakeWs = (id) => ({ clientId: id, roomId: null, readyState: 1, close() {} });

const room = rooms.createRoom(null);
const ws1 = fakeWs("C1");
const add = rooms.addPlayer(room, "테스터", ws1);
assert.ok(add.player);
ws1.roomId = room.roomId;
const token = add.player.reconnectToken;
assert.ok(token);
rooms.fillWithBots(room);
assert.strictEqual(room.players.length, 5);

// soft disconnect → 봇 대타
const soft = rooms.softDisconnect("C1");
assert.ok(soft);
assert.strictEqual(add.player.connected, false);
assert.ok(rooms.isBotControlled(add.player));
assert.strictEqual(add.player.reconnectToken, token);

// reconnect → clientId 유지
const ws2 = fakeWs("C99");
const rec = rooms.reconnectPlayer(token, ws2);
assert.ok(!rec.error, rec.error);
assert.strictEqual(ws2.clientId, "C1");
assert.strictEqual(ws2.roomId, room.roomId);
assert.strictEqual(add.player.connected, true);
assert.ok(!rooms.isBotControlled(add.player) || add.player.isBot === false);
assert.strictEqual(rooms.isBotControlled(add.player), false);

// 유예 만료 (대기중) → 제거
rooms.softDisconnect("C1");
add.player.disconnectedAt = Date.now() - RECONNECT_GRACE_MS - 1;
const affected = rooms.reclaimExpiredSeats();
assert.ok(affected.length >= 0);
assert.ok(!rooms.findByReconnectToken(token));

console.log("reconnect tests OK");

// Intentional exits delete bot-only rooms immediately, even during a game.
for (const status of ["waiting", "bidding", "playing", "finished"]) {
  const r = rooms.createRoom(null);
  const host = rooms.addPlayer(r, "Host", fakeWs("exit-" + status)).player;
  rooms.fillWithBots(r);
  r.status = status;
  rooms.removePlayerByClientId(host.clientId);
  assert.strictEqual(rooms.getRoom(r.roomId), undefined);
  assert.strictEqual(rooms.findByReconnectToken(host.reconnectToken), null);
}

// A multiplayer room remains for another human, who inherits hosting.
const multi = rooms.createRoom(null);
const first = rooms.addPlayer(multi, "First", fakeWs("first")).player;
const second = rooms.addPlayer(multi, "Second", fakeWs("second")).player;
rooms.fillWithBots(multi);
rooms.removePlayerByClientId(first.clientId);
rooms.fillWithBots(multi);
assert.strictEqual(rooms.getRoom(multi.roomId), multi);
assert.ok(rooms.isHost(multi, second.clientId));
assert.strictEqual(rooms.findByReconnectToken(first.reconnectToken), null);
rooms.removePlayerByClientId(second.clientId);
assert.strictEqual(rooms.getRoom(multi.roomId), undefined);
console.log("intentional leave tests OK");
