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

room.targetScore = 16;
const r2 = Scoring.calculateResult(room);
assert.strictEqual(r2.winner, "defender");

// 미공개 카드 프렌드 → 주공 단독 (프렌드 점수 수비로)
room.friendRevealed = false;
room.friendClientId = null;
room.targetScore = 10;
const r3 = Scoring.calculateResult(room);
assert.strictEqual(r3.friendNickname, null);
assert.strictEqual(r3.declarerTeamScore, 8 + 1); // 주공 8 + kitty 1
assert.strictEqual(r3.defenderTeamScore, 6 + 2 + 1 + 2);

console.log("Scoring tests OK");
