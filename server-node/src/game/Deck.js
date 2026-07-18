// 카드 덱 생성 / 셔플 / 배분
//
// 마이티는 트럼프 52장 + 조커 1장 = 53장을 사용한다.
// 5명에게 10장씩(=50장) 나누고, 남은 3장은 바닥패(kitty)가 된다.

const { SUITS, RANKS, makeCard, makeJoker } = require("./Card");

const HAND_SIZE = 10; // 플레이어당 손패 장수
const NUM_PLAYERS = 5;

// 53장 덱 생성 (조커 포함)
function createDeck(includeJoker = true) {
  const deck = [];
  for (const suit of SUITS) {
    for (const rank of RANKS) {
      deck.push(makeCard(suit, rank));
    }
  }
  if (includeJoker) {
    deck.push(makeJoker());
  }
  return deck;
}

// Fisher-Yates 셔플 (제자리에서 섞고 같은 배열 반환)
function shuffle(deck) {
  for (let i = deck.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [deck[i], deck[j]] = [deck[j], deck[i]];
  }
  return deck;
}

// 셔플된 덱을 5명에게 10장씩 나누고 바닥패 3장을 남긴다.
// 반환: { hands: [ [10장], x5 ], kitty: [3장] }
function deal(deck) {
  const hands = [];
  let index = 0;
  for (let p = 0; p < NUM_PLAYERS; p++) {
    hands.push(deck.slice(index, index + HAND_SIZE));
    index += HAND_SIZE;
  }
  const kitty = deck.slice(index); // 남은 카드 (보통 3장)
  return { hands, kitty };
}

// 편의 함수: 새 덱을 만들고 섞어서 배분까지 한 번에
function createShuffledDeal() {
  const deck = shuffle(createDeck(true));
  return deal(deck);
}

module.exports = {
  HAND_SIZE,
  NUM_PLAYERS,
  createDeck,
  shuffle,
  deal,
  createShuffledDeal,
};
