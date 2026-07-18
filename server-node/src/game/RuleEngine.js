// RuleEngine: 마이티 룰 판정 (표준룰 기준)
//
// 역할:
//   - makeRuleConfig(trumpSuit, noTrump): 이 판의 룰 설정 생성
//   - canPlayCard(...): 낼 수 있는 카드인지(따라내기 강제) 검증
//   - determineTrickWinner(...): 트릭 승자 판정 (마이티/조커/기루다/리드무늬)
//
// 표준룰 요약:
//   - 카드 서열(강→약): 마이티 > 조커 > 기루다(높은 랭크) > 리드무늬(높은 랭크) > 나머지(짐)
//   - 마이티: 기루다가 스페이드면 다이아 A, 아니면 스페이드 A
//   - 조커콜: 기루다가 클로버면 스페이드 3, 아니면 클로버 3
//   - 조커는 첫/마지막 트릭에서 효과 없음. 조커콜이 리드로 나오면 조커 효과 없음.
//   - 리드무늬 카드를 손에 가지고 있으면 반드시 리드무늬를 내야 함(마이티/조커는 예외).

const { RANKS } = require("./Card");

function rankValue(rank) {
  return RANKS.indexOf(rank); // 2가 0 ... A가 12, 없는 값(JOKER)은 -1
}

// 이 판의 룰 설정을 만든다.
//   trumpSuit: "SPADE"|"HEART"|"DIAMOND"|"CLUB"
//   noTrump: true면 노기루(기루다 없음)
function makeRuleConfig(trumpSuit, noTrump) {
  const t = noTrump ? null : trumpSuit;
  return {
    trumpSuit: t,
    noTrump: !!noTrump,
    // 마이티: 기루다 스페이드면 다이아 A, 그 외엔 스페이드 A
    mightyCardId: t === "SPADE" ? "D_A" : "S_A",
    jokerCardId: "JOKER",
    // 조커콜: 기루다 클로버면 스페이드 3, 그 외엔 클로버 3
    jokerCallCardId: t === "CLUB" ? "S_3" : "C_3",
    mustFollowSuit: true,
    revealFriendWhenPlayed: true,
  };
}

function isJoker(card, cfg) {
  return card.id === cfg.jokerCardId || card.suit === "JOKER";
}
function isMighty(card, cfg) {
  return card.id === cfg.mightyCardId;
}
function isJokerCall(card, cfg) {
  return card.id === cfg.jokerCallCardId;
}

// 이번 트릭의 리드 무늬. 아직 아무도 안 냈으면 null(=리드 차례).
// 조커가 리드로 나오면 따라낼 무늬가 없다고 보고 null 취급(단순화).
function leadSuitOf(tableCards, cfg) {
  if (!tableCards || tableCards.length === 0) return null;
  const lead = tableCards[0].card;
  if (isJoker(lead, cfg)) return null;
  return lead.suit;
}

// 낼 수 있는 카드인가?
//   { playerHand, card, tableCards, ruleConfig }
function canPlayCard({ playerHand, card, tableCards, ruleConfig }) {
  const cfg = ruleConfig;
  const leadSuit = leadSuitOf(tableCards, cfg);
  if (!leadSuit) return true; // 내가 리드면 아무거나
  if (!cfg.mustFollowSuit) return true;
  if (isJoker(card, cfg)) return true; // 조커는 아무 때나
  if (isMighty(card, cfg)) return true; // 마이티는 아무 때나
  // 리드 무늬 카드를 가지고 있으면 반드시 리드 무늬를 내야 함
  const hasLead = playerHand.some((c) => !isJoker(c, cfg) && c.suit === leadSuit);
  if (hasLead) return card.suit === leadSuit;
  return true; // 리드 무늬가 없으면 아무거나
}

// 트릭 승자의 clientId를 반환.
//   { tableCards: [{clientId, card}], ruleConfig, trickNumber, numTricks }
function determineTrickWinner({ tableCards, ruleConfig, trickNumber = 1, numTricks = 10 }) {
  const cfg = ruleConfig;
  const firstOrLast = trickNumber === 1 || trickNumber === numTricks;

  // 1) 마이티 (항상 최강)
  const mighty = tableCards.find((t) => isMighty(t.card, cfg));
  if (mighty) return mighty.clientId;

  const lead = tableCards[0];
  const jokerCalled = isJokerCall(lead.card, cfg); // 조커콜이 리드로 나왔는가

  // 2) 조커 (첫/마지막 트릭이 아니고, 조커콜이 리드로 안 나왔을 때만 효과)
  if (!firstOrLast && !jokerCalled) {
    const joker = tableCards.find((t) => isJoker(t.card, cfg));
    if (joker) return joker.clientId;
  }

  // 3) 리드 무늬 결정 (조커가 리드면 그 다음 실제 무늬)
  let leadSuit;
  if (isJoker(lead.card, cfg)) {
    const firstReal = tableCards.find((t) => !isJoker(t.card, cfg));
    leadSuit = firstReal ? firstReal.card.suit : null;
  } else {
    leadSuit = lead.card.suit;
  }

  // 4) 기루다 중 최고 랭크
  if (cfg.trumpSuit) {
    let best = null;
    for (const t of tableCards) {
      if (isJoker(t.card, cfg)) continue;
      if (t.card.suit !== cfg.trumpSuit) continue;
      if (!best || rankValue(t.card.rank) > rankValue(best.card.rank)) best = t;
    }
    if (best) return best.clientId;
  }

  // 5) 리드 무늬 중 최고 랭크
  let best = null;
  for (const t of tableCards) {
    if (isJoker(t.card, cfg)) continue;
    if (t.card.suit !== leadSuit) continue;
    if (!best || rankValue(t.card.rank) > rankValue(best.card.rank)) best = t;
  }
  if (best) return best.clientId;

  // 6) 해당 없음 → 리더
  return lead.clientId;
}

module.exports = {
  makeRuleConfig,
  canPlayCard,
  determineTrickWinner,
  leadSuitOf,
  isJoker,
  isMighty,
  isJokerCall,
};
