const assert = require('node:assert/strict');
const RoomManager = require('../src/RoomManager');
const Scoring = require('../src/game/Scoring');
const rooms = new RoomManager();
const points = n => Array.from({ length: n }, (_, i) => ({ id: 'point-' + i, point: 1 }));
const room = {
  roomId: 'TEST', status: 'playing', declarerClientId: 'D', targetScore: 13,
  friendType: 'player', friendClientId: 'F', friendRevealed: true,
  discardedKitty: points(2),
  players: [['D', 8], ['F', 3], ['A', 4], ['B', 2], ['C', 1]].map(([clientId, n]) =>
    ({ clientId, nickname: clientId, wonCards: points(n), hand: [], connected: true })),
};
for (const phase of ['choosing_friend', 'playing']) {
  room.status = phase;
  const own = rooms.publicState(room, 'D');
  assert.equal(own.declarerTeamScore, 13);
  assert.equal(own.kittyScore, 2);
  assert.equal(own.pointsNeeded, 0);
  for (const viewer of ['F', 'A', 'B', 'C', undefined, 'spectator']) {
    const visible = rooms.publicState(room, viewer);
    assert.equal(visible.declarerTeamScore, 11);
    assert.equal(visible.defenderTeamScore, 7);
    assert.equal(visible.pointsNeeded, 2, 'remaining points must not reveal discards');
    assert.ok(!Object.hasOwn(visible, 'kittyScore'), 'friend/defenders must not receive discard score');
    assert.equal(visible.lastResult, null);
  }
}
// Empty discard is distinct from the original kitty before the exchange.
const beforeDiscard = { ...room, discardedKitty: null, kitty: points(3) };
assert.equal(Scoring.liveTeamScores(beforeDiscard, 'D').kittyScore, 0);
assert.equal(Scoring.liveTeamScores(beforeDiscard, 'D').declarerTeamScore, 11);
const zero = { ...room, discardedKitty: [] };
assert.equal(Scoring.liveTeamScores(zero, 'D').kittyScore, 0);
assert.ok(!Object.hasOwn(Scoring.liveTeamScores(zero, 'F'), 'kittyScore'));
// Reconnection restores the same personalized view (stable client ID).
assert.equal(rooms.publicState(room, 'D').kittyScore, 2);
assert.ok(!Object.hasOwn(rooms.publicState(room, 'F'), 'kittyScore'));
const result = rooms.finishGame(room);
assert.equal(result.winner, 'declarer', 'discarded points must decide the final win');
assert.equal(result.declarerTeamScore, 13);
assert.equal(result.kittyScore, 2);
assert.equal(result.defenderTeamScore, 7);
assert.equal(Object.values(result.deltas).reduce((a,b) => a+b, 0), 0);
for (const viewer of ['D', 'F', 'A', undefined]) {
  const final = rooms.publicState(room, viewer);
  assert.equal(final.declarerTeamScore, 13);
  assert.equal(final.kittyScore, 2);
  assert.equal(final.pointsNeeded, 0);
}
console.log('Private discarded points: declarer-only totals, hidden peer payloads, final win and settlement passed.');
