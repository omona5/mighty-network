const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const GameRecorder = require('../src/GameRecorder');
const RoomManager = require('../src/RoomManager');
const Rules = require('../src/game/RuleEngine');
const Deck = require('../src/game/Deck');
const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'mighty-recording-'));
const recorder = new GameRecorder({ directory });
const rooms = new RoomManager({ recorder });
const logs = () => fs.readdirSync(directory, { recursive: true }).filter(p => p.endsWith('.jsonl'))
  .map(p => fs.readFileSync(path.join(directory, p), 'utf8').trim().split('\n').map(JSON.parse));
try {
  const room = rooms.createRoom('private-password');
  rooms.addPlayer(room, 'Human', { clientId: 'human', secretSocketField: 'socket-secret' });
  rooms.fillWithBots(room);
  rooms.dealCards(room);
  const initial = room.players.map(p => p.hand.map(c => c.id));
  const kitty = room.kitty.map(c => c.id);
  room.status = 'bidding'; rooms.startBidding(room);
  // Persist all-pass rounds even though room.bids is reset for the next one.
  for (let i = 0; i < 5; i++) rooms.passBid(room, rooms.currentBidder(room).clientId);
  assert.equal(rooms.resolveBidding(room).newMin, 12);
  rooms.startBidding(room, 12);
  assert.ok(rooms.placeBid(room, 'human', { targetScore: 12, trumpSuit: 'HEART' }).ok);
  for (let i = 0; i < 4; i++) rooms.passBid(room, rooms.currentBidder(room).clientId);
  rooms.resolveBidding(room);
  room.status = 'exchanging_kitty';
  rooms.startKittyExchange(room);
  rooms.startKittyExchange(room); // No duplicate pickup event.
  const before = room.players[0].hand.map(c => c.id);
  assert.ok(rooms.discardKitty(room, 'human', [before[0], 'INVALID', before[1]]).error);
  assert.deepEqual(room.players[0].hand.map(c => c.id), before);
  const discarded = rooms.botPickDiscardIds(room);
  rooms.discardKitty(room, 'human', discarded);
  room.status = 'choosing_friend';
  rooms.chooseFriend(room, 'human', { friendCardId: rooms.botFriendCardId(room) });
  room.status = 'playing'; rooms.startPlay(room);
  const n = logs()[0].length;
  assert.ok(rooms.playCard(room, 'invalid', 'INVALID').error);
  assert.equal(logs()[0].length, n);
  for (let i = 0; i < 50; i++) {
    const p = rooms.currentTurnPlayer(room);
    const pick = rooms.botPickPlay(room, p);
    assert.ok(!rooms.playCard(room, p.clientId, pick.cardId, { ...pick, automated: true }).error);
  }
  const result = rooms.finishGame(room);
  rooms.returnToWaiting(room);
  const events = logs()[0];
  assert.deepEqual(events[0].data.initialHands.map(p => p.cards.map(c => c.id)), initial);
  assert.deepEqual(events[0].data.initialKitty.map(c => c.id), kitty);
  assert.equal(events.filter(e => e.type === 'bidding_start').length, 2);
  assert.equal(events.filter(e => e.type === 'bid_pass').length, 9);
  assert.equal(events.find(e => e.type === 'bid').data.targetScore, 12);
  assert.equal(events.filter(e => e.type === 'kitty_pickup').length, 1);
  assert.deepEqual(events.find(e => e.type === 'kitty_pickup').data.cards.map(c => c.id), kitty);
  assert.deepEqual(events.find(e => e.type === 'kitty_discard').data.cards.map(c => c.id), discarded);
  assert.equal(events.filter(e => e.type === 'play').length, 50);
  assert.equal(events.filter(e => e.type === 'trick_end').length, 10);
  assert.equal(events.filter(e => e.type === 'game_end').length, 1);
  assert.deepEqual(events.at(-1).data.result, result);
  // Reconstruct hands and validate every saved play against the preceding state.
  let previous;
  for (const [i, event] of events.entries()) {
    assert.equal(event.seq, i + 1);
    if (event.type === 'play') {
      const p = previous.players.find(p => p.clientId === event.data.clientId);
      assert.ok(p.hand.some(c => c.id === event.data.card.id));
      assert.ok(Rules.canPlayCard({ playerHand: p.hand, card: event.data.card,
        tableCards: previous.trickComplete ? [] : previous.tableCards, ruleConfig: previous.ruleConfig }));
      assert.equal(event.state.players.find(x => x.clientId === p.clientId).hand.length, p.hand.length - 1);
      assert.equal(event.data.controller, p.isBot ? 'bot' : 'auto');
    }
    if (event.type === 'trick_end') {
      assert.equal(event.data.outcomes.filter(x => x.won).length, 1);
      assert.equal(Rules.determineTrickWinner({ tableCards: event.data.table,
        ruleConfig: event.state.ruleConfig, trickNumber: event.data.trickNumber }), event.data.winnerClientId);
    }
    previous = event.state;
  }
  const raw = JSON.stringify(events);
  assert.ok(!raw.includes('private-password') && !raw.includes('socket-secret'));
  assert.ok(!raw.includes('reconnectToken'));
  assert.ok(!JSON.stringify(rooms.publicState(room)).includes('initialHands'));

  // Separate IDs for redeals, persisted partial games, and explicit joker options.
  rooms.dealCards(room);
  rooms.dealCards(room);
  assert.equal(logs().filter(es => es.at(-1).data.reason === 'redealt').length, 1);
  const deck = Object.fromEntries(Deck.createDeck().map(c => [c.id, c]));
  room.ruleConfig = Rules.makeRuleConfig('HEART', false);
  room.declarerClientId = 'human'; room.status = 'playing'; rooms.startPlay(room);
  room.players[0].hand = [deck.JOKER, deck.C_3];
  assert.ok(!rooms.playCard(room, 'human', 'JOKER', { declaredSuit: 'DIAMOND' }).error);
  const joker = logs().flat().find(e => e.type === 'play' && e.data.declaredSuit === 'DIAMOND');
  assert.ok(joker);
  room.tableCards = []; room.currentTurnIndex = 0; room.jokerPlayed = false;
  assert.ok(!rooms.playCard(room, 'human', 'C_3', { activateJokerCall: true }).error);
  assert.ok(logs().flat().some(e => e.type === 'play' && e.data.jokerCallActivated === true));
  rooms.removePlayerByClientId('human');
  assert.ok(logs().some(es => es.at(-1).data.reason === 'no_humans'));
  const count = logs().length;
  const tutorial = rooms.createRoom(null); rooms.fillWithBots(tutorial);
  tutorial.tutorial = {}; rooms.dealCards(tutorial);
  assert.equal(logs().length, count);
  // Storage failure must be observable without terminating gameplay.
  const blocked = path.join(directory, 'not-a-directory'); fs.writeFileSync(blocked, 'x');
  const failures = [];
  const bad = new GameRecorder({ directory: blocked, onError: msg => failures.push(msg) });
  bad.begin(room);
  assert.equal(bad.health().writeErrors, 1);
  assert.equal(failures.length, 1);
  assert.equal(recorder.health().writeErrors, 0);
  console.log('Game recorder: complete/redealt/aborted games, replay, private fields, joker options and write failures passed.');
} finally {
  fs.rmSync(directory, { recursive: true, force: true });
}
