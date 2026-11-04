const assert = require('node:assert/strict');
const Bots = require('../src/game/BotPolicy');
const Rules = require('../src/game/RuleEngine');
const Deck = require('../src/game/Deck');
const RoomManager = require('../src/RoomManager');
const cards = Object.fromEntries(Deck.createDeck().map(c => [c.id, c]));
const entry = (clientId, id, extra = {}) => ({ clientId, card: cards[id], ...extra });
const room = {
  players: Array.from({ length: 5 }, (_, i) => ({ clientId: String(i), hand: [] })),
  ruleConfig: Rules.makeRuleConfig('HEART', false), trickNumber: 3,
  declarerClientId: '0', friendType: 'none', friendRevealed: true,
  tableCards: [], trickHistory: [],
};
const player = room.players[0];
player.hand = ['H_2', 'H_K', 'C_A', 'C_2', 'D_Q'].map(id => cards[id]);
const pick = name => Bots.pickPlay(room, player, name, () => 0).cardId;
assert.equal(pick('GreedyBot'), 'H_K');
assert.equal(pick('ScoreBot'), 'H_K');
assert.equal(pick('ConservativeBot'), 'C_2');
assert.equal(pick('AggressiveBot'), 'H_K');
assert.equal(pick('TrumpBot'), 'C_2');
room.friendType = 'card'; room.friendCardId = 'D_A'; room.friendRevealed = false;
assert.equal(pick('PartnerBot'), 'D_Q');
room.trickHistory = [{ table: [entry('1', 'H_3'), entry('2', 'C_4')] }];
assert.equal(pick('InformationBot'), 'C_A');
// Joker leads use their declared suit; special-card exceptions prove no void.
room.trickHistory.push({ table: [entry('1', 'JOKER', { declaredSuit: 'DIAMOND' }),
  entry('2', 'C_5'), entry('3', 'S_A')] });
assert.deepEqual([...Bots.voidSuits(room, '2')].sort(), ['DIAMOND', 'HEART']);
assert.deepEqual([...Bots.voidSuits(room, '3')], []);
room.trickHistory.push({ cards: [cards.C_A] }); // Older records can be ignored safely.
assert.equal(pick('InformationBot'), 'C_A');
// Hidden identities and opponent hands must not affect a decision.
const before = pick('PartnerBot');
room.friendClientId = '2';
for (const p of room.players.slice(1)) Object.defineProperty(p, 'hand', { get() { throw Error('private hand read'); } });
assert.equal(pick('PartnerBot'), before);
assert.equal(Bots.knownTeam(room, player, '2'), null);
room.friendClientId = '3';
assert.equal(Bots.knownTeam(room, player, '3'), null);
// Special-card conservation and real winner rules (including first/last joker).
room.tableCards = [entry('1', 'C_A')];
player.hand = [cards.JOKER, cards.D_2];
assert.equal(pick('SpecialCardBot'), 'D_2');
assert.equal(pick('GreedyBot'), 'JOKER');
room.trickNumber = 10;
assert.equal(pick('GreedyBot'), 'D_2');
for (const name of Bots.NAMES) {
  room.tableCards = [entry('1', 'C_3', { jokerCallActivated: true })];
  assert.equal(pick(name), 'JOKER');
  room.tableCards = [];
  player.hand = [cards.JOKER];
  assert.equal(Bots.pickPlay(room, player, name).declaredSuit, 'HEART');
  player.hand = [cards.JOKER, cards.D_2];
}

// Exercise each policy in every seat through all 50 server-validated plays.
for (const name of Bots.NAMES) {
  for (const trick of [1, 9, 10]) {
    room.trickNumber = trick;
    for (const table of [[], [entry('1', 'C_A')], [entry('1', 'S_A')]]) {
      room.tableCards = table;
      player.hand = [cards.JOKER, cards.S_A];
      assert.equal(pick(name), trick === 9 ? 'JOKER' : 'S_A', `${name}: trick ${trick}`);
      if (!table.length && trick === 9)
        assert.equal(Bots.pickPlay(room, player, name).declaredSuit, 'HEART');
    }
    room.tableCards = [entry('1', 'C_3', { jokerCallActivated: true })];
    assert.equal(pick(name), 'JOKER', `${name}: forced call on ${trick}`);
    room.tableCards = [];
    player.hand = [cards.JOKER];
    assert.equal(pick(name), 'JOKER', `${name}: sole card on ${trick}`);
  }
}

const rooms = new RoomManager();
{
  const r = rooms.createRoom(null);
  assert.ok(rooms.addBot(r, 'UnknownBot').error);
  const p = rooms.addBot(r, 'InformationBot').player;
  assert.equal(p.botPolicy, 'InformationBot');
  r.declarerClientId = p.clientId;
  r.ruleConfig = Rules.makeRuleConfig('HEART', false);
  p.hand = [cards.S_A, cards.JOKER];
  assert.equal(rooms.botFriendCardId(r), 'H_A');
  p.hand.push(cards.H_A);
  assert.equal(rooms.botFriendCardId(r), 'H_K');
  p.hand.push(cards.H_K, cards.H_Q, cards.H_J);
  assert.ok(['D_A', 'C_A'].includes(rooms.botFriendCardId(r)));
}
for (let game = 0; game < 80; game++) {
  const r = rooms.createRoom(null);
  rooms.fillWithBots(r);
  for (const p of r.players) assert.ok(Bots.NAMES.includes(p.botPolicy));
  r.players.forEach((p, seat) => { p.botPolicy = Bots.NAMES[(game + seat) % 8]; });
  rooms.dealCards(r);
  r.declarerClientId = r.players[game % 5].clientId;
  r.targetScore = 13;
  r.declaredTrump = ['SPADE', 'HEART', 'DIAMOND', 'CLUB', null][game % 5];
  r.ruleConfig = Rules.makeRuleConfig(r.declaredTrump, !r.declaredTrump);
  r.status = 'exchanging_kitty';
  rooms.startKittyExchange(r);
  assert.ok(rooms.discardKitty(r, r.declarerClientId, rooms.botPickDiscardIds(r)).ok);
  r.status = 'choosing_friend';
  assert.ok(rooms.chooseFriend(r, r.declarerClientId, { friendCardId: rooms.botFriendCardId(r) }).ok);
  r.status = 'playing'; rooms.startPlay(r);
  for (let turn = 0; turn < 50; turn++) {
    const p = rooms.currentTurnPlayer(r);
    const action = rooms.botPickPlay(r, p);
    const result = rooms.playCard(r, p.clientId, action.cardId, action);
    assert.ok(!result.error, result.error);
  }
  assert.ok(rooms.isHandOver(r));
  assert.equal(r.trickHistory.length, 10);
  for (const [i, trick] of r.trickHistory.entries()) {
    assert.equal(trick.trickNumber, i + 1);
    assert.equal(new Set(trick.table.map(t => t.clientId)).size, 5);
    assert.deepEqual(trick.table.map(t => t.card), trick.cards);
  }
  const last = r.trickHistory[9].table[0];
  const saved = last.card.id;
  r.tableCards[0].card = cards.JOKER;
  assert.equal(last.card.id, saved, 'history is an independent snapshot');
  const result = rooms.finishGame(r);
  assert.equal(result.declarerTeamScore + result.defenderTeamScore, 20);
}
console.log('Eight bot policies: public-information decisions, void inference, 80 complete games / 4,000 legal plays passed.');
