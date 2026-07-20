// 카드 한 장을 표현한다.
//
// 카드 데이터 구조:
//   { id: "S_A", suit: "SPADE", rank: "A", point: 1 }
//
//   - id:   무늬_숫자 조합의 고유 식별자 (조커는 "JOKER")
//   - suit: SPADE / HEART / DIAMOND / CLUB / JOKER
//   - rank: "2".."10", "J", "Q", "K", "A", "JOKER"
//   - point: 점수 카드면 1, 아니면 0 (마이티 점수 카드는 10,J,Q,K,A)

const SUITS = ["SPADE", "HEART", "DIAMOND", "CLUB"];
const RANKS = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"];

// 손패 표시용 무늬 순서 (♠ → ♥ → ♦ → ♣ → 조커)
const SUIT_SORT_ORDER = { SPADE: 0, HEART: 1, DIAMOND: 2, CLUB: 3, JOKER: 4 };
// 랭크: 높은 것부터 (A > K > ... > 2)
const RANK_SORT_ORDER = {};
RANKS.forEach((r, i) => { RANK_SORT_ORDER[r] = i; });
RANK_SORT_ORDER.JOKER = 99;

// 무늬별 짧은 접두어 (id 생성용)
const SUIT_PREFIX = {
  SPADE: "S",
  HEART: "H",
  DIAMOND: "D",
  CLUB: "C",
};

// 점수 카드 여부 (10, J, Q, K, A = 각 1점, 한 판 총 20점)
const POINT_RANKS = new Set(["10", "J", "Q", "K", "A"]);

function makeCard(suit, rank) {
  return {
    id: SUIT_PREFIX[suit] + "_" + rank,
    suit,
    rank,
    point: POINT_RANKS.has(rank) ? 1 : 0,
  };
}

function makeJoker() {
  return { id: "JOKER", suit: "JOKER", rank: "JOKER", point: 0 };
}

// 손패를 무늬 → 숫자(높은순)로 정렬. 원본 배열을 정렬해 반환.
function sortHand(cards) {
  if (!cards || cards.length === 0) return cards || [];
  cards.sort((a, b) => {
    const sa = SUIT_SORT_ORDER[a.suit] != null ? SUIT_SORT_ORDER[a.suit] : 9;
    const sb = SUIT_SORT_ORDER[b.suit] != null ? SUIT_SORT_ORDER[b.suit] : 9;
    if (sa !== sb) return sa - sb;
    const ra = RANK_SORT_ORDER[a.rank] != null ? RANK_SORT_ORDER[a.rank] : 0;
    const rb = RANK_SORT_ORDER[b.rank] != null ? RANK_SORT_ORDER[b.rank] : 0;
    return rb - ra; // 높은 랭크 먼저
  });
  return cards;
}

module.exports = {
  SUITS,
  RANKS,
  makeCard,
  makeJoker,
  sortHand,
};
