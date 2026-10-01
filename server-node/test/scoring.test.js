// Scoring 단위 테스트
const assert = require("assert");
const Scoring = require("../src/game/Scoring");

function c(id, point) {
  return { id, point };
}

assert.strictEqual(Scoring.scoreOfCards([c("S_A", 1), c("H_2", 0), c("D_K", 1)]), 2);
assert.strictEqual(Scoring.scoreOfCards([]), 0);

const room = {
  declarerClientId: "D",
  targetScore: 14,
  friendType: "card",
  friendRevealed: true,
  friendClientId: "F",
  friendCardId: "S_A",
  declaredTrump: "HEART",
  noTrump: false,
  kitty: [c("C_10", 1), c("H_3", 0), c("D_2", 0)], // kitty 1점
  players: [
    { clientId: "D", nickname: "주공", isBot: false, wonCards: [c("S_A", 1), c("S_K", 1), c("S_Q", 1), c("S_J", 1), c("S_10", 1), c("H_A", 1), c("H_K", 1), c("H_Q", 1)] }, // 8
    { clientId: "F", nickname: "프렌드", isBot: true, wonCards: [c("D_A", 1), c("D_K", 1), c("D_Q", 1), c("D_J", 1), c("D_10", 1), c("C_A", 1)] }, // 6
    { clientId: "A", nickname: "야1", isBot: true, wonCards: [c("C_K", 1), c("C_Q", 1)] }, // 2
    { clientId: "B", nickname: "야2", isBot: true, wonCards: [c("C_J", 1)] }, // 1
    { clientId: "C", nickname: "야3", isBot: true, wonCards: [c("H_J", 1), c("H_10", 1)] }, // 2
  ],
};
// 주공팀 8+6+kitty1=15 >= 14 → 주공 승, 수비 2+1+2=5
const r = Scoring.calculateResult(room);
assert.strictEqual(r.declarerTeamScore, 15);
assert.strictEqual(r.defenderTeamScore, 5);
assert.strictEqual(r.kittyScore, 1);
assert.strictEqual(r.winner, "declarer");
assert.strictEqual(r.isRun, false);
assert.strictEqual(r.isBackrun, false);
assert.strictEqual(r.multiplier, 1);
assert.strictEqual(r.stakeBase, 5); // 15-10
assert.strictEqual(r.stakeTotal, 5);

room.targetScore = 16;
const r2 = Scoring.calculateResult(room);
assert.strictEqual(r2.winner, "defender");
assert.strictEqual(r2.stakeBase, 1); // 16-15

// 미공개 카드 프렌드 → 주공 단독 (프렌드 점수 수비로)
room.friendRevealed = false;
room.friendClientId = null;
room.targetScore = 10;
const r3 = Scoring.calculateResult(room);
assert.strictEqual(r3.friendNickname, null);
assert.strictEqual(r3.declarerTeamScore, 8 + 1); // 주공 8 + kitty 1
assert.strictEqual(r3.defenderTeamScore, 6 + 2 + 1 + 2);

// 백런: 주공팀 10점 이하
assert.strictEqual(r3.isBackrun, true); // 9점
assert.strictEqual(r3.multiplier, 2);
assert.ok(r3.multipliers.includes("백런"));

// 런: 주공팀 20점
const runRoom = {
  declarerClientId: "D",
  targetScore: 14,
  friendType: "none",
  friendRevealed: true,
  friendClientId: null,
  declaredTrump: null,
  noTrump: true,
  discardedKitty: [c("C_10", 1), c("H_A", 1), c("D_K", 1)], // 3
  players: [
    { clientId: "D", nickname: "주공", isBot: false, wonCards: Array.from({ length: 17 }, (_, i) => c("X" + i, 1)) }, // 17 + kitty 3 = 20
    { clientId: "A", nickname: "야1", isBot: true, wonCards: [] },
    { clientId: "B", nickname: "야2", isBot: true, wonCards: [] },
    { clientId: "C", nickname: "야3", isBot: true, wonCards: [] },
    { clientId: "E", nickname: "야4", isBot: true, wonCards: [] },
  ],
};
const rRun = Scoring.calculateResult(runRoom);
assert.strictEqual(rRun.declarerTeamScore, 20);
assert.strictEqual(rRun.isRun, true);
assert.strictEqual(rRun.winner, "declarer");
// 런×2 + 노기루×2 + 노프렌드×2 = 8
assert.strictEqual(rRun.multiplier, 8);
assert.deepStrictEqual(rRun.multipliers.sort(), ["노기루", "노프렌드", "런"].sort());
assert.strictEqual(rRun.stakeBase, 10); // 20-10
assert.strictEqual(rRun.stakeTotal, 80);
// 노프렌드 승: 주공 +4*80, 야당 각 -80 → 영합
assert.strictEqual(rRun.deltas.D, 320);
assert.strictEqual(rRun.deltas.A, -80);
assert.strictEqual(Object.values(rRun.deltas).reduce((a, b) => a + b, 0), 0);

// 세션 누적
rRun.deltas = rRun.deltas; // already set
Scoring.applySessionScores(runRoom, rRun);
assert.strictEqual(runRoom.players[0].sessionScore, 320);
assert.strictEqual(rRun.scoreboard[0].nickname, "주공");

// 프렌드 있는 승 영합: unit=5, 주공+10 프렌드+5 야당 각-5
const rFriend = Scoring.calculateResult({
  ...room,
  friendRevealed: true,
  friendClientId: "F",
  friendType: "card",
  targetScore: 14,
  noTrump: false,
});
assert.strictEqual(rFriend.stakeTotal, 5);
assert.strictEqual(rFriend.deltas.D, 10);
assert.strictEqual(rFriend.deltas.F, 5);
assert.strictEqual(rFriend.deltas.A + rFriend.deltas.B + rFriend.deltas.C, -15);
assert.strictEqual(Object.values(rFriend.deltas).reduce((a, b) => a + b, 0), 0);

console.log("Scoring tests OK");

// IDs, not nickname/seat/bot ownership, define teams. Defender points never leak.
for (const [type, revealed, id, expectedFriend] of [
  ['player', true, 'F', 'F'], ['card', true, 'F', 'F'],
  ['card', false, 'F', null], ['none', true, 'F', null],
  ['card', true, 'D', null], ['card', true, 'missing', null],
]) {
  const testRoom = { ...room, friendType: type, friendRevealed: revealed, friendClientId: id,
    discardedKitty: [c('kitty', 1)],
    players: room.players.map(p => ({ ...p, nickname: 'Same name' })).reverse() };
  const result = Scoring.calculateResult(testRoom);
  assert.deepEqual(result.declarerTeam.map(p => p.clientId).sort(), expectedFriend ? ['D', 'F'] : ['D']);
  assert.equal(result.declarerTeamScore, 9 + (expectedFriend ? 6 : 0));
  assert.equal(result.defenderTeamScore, expectedFriend ? 5 : 11);
  assert.equal(result.declarerTeamScore + result.defenderTeamScore, 20);
  assert.equal(result.friendClientId, expectedFriend);
  assert.equal(Scoring.liveTeamScores(testRoom).declarerTeamScore, result.declarerTeamScore - result.kittyScore);
  assert.equal(Scoring.liveTeamScores(testRoom, 'D').declarerTeamScore, result.declarerTeamScore);
  for (const defender of result.defenderTeam) {
    const copy = structuredClone(testRoom);
    copy.players.find(p => p.clientId === defender.clientId).wonCards.push(c('extra', 1));
    assert.equal(Scoring.calculateResult(copy).declarerTeamScore, result.declarerTeamScore);
  }
}
console.log('Team isolation: card/player/no/self/missing friend, duplicate names and shuffled seats passed.');
