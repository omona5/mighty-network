const assert = require('node:assert/strict');
const RoomManager = require('../src/RoomManager');
const rooms = new RoomManager();
// Passing must not increase the next bid; all-pass rounds really allow 12/11.
{
  const room = rooms.createRoom(null);
  rooms.addPlayer(room, 'Bidder', { clientId: 'bid-regression' });
  rooms.fillWithBots(room);
  room.status = 'bidding';
  rooms.startBidding(room);
  assert.ok(rooms.placeBid(room, room.players[0].clientId,
    { targetScore: 13, trumpSuit: 'HEART' }).ok);
  assert.equal(rooms.publicState(room).nextMinBid, 14);
  assert.ok(rooms.passBid(room, room.players[1].clientId).ok);
  let state = rooms.publicState(room);
  assert.equal(state.currentBidderClientId, room.players[2].clientId);
  assert.equal(state.nextMinBid, 14);
  assert.ok(rooms.placeBid(room, room.players[2].clientId,
    { targetScore: 14, trumpSuit: 'HEART' }).ok);
  rooms.startBidding(room);
  for (const minimum of [13, 12]) {
    for (let i = 0; i < 5; i++) assert.ok(rooms.passBid(room, rooms.currentBidder(room).clientId).ok);
    const result = rooms.resolveBidding(room);
    assert.equal(result.newMin, minimum - 1);
    rooms.startBidding(room, result.newMin);
    state = rooms.publicState(room);
    assert.equal(state.minBid, minimum - 1);
    assert.equal(state.nextMinBid, minimum - 1);
    assert.equal(state.highestBid, null);
  }
  assert.ok(rooms.placeBid(room, room.players[0].clientId,
    { targetScore: 11, trumpSuit: 'HEART' }).ok);
  rooms.startBidding(room, 12);
  assert.ok(rooms.placeBid(room, room.players[0].clientId,
    { targetScore: 12, trumpSuit: 'HEART' }).ok);
  room.declarerClientId = room.players[0].clientId;
  room.status = 'choosing_friend';
  assert.ok(rooms.chooseFriend(room, room.declarerClientId,
    { friendClientId: room.players[2].clientId }).ok);
  state = rooms.publicState(room);
  assert.equal(state.friendType, 'player');
  assert.equal(state.friendNickname, room.players[2].nickname);
  assert.equal(state.friendCardId, null);
}
console.log('Bidding: pass preserves 14, all-pass lowers to 12/11; named friend is public.');
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
