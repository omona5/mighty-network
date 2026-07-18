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

module.exports = {
  SUITS,
  RANKS,
  makeCard,
  makeJoker,
};
