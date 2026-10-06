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
room.status = "playing";
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

// 게임 중 유예 만료 → 재접속 불가
rooms.softDisconnect("C1");
add.player.disconnectedAt = Date.now() - RECONNECT_GRACE_MS - 1;
const affected = rooms.reclaimExpiredSeats();
assert.ok(affected.length >= 0);
assert.ok(!rooms.findByReconnectToken(token));

console.log("reconnect tests OK");

// Waiting-room disconnect removes the seat immediately and transfers hosting.
const lobby = rooms.createRoom(null);
const host = rooms.addPlayer(lobby, "Host", fakeWs("lobby-host")).player;
const guest = rooms.addPlayer(lobby, "Guest", fakeWs("lobby-guest")).player;
rooms.fillWithBots(lobby);
assert.strictEqual(rooms.softDisconnect(host.clientId), lobby);
assert.ok(!lobby.players.includes(host));
assert.strictEqual(lobby.hostClientId, guest.clientId);
assert.strictEqual(lobby.players.length, 5);
assert.strictEqual(rooms.findByReconnectToken(host.reconnectToken), null);
rooms.softDisconnect(guest.clientId);
assert.strictEqual(rooms.getRoom(lobby.roomId), undefined);
assert.strictEqual(rooms.findByReconnectToken(guest.reconnectToken), null);

const electionRoom = rooms.createRoom(null);
electionRoom.electionUntil = Date.now() + 4000;
assert.match(rooms.discardKitty(electionRoom, "any", []).error, /당선/);

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

// A deliberate exit must preserve all five seats and the departing player's cards.
for (const status of ["bidding", "discarding_kitty", "choosing_friend", "playing", "finished"]) {
  const r = rooms.createRoom(null);
  const departing = rooms.addPlayer(r, "Departing", fakeWs("depart-" + status)).player;
  const remaining = rooms.addPlayer(r, "Remaining", fakeWs("remain-" + status)).player;
  rooms.fillWithBots(r);
  r.status = status;
  const savedToken = departing.reconnectToken;
  departing.hand = [{ id: "S_A" }, { id: "H_2" }];
  departing.sessionScore = 12;
  r.declarerClientId = departing.clientId;
  const seats = r.players.slice();
  const hand = departing.hand;
  assert.strictEqual(rooms.removePlayerByClientId(departing.clientId), r);
  assert.strictEqual(r.players.length, 5);
  seats.forEach((p, i) => assert.strictEqual(r.players[i], p));
  assert.strictEqual(departing.hand, hand);
  assert.strictEqual(departing.sessionScore, 12);
  assert.strictEqual(r.declarerClientId, departing.clientId);
  assert.strictEqual(departing.isBot, true);
  assert.strictEqual(departing.ws, null);
  assert.strictEqual(rooms.isBotControlled(departing), true);
  assert.strictEqual(rooms.findByReconnectToken(savedToken), null);
  assert.strictEqual(r.hostClientId, remaining.clientId);
  // The same browser can leave a new room without removing its old bot seat.
  const next = rooms.createRoom(null);
  rooms.addPlayer(next, "Returned", fakeWs(departing.clientId));
  rooms.removePlayerByClientId(departing.clientId);
  assert.strictEqual(rooms.getRoom(next.roomId), undefined);
  assert.strictEqual(r.players.length, 5);
  rooms.removePlayerByClientId(remaining.clientId);
  assert.strictEqual(rooms.getRoom(r.roomId), undefined);
}
console.log("in-game leave bot replacement tests OK");
