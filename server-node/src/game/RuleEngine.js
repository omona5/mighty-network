// RuleEngine: 마이티 룰 판정 (표준 5마 기준)
//
// 역할:
//   - makeRuleConfig(trumpSuit, noTrump): 이 판의 룰 설정 생성
//   - canPlayCard(...): 낼 수 있는 카드인지(따라내기·조커콜 강제) 검증
//   - determineTrickWinner(...): 트릭 승자 판정 (마이티/조커/기루다/리드무늬)
//   - dealMissScore / canDeclareDealMiss: 딜미스(0.5점식) 판정
//
// 표준룰 요약:
//   - 카드 서열(강→약): 마이티 > 조커 > 기루다(높은 랭크) > 리드무늬(높은 랭크) > 나머지(짐)
//   - 마이티: 기루다가 스페이드면 다이아 A, 아니면 스페이드 A
//   - 조커콜: 기루다가 클로버면 스페이드 3, 아니면 클로버 3
//   - 조커는 첫/마지막 트릭에서 효과 없음. 조커콜이 ‘활성화’되어 리드되면 조커 효과 없음.
//   - 조커 리드 시 선언 무늬를 따라내야 함.
//   - 조커콜 카드 리드는 선택적으로 활성화. 활성 시 조커 보유자는 조커 강제.
//   - 리드무늬 카드를 손에 가지고 있으면 반드시 리드무늬를 내야 함(마이티/조커는 예외).

const { RANKS, SUITS } = require("./Card");

const VALID_SUITS = new Set(SUITS);

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

function isValidSuit(suit) {
  return typeof suit === "string" && VALID_SUITS.has(suit);
}

// 이번 트릭의 리드 무늬. 아직 아무도 안 냈으면 null(=리드 차례).
// 조커가 리드면 선언한 declaredSuit를 따른다.
function leadSuitOf(tableCards, cfg) {
  if (!tableCards || tableCards.length === 0) return null;
  const lead = tableCards[0];
  if (isJoker(lead.card, cfg)) {
    return isValidSuit(lead.declaredSuit) ? lead.declaredSuit : null;
  }
  return lead.card.suit;
}

function isJokerCallActivated(leadEntry, cfg) {
  if (!leadEntry || !leadEntry.card) return false;
  if (!isJokerCall(leadEntry.card, cfg)) return false;
  return !!leadEntry.jokerCallActivated;
}

// 낼 수 있는 카드인가?
//   { playerHand, card, tableCards, ruleConfig }
function canPlayCard({ playerHand, card, tableCards, ruleConfig }) {
  const cfg = ruleConfig;
  if (!tableCards || tableCards.length === 0) return true; // 리드면 아무거나(선언 필드는 playCard에서 검증)

  const lead = tableCards[0];

  // 조커콜 활성: 조커 보유자는 마이티 보유 여부와 관계없이 조커 강제.
  if (isJokerCallActivated(lead, cfg)) {
    const hasJoker = playerHand.some((c) => isJoker(c, cfg));
    if (hasJoker) {
      if (isJoker(card, cfg)) return true;
      return false;
    }
  }

  const leadSuit = leadSuitOf(tableCards, cfg);
  if (!leadSuit) return true; // 조커 리드인데 선언 무늬 없음(비정상) → 제한 없음
  if (!cfg.mustFollowSuit) return true;
  if (isJoker(card, cfg)) return true; // 조커는 아무 때나
  if (isMighty(card, cfg)) return true; // 마이티는 아무 때나
  // 리드 무늬 카드를 가지고 있으면 반드시 리드 무늬를 내야 함
  const hasLead = playerHand.some((c) => !isJoker(c, cfg) && c.suit === leadSuit);
  if (hasLead) return card.suit === leadSuit;
  return true; // 리드 무늬가 없으면 아무거나
}

// 트릭 승자의 clientId를 반환.
//   { tableCards: [{clientId, card, declaredSuit?, jokerCallActivated?}], ruleConfig, trickNumber, numTricks }
function determineTrickWinner({ tableCards, ruleConfig, trickNumber = 1, numTricks = 10 }) {
  const cfg = ruleConfig;
  const firstOrLast = trickNumber === 1 || trickNumber === numTricks;

  // 1) 마이티 (항상 최강)
  const mighty = tableCards.find((t) => isMighty(t.card, cfg));
  if (mighty) return mighty.clientId;

  const lead = tableCards[0];
  const jokerCalled = isJokerCallActivated(lead, cfg);

  // 2) 조커 (첫/마지막 트릭이 아니고, 조커콜이 활성 리드가 아닐 때만 효과)
  if (!firstOrLast && !jokerCalled) {
    const joker = tableCards.find((t) => isJoker(t.card, cfg));
    if (joker) return joker.clientId;
  }

  // 3) 리드 무늬 결정 (조커 리드면 선언 무늬, 없으면 다음 실제 무늬)
  let leadSuit = leadSuitOf(tableCards, cfg);
  if (!leadSuit && isJoker(lead.card, cfg)) {
    const firstReal = tableCards.find((t) => !isJoker(t.card, cfg));
    leadSuit = firstReal ? firstReal.card.suit : null;
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

// 딜미스 점수 (mightyfriend 5마식).
//   마이티(기본 ♠A)=0, 10=0.5, 조커=-1, 그 외 점수카드(A/K/Q/J)=1
//   기루다 결정 전이므로 마이티는 항상 S_A.
function dealMissScore(hand) {
  if (!hand || hand.length === 0) return Infinity;
  let score = 0;
  for (const c of hand) {
    if (c.suit === "JOKER" || c.id === "JOKER") {
      score -= 1;
      continue;
    }
    if (c.id === "S_A") continue; // 마이티 = 0
    if (c.rank === "10") {
      score += 0.5;
      continue;
    }
    if (c.rank === "A" || c.rank === "K" || c.rank === "Q" || c.rank === "J") {
      score += 1;
    }
  }
  return score;
}

// 딜미스 가능: 손패 점수가 0.5 이하
function canDeclareDealMiss(hand) {
  return dealMissScore(hand) <= 0.5;
}

module.exports = {
  makeRuleConfig,
  canPlayCard,
  determineTrickWinner,
  leadSuitOf,
  isJoker,
  isMighty,
  isJokerCall,
  isValidSuit,
  isJokerCallActivated,
  dealMissScore,
  canDeclareDealMiss,
};
