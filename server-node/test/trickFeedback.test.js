const assert = require('node:assert/strict');
const RoomManager = require('../src/RoomManager');
const Rules = require('../src/game/RuleEngine');
const { makeCard: card, makeJoker } = require('../src/game/Card');
const rooms = new RoomManager();
const room = rooms.createRoom(null);
rooms.addPlayer(room, 'Tester', { clientId: 'tester' });
rooms.fillWithBots(room);
room.status = 'playing';
room.ruleConfig = Rules.makeRuleConfig('HEART', false);
room.trickNumber = 2;
// The opening rule announcement must finish before anyone can play a card.
room.tableCards = [];
room.playIntroUntil = Date.now() + 4200;
assert.match(rooms.playCard(room, 'tester', 'S_A').error, /게임 시작 안내/);
assert.equal(room.tableCards.length, 0);
room.playIntroUntil = Date.now() - 1;
assert.doesNotMatch(rooms.playCard(room, 'tester', 'S_A').error || '', /게임 시작 안내/);
const entry = (index, c, options = {}) => ({ clientId: room.players[index].clientId, card: c, ...options });
const winner = () => rooms.publicState(room).tableWinningCardId;
room.tableCards = [];
assert.equal(winner(), null);
room.tableCards.push(entry(0, card('CLUB', 'K')));
assert.equal(winner(), null);
room.tableCards.push(entry(1, card('DIAMOND', 'A')));
assert.equal(winner(), 'C_K', 'off-suit ace does not beat the lead suit');
room.tableCards.push(entry(2, card('HEART', '2')));
assert.equal(winner(), 'H_2', 'trump changes the highlight');
room.tableCards.push(entry(3, makeJoker()));
assert.equal(winner(), 'JOKER');
room.trickNumber = 1;
assert.equal(winner(), 'H_2');
room.trickNumber = 10;
assert.equal(winner(), 'H_2');
room.tableCards.push(entry(4, card('SPADE', 'A')));
assert.equal(winner(), 'S_A', 'mighty always wins');
room.tableCards = [entry(0, card('CLUB', '3'), { jokerCallActivated: true }), entry(1, makeJoker())];
room.trickNumber = 4;
assert.equal(winner(), 'C_3', 'called joker is powerless');
room.ruleConfig = Rules.makeRuleConfig(null, true);
room.tableCards = [entry(0, makeJoker(), { declaredSuit: 'DIAMOND' }), entry(1, card('DIAMOND', 'K'))];
room.trickNumber = 1;
assert.equal(winner(), 'D_K');
// First-trick resolution increments the number before broadcasting its table.
room.trickComplete = true;
room.trickNumber = 2;
room.lastTrickWinner = { clientId: room.players[1].clientId };
assert.equal(winner(), 'D_K', 'resolved winner survives trick-number advancement');
room.trickComplete = false;
room.ruleConfig = Rules.makeRuleConfig('HEART', false);
const bot = room.players[1];
const originalRandom = Math.random;
try {
  for (const random of [0, 0.999]) {
    Math.random = () => random;
    for (const trick of [1, 10]) {
      room.trickNumber = trick;
      room.tableCards = [];
      bot.hand = [makeJoker(), card('CLUB', '2')];
      assert.equal(rooms.botPickPlay(room, bot).cardId, 'C_2');
    }
    room.trickNumber = 9;
    assert.equal(rooms.botPickPlay(room, bot).cardId, 'JOKER', 'spend joker before final trick');
    room.trickComplete = true; // New lead after trick 8 resolves.
    assert.equal(rooms.botPickPlay(room, bot).cardId, 'JOKER');
    room.trickComplete = false;
    room.trickNumber = 1;
    room.tableCards = [entry(0, card('CLUB', '3'), { jokerCallActivated: true })];
    assert.equal(rooms.botPickPlay(room, bot).cardId, 'JOKER', 'forced call still obeyed');
    room.tableCards = [];
    room.trickNumber = 10;
    bot.hand = [makeJoker()];
    assert.equal(rooms.botPickPlay(room, bot).cardId, 'JOKER', 'forced last card cannot deadlock');
  }
} finally { Math.random = originalRandom; }
console.log('Trick feedback: live winner, trump, mighty, joker exceptions, completed trick and bot timing passed.');
