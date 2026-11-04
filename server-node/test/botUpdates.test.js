const assert = require('node:assert/strict');
const Rooms = require('../src/RoomManager');
const Timer = require('../src/BotDealMissTimer');
const Bots = require('../src/game/BotPolicy');
const Rules = require('../src/game/RuleEngine');
const Deck = require('../src/game/Deck');
const cards = Object.fromEntries(Deck.createDeck().map(c => [c.id, c]));
const rooms = new Rooms();
const room = rooms.createRoom(null);
const human = rooms.addPlayer(room, 'Human', { clientId: 'human' }).player;
rooms.fillWithBots(room);
rooms.dealCards(room); room.status = 'bidding'; room.dealId = 1; rooms.startBidding(room);
const weak = ['S_2','S_3','S_4','S_5','H_2','H_3','H_4','D_2','D_3','C_2'].map(id => cards[id]);
human.hand = weak.slice();
room.currentBidderIndex = 3;
assert.ok(rooms.declareDealMiss(room, human.clientId).ok, 'human can declare out of turn');
room.bids.push({clientId: human.clientId, pass: true});
assert.ok(rooms.declareDealMiss(room, human.clientId).error, 'cannot declare after acting');
rooms.startBidding(room);
// Keep all other candidates strong, and put the weak bot last.
room.players.forEach(p => { p.hand = [cards.C_A, cards.H_A, cards.D_A]; });
const bot = room.players[4]; bot.hand = weak.slice();
const jobs = [];
let declarations = 0;
const timer = new Timer(rooms, (r,p) => {
  assert.equal(p, bot); assert.ok(rooms.declareDealMiss(r,p.clientId).ok);
  declarations++; rooms.dealCards(r); rooms.startBidding(r); r.dealId++;
}, { schedule: (fn,ms) => { const j={fn,ms}; jobs.push(j); return j; }, cancel:j=>{j.cancelled=true;} });
assert.ok(timer.arm(room)); assert.ok(timer.arm(room));
assert.equal(jobs.length,1); assert.equal(jobs[0].ms,3000);
assert.equal(rooms.currentBidder(room), human, 'bot need not have the bid turn');
jobs[0].fn(); jobs[0].fn();
assert.equal(declarations,1); assert.equal(room.dealId,2);
room.players.forEach(p => {p.hand=[cards.C_A,cards.H_A,cards.D_A];}); bot.hand=weak.slice();
timer.arm(room); const stale=jobs.at(-1); room.dealId++;
stale.fn(); assert.equal(declarations,1);
timer.arm(room); room.status='playing'; jobs.at(-1).fn(); assert.equal(declarations,1);
room.status='bidding'; room.tutorial={}; assert.equal(timer.arm(room),false); delete room.tutorial;

// Boundary values: opening shave, overcall reserve, lowered floors, maximum bid.
const evaluate = rooms._evaluateHandForBid;
rooms._evaluateHandForBid = () => ({suit:'HEART',tricks:14.49});
room.minBid=13; room.highestBid=null;
assert.equal(rooms.botDecideBid(room,human),null);
rooms._evaluateHandForBid = () => ({suit:'HEART',tricks:14.5});
assert.equal(rooms.botDecideBid(room,human).targetScore,13);
room.highestBid={targetScore:13};
rooms._evaluateHandForBid = () => ({suit:'HEART',tricks:16.49});
assert.equal(rooms.botDecideBid(room,human),null);
rooms._evaluateHandForBid = () => ({suit:'HEART',tricks:16.5});
assert.equal(rooms.botDecideBid(room,human).targetScore,14);
room.highestBid=null; room.minBid=11;
rooms._evaluateHandForBid = () => ({suit:'HEART',tricks:12.5});
assert.equal(rooms.botDecideBid(room,human).targetScore,11);
rooms._evaluateHandForBid=evaluate;

// Mighty filtering across all policies, including changed mighty under spades.
for (const trump of ['HEART','SPADE']) for (const policy of Bots.NAMES) {
  room.ruleConfig=Rules.makeRuleConfig(trump,false); room.trickNumber=4; room.trickComplete=false;
  room.friendType='none'; room.friendRevealed=true; room.declarerClientId=human.clientId;
  const mighty=cards[room.ruleConfig.mightyCardId];
  human.hand=[mighty,cards.C_2]; room.tableCards=[];
  assert.equal(Bots.pickPlay(room,human,policy).cardId,'C_2',policy+' saves mighty lead');
  room.tableCards=[{clientId:bot.clientId,card:cards.C_9}];
  assert.equal(Bots.pickPlay(room,human,policy).cardId,'C_2',policy+' saves low capture');
  human.hand=[mighty];
  assert.equal(Bots.pickPlay(room,human,policy).cardId,mighty.id,policy+' sole mighty allowed');
  human.hand=[mighty,cards.C_2];
  room.tableCards=[{clientId:bot.clientId,card:cards.C_A}];
  assert.equal(Bots.pickPlay(room,human,'GreedyBot').cardId,mighty.id,'two point capture allowed');
  human.hand=[mighty,cards.JOKER]; room.trickNumber=9; room.tableCards=[];
  assert.equal(Bots.pickPlay(room,human,policy).cardId,'JOKER','joker ninth priority preserved');
}
// Previous declarer survives the lobby and seat changes; missing player falls back.
room.declarerClientId=bot.clientId; room.targetScore=13;
rooms.finishGame(room);
rooms.returnToWaiting(room);
room.players.reverse(); rooms.dealCards(room); rooms.startBidding(room);
assert.equal(rooms.currentBidder(room).clientId,bot.clientId);
rooms.startBidding(room,12);
assert.equal(rooms.currentBidder(room).clientId,bot.clientId);
room.previousDeclarerClientId='departed'; rooms.startBidding(room);
assert.equal(rooms.currentBidder(room),room.players[0]);
console.log('Bot updates: out-of-turn deal miss, 3s timer/stale guards, bidding margins, mighty policy and previous declarer passed.');
