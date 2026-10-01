const assert = require('node:assert/strict');
const RoomManager = require('../src/RoomManager');
const rooms = new RoomManager();
for (let game = 0; game < 20; game++) {
  const room = rooms.createRoom(null);
  rooms.addPlayer(room, 'Human', { clientId: 'human-' + game });
  rooms.fillWithBots(room);
  rooms.dealCards(room);
  room.declarerClientId = room.players[0].clientId;
  room.targetScore = 13;
  room.status = 'exchanging_kitty';
  rooms.startKittyExchange(room);
  assert.ok(rooms.discardKitty(room, room.declarerClientId, rooms.botPickDiscardIds(room)).ok);
  room.status = 'choosing_friend';
  const friendId = room.players[2].clientId;
  rooms.chooseFriend(room, room.declarerClientId, game % 2
    ? { friendClientId: friendId } : { friendCardId: room.players[2].hand[0].id });
  room.status = 'playing';
  rooms.startPlay(room);
  for (let turn = 0; turn < 50; turn++) {
    assert.equal(rooms.isHandOver(room), false);
    if (room.trickComplete) {
      const before = JSON.stringify(room);
      assert.ok(rooms.playCard(room, 'wrong-player', 'invalid').error);
      assert.equal(JSON.stringify(room), before, 'invalid input must preserve won trick');
    }
    const p = rooms.currentTurnPlayer(room);
    const pick = rooms.botPickPlay(room, p);
    assert.ok(!rooms.playCard(room, p.clientId, pick.cardId, pick).error);
  }
  assert.equal(rooms.isHandOver(room), true);
  const before = JSON.stringify(room);
  assert.ok(rooms.playCard(room, room.players[0].clientId, 'invalid').error);
  assert.equal(JSON.stringify(room), before);
  const result = rooms.finishGame(room);
  const state = rooms.publicState(room);
  assert.equal(result.declarerTeamScore + result.defenderTeamScore, 20);
  assert.equal(state.declarerTeamScore, result.declarerTeamScore);
  assert.equal(state.defenderTeamScore, result.defenderTeamScore);
  assert.equal(state.friendClientId, friendId);
  assert.equal(result.declarerTeamScore, room.players[0].wonCards.reduce((s,c) => s+c.point, 0)
    + room.players[2].wonCards.reduce((s,c) => s+c.point, 0) + result.kittyScore);
}
console.log('Gameplay: 20 complete deals, 1,000 legal plays, invalid input preservation, team totals/public results passed.');
