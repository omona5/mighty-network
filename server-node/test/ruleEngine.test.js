// RuleEngine 단순 테스트 (프레임워크 없이 node로 실행: `node test/ruleEngine.test.js`)
const assert = require("assert");
const RE = require("../src/game/RuleEngine");
const { makeCard, makeJoker } = require("../src/game/Card");

let passed = 0;
function test(name, fn) {
  try {
    fn();
    passed++;
    console.log("  ✓ " + name);
  } catch (e) {
    console.error("  ✗ " + name + "\n     " + e.message);
    process.exitCode = 1;
  }
}

// 편의: 테이블 엔트리 생성
function entry(id, card) {
  return { clientId: id, card };
}

console.log("RuleEngine 테스트");

// 기루다 HEART 기준 설정 (마이티=S_A, 조커콜=C_3)
const cfg = RE.makeRuleConfig("HEART", false);

test("makeRuleConfig: 기루 HEART면 마이티=S_A, 조커콜=C_3", () => {
  assert.strictEqual(cfg.mightyCardId, "S_A");
  assert.strictEqual(cfg.jokerCallCardId, "C_3");
  assert.strictEqual(cfg.trumpSuit, "HEART");
});

test("makeRuleConfig: 기루 SPADE면 마이티=D_A", () => {
  const c = RE.makeRuleConfig("SPADE", false);
  assert.strictEqual(c.mightyCardId, "D_A");
});

test("makeRuleConfig: 기루 CLUB면 조커콜=S_3", () => {
  const c = RE.makeRuleConfig("CLUB", false);
  assert.strictEqual(c.jokerCallCardId, "S_3");
});

// 1. 따라내기 강제
test("리드가 HEART이고 HEART가 있으면 SPADE 불가", () => {
  const hand = [makeCard("HEART", "5"), makeCard("SPADE", "K")];
  const table = [entry("A", makeCard("HEART", "9"))];
  const ok = RE.canPlayCard({ playerHand: hand, card: makeCard("SPADE", "K"), tableCards: table, ruleConfig: cfg });
  assert.strictEqual(ok, false);
});

test("리드가 HEART이고 HEART가 있으면 HEART는 가능", () => {
  const hand = [makeCard("HEART", "5"), makeCard("SPADE", "K")];
  const table = [entry("A", makeCard("HEART", "9"))];
  const ok = RE.canPlayCard({ playerHand: hand, card: makeCard("HEART", "5"), tableCards: table, ruleConfig: cfg });
  assert.strictEqual(ok, true);
});

test("리드 무늬가 손에 없으면 아무거나 가능", () => {
  const hand = [makeCard("SPADE", "K"), makeCard("CLUB", "2")];
  const table = [entry("A", makeCard("HEART", "9"))];
  const ok = RE.canPlayCard({ playerHand: hand, card: makeCard("SPADE", "K"), tableCards: table, ruleConfig: cfg });
  assert.strictEqual(ok, true);
});

test("리드 차례(테이블 비어있음)면 아무거나 가능", () => {
  const hand = [makeCard("SPADE", "K")];
  const ok = RE.canPlayCard({ playerHand: hand, card: makeCard("SPADE", "K"), tableCards: [], ruleConfig: cfg });
  assert.strictEqual(ok, true);
});

// 2. 기루다가 리드 무늬보다 강함
test("기루다(HEART) 2가 리드무늬(SPADE) K보다 강함", () => {
  const table = [
    entry("A", makeCard("SPADE", "K")), // 리드 (마이티 아님)
    entry("B", makeCard("HEART", "2")), // 기루다
    entry("C", makeCard("SPADE", "Q")),
    entry("D", makeCard("CLUB", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 3 });
  assert.strictEqual(winner, "B");
});

// 3. 마이티가 기루다보다 강함
test("마이티(S_A)가 기루다보다 강함", () => {
  const table = [
    entry("A", makeCard("HEART", "K")), // 리드=기루다
    entry("B", makeCard("SPADE", "A")), // 마이티
    entry("C", makeCard("HEART", "A")), // 기루다 최고
    entry("D", makeCard("CLUB", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 3 });
  assert.strictEqual(winner, "B");
});

// 4. 조커가 기루다보다 강함(효과 있을 때), 마이티보단 약함
test("조커가 기루다보다 강함(중간 트릭)", () => {
  const table = [
    entry("A", makeCard("HEART", "A")), // 기루다 최고
    entry("B", makeJoker()),            // 조커
    entry("C", makeCard("SPADE", "K")),
    entry("D", makeCard("CLUB", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 3 });
  assert.strictEqual(winner, "B");
});

test("조커는 마이티보다 약함", () => {
  const table = [
    entry("A", makeJoker()),
    entry("B", makeCard("SPADE", "A")), // 마이티
    entry("C", makeCard("HEART", "A")),
    entry("D", makeCard("CLUB", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 3 });
  assert.strictEqual(winner, "B");
});

// 5. 첫/마지막 트릭엔 조커 효과 없음
test("첫 트릭에서는 조커 효과 없음(기루다가 이김)", () => {
  const table = [
    entry("A", makeCard("HEART", "A")), // 기루다 최고
    entry("B", makeJoker()),            // 조커(효과 없음)
    entry("C", makeCard("HEART", "K")),
    entry("D", makeCard("CLUB", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 1 });
  assert.strictEqual(winner, "A");
});

// 6. 조커콜이 활성화되어 리드면 조커 효과 없음
test("조커콜(C_3) 활성 리드면 조커 효과 없음", () => {
  const table = [
    { clientId: "A", card: makeCard("CLUB", "3"), jokerCallActivated: true },
    entry("B", makeJoker()),            // 조커(효과 없음)
    entry("C", makeCard("CLUB", "K")),  // 리드무늬(클로버) 최고
    entry("D", makeCard("SPADE", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 4 });
  assert.strictEqual(winner, "C");
});

test("조커콜 카드라도 비활성이면 조커가 이김", () => {
  const table = [
    { clientId: "A", card: makeCard("CLUB", "3"), jokerCallActivated: false },
    entry("B", makeJoker()),
    entry("C", makeCard("CLUB", "K")),
    entry("D", makeCard("SPADE", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 4 });
  assert.strictEqual(winner, "B");
});

// 7. 리드무늬만 있을 때 최고 랭크
test("리드무늬 중 최고 랭크가 승자", () => {
  const table = [
    entry("A", makeCard("CLUB", "5")),
    entry("B", makeCard("CLUB", "K")),
    entry("C", makeCard("CLUB", "9")),
    entry("D", makeCard("SPADE", "A")), // 마이티 아님? S_A는 마이티! -> 주의
    entry("E", makeCard("DIAMOND", "9")),
  ];
  // 위 D는 마이티(S_A)라 이 케이스는 마이티가 이김
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 3 });
  assert.strictEqual(winner, "D");
});

// 8. 조커 리드 시 선언 무늬 따라내기
test("조커 리드 + declaredSuit=HEART면 HEART를 따라내야 함", () => {
  const hand = [makeCard("SPADE", "K"), makeCard("HEART", "5")];
  const table = [{ clientId: "A", card: makeJoker(), declaredSuit: "HEART" }];
  assert.strictEqual(
    RE.canPlayCard({ playerHand: hand, card: makeCard("SPADE", "K"), tableCards: table, ruleConfig: cfg }),
    false
  );
  assert.strictEqual(
    RE.canPlayCard({ playerHand: hand, card: makeCard("HEART", "5"), tableCards: table, ruleConfig: cfg }),
    true
  );
});

test("조커 리드(중간트릭)면 조커가 승자", () => {
  // 조커가 중간 트릭에서 리드 → 조커가 최강이므로 조커 승
  const table = [
    { clientId: "A", card: makeJoker(), declaredSuit: "CLUB" },
    entry("B", makeCard("CLUB", "A")),
    entry("C", makeCard("CLUB", "K")),
    entry("D", makeCard("SPADE", "5")),
    entry("E", makeCard("DIAMOND", "9")),
  ];
  const winner = RE.determineTrickWinner({ tableCards: table, ruleConfig: cfg, trickNumber: 4 });
  assert.strictEqual(winner, "A");
});

// 9. 조커콜 활성 시 조커 강제
test("조커콜 활성 시 조커 보유자는 조커(또는 마이티)만 가능", () => {
  const hand = [makeJoker(), makeCard("CLUB", "K"), makeCard("SPADE", "A")];
  const table = [{ clientId: "A", card: makeCard("CLUB", "3"), jokerCallActivated: true }];
  assert.strictEqual(
    RE.canPlayCard({ playerHand: hand, card: makeCard("CLUB", "K"), tableCards: table, ruleConfig: cfg }),
    false
  );
  assert.strictEqual(
    RE.canPlayCard({ playerHand: hand, card: makeJoker(), tableCards: table, ruleConfig: cfg }),
    true
  );
  assert.strictEqual(
    RE.canPlayCard({ playerHand: hand, card: makeCard("SPADE", "A"), tableCards: table, ruleConfig: cfg }),
    true
  );
});

// 10. 딜미스 0.5점식
test("딜미스: 빈 점수패 = 0 → 가능", () => {
  const hand = [makeCard("SPADE", "2"), makeCard("HEART", "3"), makeCard("CLUB", "4")];
  assert.strictEqual(RE.dealMissScore(hand), 0);
  assert.strictEqual(RE.canDeclareDealMiss(hand), true);
});

test("딜미스: 마이티(S_A)=0, 조커=-1 → 가능", () => {
  const hand = [makeCard("SPADE", "A"), makeJoker(), makeCard("HEART", "2")];
  assert.strictEqual(RE.dealMissScore(hand), -1);
  assert.strictEqual(RE.canDeclareDealMiss(hand), true);
});

test("딜미스: 10 한 장 = 0.5 → 가능", () => {
  const hand = [makeCard("HEART", "10"), makeCard("CLUB", "2")];
  assert.strictEqual(RE.dealMissScore(hand), 0.5);
  assert.strictEqual(RE.canDeclareDealMiss(hand), true);
});

test("딜미스: K 한 장 = 1 → 불가", () => {
  const hand = [makeCard("HEART", "K"), makeCard("CLUB", "2")];
  assert.strictEqual(RE.dealMissScore(hand), 1);
  assert.strictEqual(RE.canDeclareDealMiss(hand), false);
});

test("딜미스: 10 + 조커 = -0.5 → 가능", () => {
  const hand = [makeCard("HEART", "10"), makeJoker()];
  assert.strictEqual(RE.dealMissScore(hand), -0.5);
  assert.strictEqual(RE.canDeclareDealMiss(hand), true);
});

console.log(`\n통과: ${passed}개`);
