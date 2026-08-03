// RoomManager: 방(room)들을 메모리에서 관리한다.
//
// 순수 WebSocket이라 socket.io의 방(room) 기능이 없으므로,
// "어떤 방에 누가 있는지"를 여기서 직접 관리한다.
//
// 방 데이터 구조:
//   {
//     roomId: "ABCD",
//     password: null | "1234",
//     status: "waiting" | "playing" | ...,
//     players: [ Player, ... ]
//   }
//
// Player 데이터 구조:
//   {
//     clientId,          // ws 연결 식별자 (내부용)
//     nickname,
//     isReady,
//     reconnectToken,    // 재접속 복구용 비밀 토큰 (본인에게만 전달)
//     connected,         // 현재 연결 상태
//     disconnectedAt,    // 끊긴 시각(ms). 재접속 유예 만료에 사용
//     ws                 // WebSocket 참조 (브로드캐스트용, 외부로 노출 금지)
//   }

const crypto = require("crypto");
const Deck = require("./game/Deck");
const RuleEngine = require("./game/RuleEngine");
const Scoring = require("./game/Scoring");
const { sortHand } = require("./game/Card");

const MAX_PLAYERS = 5;
const NUM_TRICKS = 10;
// 끊김 후 재접속 허용 시간. 그동안 봇이 대신 플레이한다.
const RECONNECT_GRACE_MS = 5 * 60 * 1000;

// 입찰(공약) 범위
const MIN_BID = 13; // 입찰 시작 최소 공약
const MAX_BID = 20;
const BID_FLOOR = 11; // 전원 패스 시 최소공약을 여기까지 낮춘다. 이보다 낮아지면 재딜.
const SUITS = ["SPADE", "HEART", "DIAMOND", "CLUB"];

// 폴백 기루다: 입찰에서 아무도 안 정해졌을 때 대비용 기본값(정상 흐름에선 미사용)
const FALLBACK_TRUMP_SUIT = "HEART";

// 봇 clientId 중복 방지용 전역 카운터
let botSeq = 0;

// 4자리 대문자/숫자 방 코드 생성 (헷갈리는 0/O, 1/I 제외)
function makeRoomId() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let id = "";
  for (let i = 0; i < 4; i++) {
    id += chars[Math.floor(Math.random() * chars.length)];
  }
  return id;
}

// 재접속용 비밀 토큰
function makeToken() {
  return crypto.randomBytes(16).toString("hex");
}

class RoomManager {
  constructor() {
    this.rooms = {};
  }

  getRoom(roomId) {
    return this.rooms[roomId];
  }

  // 새 방 생성 (생성자는 아직 입장 전 상태; addPlayer로 입장시킨다)
  createRoom(password) {
    let roomId;
    do {
      roomId = makeRoomId();
    } while (this.rooms[roomId]);

    const room = {
      roomId,
      password: password || null,
      status: "waiting",
      players: [],
      hostClientId: null, // 방장(호스트) - 첫 입장자가 됨, 나가면 다음 사람이 승계
    };
    this.rooms[roomId] = room;
    return room;
  }

  // 방에 플레이어 추가. 실패 시 { error } 반환, 성공 시 { player } 반환.
  // 닉네임이 겹치면 동건2, 동건3… 형태로 자동 부여 (최대 12자).
  addPlayer(room, nickname, ws) {
    if (room.players.length >= MAX_PLAYERS) {
      return { error: "방이 가득 찼습니다. (최대 5명)" };
    }
    if (room.status !== "waiting") {
      return { error: "이미 시작된 방에는 입장할 수 없습니다." };
    }

    const uniqueName = this.uniqueNickname(room, nickname);

    const player = {
      clientId: ws.clientId,
      nickname: uniqueName,
      isReady: false,
      reconnectToken: makeToken(),
      connected: true,
      disconnectedAt: null,
      isBot: false,
      sessionScore: 0,
      ws,
    };
    // 사람은 항상 봇들보다 앞에 배치한다. (사람들 다음, 첫 봇 앞에 삽입)
    // 목록이 [사람...][봇...] 순서로 유지되도록 함.
    const insertIndex = room.players.filter((p) => !p.isBot).length;
    room.players.splice(insertIndex, 0, player);
    // 방에 방장이 없으면(첫 입장자) 이 사람이 방장이 된다.
    if (!room.hostClientId) {
      room.hostClientId = player.clientId;
    }
    return { player, renamed: uniqueName !== nickname };
  }

  // 방 안 기존 닉네임(사람+봇)과 겹치지 않는 이름을 만든다.
  uniqueNickname(room, base) {
    const raw = (base || "").trim();
    const taken = new Set(room.players.map((p) => p.nickname));
    if (raw && !taken.has(raw) && raw.length <= 12) return raw;

    const stem = (raw || "플레이어").slice(0, 12);
    for (let n = 2; n < 100; n++) {
      const suffix = String(n);
      const maxStem = 12 - suffix.length;
      const candidate = stem.slice(0, Math.max(1, maxStem)) + suffix;
      if (!taken.has(candidate)) return candidate;
    }
    // 극히 드문 경우: 짧은 랜덤 접미
    return (stem.slice(0, 8) + makeToken().slice(0, 4)).slice(0, 12);
  }

  // 봇(자동 플레이어)을 방에 추가한다. 빈자리를 채워 5명을 맞추는 용도.
  // 봇은 항상 준비 상태이며 ws 연결이 없다.
  addBot(room) {
    if (room.status !== "waiting") {
      return { error: "대기 중일 때만 봇을 추가할 수 있습니다." };
    }
    if (room.players.length >= MAX_PLAYERS) {
      return { error: "자리가 가득 찼습니다. (최대 5명)" };
    }
    const botNumber = room.players.filter((p) => p.isBot).length + 1;
    const player = {
      clientId: "BOT" + ++botSeq,
      nickname: this.uniqueNickname(room, "봇" + botNumber),
      isReady: true, // 봇은 항상 준비 완료
      reconnectToken: null,
      connected: true,
      disconnectedAt: null,
      isBot: true,
      sessionScore: 0,
      ws: null,
    };
    room.players.push(player);
    return { player };
  }

  // 방에서 마지막 봇 하나를 제거한다. 성공하면 true.
  removeBot(room) {
    if (room.status !== "waiting") return false;
    for (let i = room.players.length - 1; i >= 0; i--) {
      if (room.players[i].isBot) {
        room.players.splice(i, 1);
        return true;
      }
    }
    return false;
  }

  // 방의 사람(비봇) 수
  humanCount(room) {
    return room.players.filter((p) => !p.isBot).length;
  }

  // 대기 중인 방을 봇으로 채워 항상 5명이 되도록 유지한다.
  fillWithBots(room) {
    while (room.status === "waiting" && room.players.length < MAX_PLAYERS) {
      const result = this.addBot(room);
      if (result.error) break;
    }
  }

  // 카드를 섞어 방의 5명에게 10장씩 배분하고, 바닥패 3장을 방에 보관한다.
  // 각 플레이어에는 p.hand(카드 배열)가 채워진다.
  dealCards(room) {
    const { hands, kitty } = Deck.createShuffledDeal();
    room.players.forEach((p, i) => {
      p.hand = sortHand(hands[i] || []);
    });
    room.kitty = kitty; // 바닥패 (아직 아무에게도 공개 안 함)
    return { hands, kitty };
  }

  // ===================== 09단계: 입찰 / 주공 / 프렌드 =====================

  // 입찰 시작 세팅 (한 바퀴 단판 입찰: 각자 한 번씩 공약 또는 패스)
  // startMinBid: 이번 입찰 라운드의 최소 공약(전원 패스로 낮춰진 경우 그 값)
  startBidding(room, startMinBid = MIN_BID) {
    room.minBid = startMinBid;
    room.currentBidderIndex = 0;
    room.highestBid = null; // { clientId, nickname, targetScore, trumpSuit, noTrump }
    room.bids = [];
    room.declarerClientId = null;
    room.declaredTrump = undefined;
    room.noTrump = false;
    room.targetScore = null;
    room.friendCardId = undefined; // 지정 전 undefined, 노프렌드/플레이어면 null
    room.friendClientId = null;
    room.friendType = undefined; // 지정 전 undefined, "card"|"player"|"none"
    room.friendRevealed = false;
    room.discardedKitty = null; // 주공이 버린 3장 (점수 = 주공팀)
  }

  // ===================== 바닥패 교환 =====================

  // 주공 손패에 바닥패 3장을 합친다. (10 → 13장)
  startKittyExchange(room) {
    const decl = room.players.find((p) => p.clientId === room.declarerClientId);
    if (!decl) return { error: "주공을 찾을 수 없습니다." };
    const kitty = room.kitty || [];
    decl.hand = sortHand((decl.hand || []).concat(kitty));
    room.kitty = []; // 교환 중에는 손패로 이동
    room.discardedKitty = null;
    return { ok: true, handCount: decl.hand.length };
  }

  // 주공이 손패에서 3장을 버린다. 버린 카드의 점수는 주공팀 점수에 포함.
  discardKitty(room, clientId, cardIds) {
    if (room.status !== "exchanging_kitty") {
      return { error: "지금은 바닥패를 버릴 수 없습니다." };
    }
    if (clientId !== room.declarerClientId) {
      return { error: "주공만 바닥패를 버릴 수 있습니다." };
    }
    const ids = Array.isArray(cardIds) ? cardIds.map((x) => String(x).trim().toUpperCase()) : [];
    if (ids.length !== 3) return { error: "정확히 3장을 버려야 합니다." };
    if (new Set(ids).size !== 3) return { error: "서로 다른 카드 3장을 선택하세요." };

    const decl = room.players.find((p) => p.clientId === clientId);
    if (!decl || !decl.hand) return { error: "주공 손패를 찾을 수 없습니다." };

    const discarded = [];
    for (const id of ids) {
      const idx = decl.hand.findIndex((c) => c.id === id);
      if (idx === -1) return { error: "손패에 없는 카드입니다: " + id };
      discarded.push(decl.hand[idx]);
      decl.hand.splice(idx, 1);
    }
    if (decl.hand.length !== 10) {
      return { error: "버리기 후 손패는 10장이어야 합니다. (현재 " + decl.hand.length + ")" };
    }
    room.discardedKitty = discarded;
    room.kitty = discarded; // 점수 계산용 (묻힌 카드)
    sortHand(decl.hand);
    return { ok: true, discarded, hand: decl.hand };
  }

  // 봇이 버릴 3장 선택: 기루다/마이티/조커/점수카드를 최대한 남기고 약한 카드부터
  botPickDiscardIds(room) {
    const decl = room.players.find((p) => p.clientId === room.declarerClientId);
    if (!decl || !decl.hand) return [];
    const trump = room.declaredTrump;
    const mighty = room.ruleConfig ? room.ruleConfig.mightyCardId : "S_A";
    const scored = decl.hand.map((c) => {
      let score = 0;
      if (c.id === mighty) score += 100;
      if (c.suit === "JOKER") score += 90;
      if (trump && c.suit === trump) score += 40;
      if (c.point) score += 20;
      const rankOrder = { "2": 1, "3": 2, "4": 3, "5": 4, "6": 5, "7": 6, "8": 7, "9": 8, "10": 9, J: 10, Q: 11, K: 12, A: 13, JOKER: 14 };
      score += (rankOrder[c.rank] || 0) * 0.1;
      return { id: c.id, score };
    });
    scored.sort((a, b) => a.score - b.score);
    return scored.slice(0, 3).map((x) => x.id);
  }

  currentBidder(room) {
    if (room.currentBidderIndex == null) return null;
    return room.players[room.currentBidderIndex] || null;
  }

  // 다음 입찰자로. 모두 한 번씩 했으면 true(완료) 반환.
  _advanceBidder(room) {
    room.currentBidderIndex++;
    return room.currentBidderIndex >= room.players.length;
  }

  // 공약 제출. 성공 { ok, complete }, 실패 { error }.
  placeBid(room, clientId, bid) {
    const bidder = room.players[room.currentBidderIndex];
    if (!bidder || bidder.clientId !== clientId) {
      return { error: "당신의 입찰 차례가 아닙니다." };
    }
    const targetScore = parseInt(bid && bid.targetScore, 10);
    const roundMin = room.minBid || MIN_BID;
    if (isNaN(targetScore) || targetScore < roundMin || targetScore > MAX_BID) {
      return { error: `공약은 ${roundMin}~${MAX_BID} 사이여야 합니다.` };
    }
    const minAllowed = room.highestBid ? room.highestBid.targetScore + 1 : roundMin;
    if (targetScore < minAllowed) {
      return { error: `현재 최고 공약보다 높아야 합니다. (최소 ${minAllowed})` };
    }
    const noTrump = !!(bid && bid.noTrump);
    if (!noTrump && !SUITS.includes(bid && bid.trumpSuit)) {
      return { error: "기루다를 선택하세요." };
    }
    room.highestBid = {
      clientId,
      nickname: bidder.nickname,
      targetScore,
      trumpSuit: noTrump ? null : bid.trumpSuit,
      noTrump,
    };
    room.bids.push({ ...room.highestBid, pass: false });
    return { ok: true, complete: this._advanceBidder(room) };
  }

  // 패스. 성공 { ok, complete }, 실패 { error }.
  passBid(room, clientId) {
    const bidder = room.players[room.currentBidderIndex];
    if (!bidder || bidder.clientId !== clientId) {
      return { error: "당신의 입찰 차례가 아닙니다." };
    }
    room.bids.push({ clientId, nickname: bidder.nickname, pass: true });
    return { ok: true, complete: this._advanceBidder(room) };
  }

  // 입찰 마감 후 주공/기루다/목표점 확정.
  // 아무도 공약 안 했으면: 최소공약을 아직 낮출 수 있으면 { lowerBid, newMin }, 바닥이면 { redeal }.
  resolveBidding(room) {
    if (!room.highestBid) {
      const cur = room.minBid || MIN_BID;
      if (cur > BID_FLOOR) return { lowerBid: true, newMin: cur - 1 };
      return { redeal: true };
    }
    const h = room.highestBid;
    const decl = room.players.find((p) => p.clientId === h.clientId);
    room.declarerClientId = h.clientId;
    room.declaredTrump = h.noTrump ? null : h.trumpSuit;
    room.noTrump = h.noTrump;
    room.targetScore = h.targetScore;
    // 이 판의 룰 설정을 확정 (마이티/조커콜이 기루다에 따라 결정됨)
    room.ruleConfig = RuleEngine.makeRuleConfig(room.declaredTrump, room.noTrump);
    return {
      declarerClientId: h.clientId,
      declarerNickname: decl ? decl.nickname : "",
      targetScore: h.targetScore,
      trumpSuit: room.declaredTrump,
      noTrump: h.noTrump,
    };
  }

  // 주공이 프렌드를 지정.
  // data: { friendCardId } 카드 프렌드 / { friendClientId } 플레이어 프렌드 / { friendCardId:"NONE" } 노프렌드
  // 빈 값·미지정은 오류 (노프렌드는 반드시 "NONE"을 명시해야 함)
  chooseFriend(room, clientId, data) {
    if (clientId !== room.declarerClientId) {
      return { error: "주공만 프렌드를 지정할 수 있습니다." };
    }
    const rawCard = data && data.friendCardId != null ? String(data.friendCardId).trim() : "";
    const rawPlayer = data && data.friendClientId != null ? String(data.friendClientId).trim() : "";
    const isNone = rawCard.toUpperCase() === "NONE";
    const hasCard = rawCard !== "" && !isNone;
    const hasPlayer = rawPlayer !== "";

    if (!isNone && !hasCard && !hasPlayer) {
      return { error: "프렌드를 지정하세요. (카드 / 플레이어 / 노프렌드)" };
    }
    if (hasCard && hasPlayer) {
      return { error: "카드 프렌드와 플레이어 프렌드는 동시에 지정할 수 없습니다." };
    }

    if (isNone) {
      room.friendCardId = null;
      room.friendClientId = null;
      room.friendType = "none";
      room.friendRevealed = true;
      return { ok: true, friendType: "none" };
    }

    if (hasPlayer) {
      if (rawPlayer === room.declarerClientId) {
        return { error: "주공 자신을 프렌드로 지정할 수 없습니다." };
      }
      const p = room.players.find((x) => x.clientId === rawPlayer);
      if (!p) return { error: "해당 플레이어를 찾을 수 없습니다." };
      room.friendCardId = null;
      room.friendClientId = rawPlayer;
      room.friendType = "player";
      room.friendRevealed = true; // 플레이어 지정은 즉시 공개
      return { ok: true, friendType: "player", friendNickname: p.nickname };
    }

    const cardId = rawCard.toUpperCase();
    room.friendCardId = cardId;
    const owner = room.players.find((p) =>
      (p.hand || []).some((c) => c.id === cardId)
    );
    room.friendClientId = owner ? owner.clientId : null;
    room.friendType = "card";
    room.friendRevealed = false;
    return { ok: true, friendType: "card" };
  }

  // 봇이 주공일 때 프렌드로 지정할 카드 반환.
  // 자기가 안 가진 카드 중 마이티>조커>아무 에이스 순으로 고른다(자기 자신 프렌드 방지).
  botFriendCardId(room) {
    const decl = room.players.find((p) => p.clientId === room.declarerClientId);
    const hand = (decl && decl.hand) || [];
    const has = (id) => hand.some((c) => c.id === id);
    const mighty = room.ruleConfig ? room.ruleConfig.mightyCardId : "S_A";
    const candidates = [mighty, "JOKER", "H_A", "D_A", "C_A", "S_A"];
    for (const id of candidates) {
      if (!has(id)) return id;
    }
    return mighty; // 이론상 도달 불가
  }

  // 봇 손패 평가: 각 무늬를 기루다로 가정했을 때 예상 획득 점수를 추정하고,
  // 가장 강한 무늬를 기루다 후보로 고른다. (간단 휴리스틱)
  _evaluateHandForBid(hand) {
    const hasJoker = hand.some((c) => c.suit === "JOKER");
    let best = { suit: null, tricks: 0 };
    for (const suit of SUITS) {
      const inSuit = hand.filter((c) => c.suit === suit);
      const highs = inSuit.filter((c) => ["A", "K", "Q"].includes(c.rank)).length;
      const offAces = hand.filter(
        (c) => c.suit !== suit && c.suit !== "JOKER" && c.rank === "A"
      ).length;
      // 기루다가 SPADE면 마이티는 D_A, 아니면 S_A
      const mightyId = suit === "SPADE" ? "D_A" : "S_A";
      const hasMighty = hand.some((c) => c.id === mightyId);
      // 기저 9점 + 기루다 장수/높은끗/오프A/마이티/조커 가산
      // (평균적인 패가 13 근처에 오도록 보정. 강한 패는 14~17)
      const tricks =
        9 +
        inSuit.length * 0.7 +
        highs * 0.7 +
        offAces * 0.9 +
        (hasMighty ? 1.5 : 0) +
        (hasJoker ? 1.3 : 0);
      if (tricks > best.tricks) best = { suit, tricks };
    }
    return best;
  }

  // 봇의 입찰 결정. 공약하면 { targetScore, trumpSuit, noTrump }, 패스면 null.
  botDecideBid(room, player) {
    const ev = this._evaluateHandForBid(player.hand || []);
    const myMax = Math.min(MAX_BID, Math.floor(ev.tricks));
    const roundMin = room.minBid || MIN_BID;
    if (!ev.suit || myMax < roundMin) return null; // 약한 패 → 패스
    const minAllowed = room.highestBid ? room.highestBid.targetScore + 1 : roundMin;
    if (minAllowed > MAX_BID || myMax < minAllowed) return null; // 못 이기면 패스
    // 이길 수 있으면 필요한 최소치만 보수적으로 부른다.
    return { targetScore: minAllowed, trumpSuit: ev.suit, noTrump: false };
  }

  // 딜미스(노게임) 선언 가능 여부 — 표준 5마 0.5점식 (마이티0 / 10=0.5 / 조커-1 / 그외 점수1 ≤ 0.5).
  // hasActedInBidding이 true면(이미 공약·패스) 선언 불가.
  canDeclareDealMiss(hand, hasActedInBidding = false) {
    if (hasActedInBidding) return false;
    return RuleEngine.canDeclareDealMiss(hand);
  }

  // 이 플레이어가 이번 입찰에서 이미 공약/패스했는지
  hasActedInBidding(room, clientId) {
    return (room.bids || []).some((b) => b.clientId === clientId);
  }

  // 딜미스 선언 처리. 성공 { ok, nickname }, 실패 { error }.
  declareDealMiss(room, clientId) {
    const p = room.players.find((x) => x.clientId === clientId);
    if (!p) return { error: "플레이어를 찾을 수 없습니다." };
    if (room.status !== "bidding") {
      return { error: "입찰 중에만 딜미스를 선언할 수 있습니다." };
    }
    if (this.hasActedInBidding(room, clientId)) {
      return { error: "이미 공약/패스한 뒤에는 딜미스를 선언할 수 없습니다." };
    }
    if (!this.canDeclareDealMiss(p.hand, false)) {
      return { error: "딜미스(노게임) 조건이 아닙니다. (손패 점수 0.5 이하)" };
    }
    return { ok: true, nickname: p.nickname, score: RuleEngine.dealMissScore(p.hand) };
  }

  // ===================== 06~08단계: 플레이 =====================

  // 플레이 시작 준비 - 첫 턴/빈 테이블 세팅. (기루다는 입찰에서 확정된 값 사용)
  startPlay(room) {
    // 주공이 첫 트릭을 리드. (없으면 0번)
    const declIdx = room.declarerClientId
      ? room.players.findIndex((p) => p.clientId === room.declarerClientId)
      : 0;
    room.currentTurnIndex = declIdx >= 0 ? declIdx : 0;
    room.tableCards = []; // 이번 트릭에 나온 카드들
    room.trickComplete = false; // 방금 트릭이 완성되었는지(다음 리드 때 테이블 비움)
    room.trickHistory = []; // 완료된 트릭들의 기록
    room.lastTrickWinner = null; // { clientId, nickname }
    room.trickNumber = 1; // 현재 트릭 번호 (1~10)
    // 룰 설정이 아직 없으면(입찰 없이 시작한 경우) 폴백 기루다로 생성
    if (!room.ruleConfig) {
      room.ruleConfig = RuleEngine.makeRuleConfig(FALLBACK_TRUMP_SUIT, false);
    }
    room.players.forEach((p) => {
      p.wonCards = []; // 이 판에서 획득한 카드들
    });
  }

  // 현재 차례인 플레이어
  currentTurnPlayer(room) {
    if (room.currentTurnIndex == null) return null;
    return room.players[room.currentTurnIndex] || null;
  }

  // 카드 제출 처리. 성공 { card, player, trickResult }, 실패 { error }.
  // options: { declaredSuit?, activateJokerCall? } — 리드일 때만 사용
  // trickResult: 트릭이 완성됐으면 { winnerClientId, winnerNickname }, 아니면 null.
  playCard(room, clientId, cardId, options = {}) {
    if (room.status !== "playing") return { error: "게임 중이 아닙니다." };

    // 직전 트릭이 완성된 상태면(테이블에 5장) 새 리드 전에 비운다.
    if (room.trickComplete) {
      room.tableCards = [];
      room.trickComplete = false;
    }

    const player = room.players[room.currentTurnIndex];
    if (!player || player.clientId !== clientId) {
      return { error: "당신의 차례가 아닙니다." };
    }
    const hand = player.hand || [];
    const idx = hand.findIndex((c) => c.id === cardId);
    if (idx === -1) return { error: "손패에 없는 카드입니다: " + cardId };

    // 08단계: 따라내기 / 조커콜 강제 검증
    const card = hand[idx];
    const isLead = room.tableCards.length === 0;
    const cfg = room.ruleConfig;

    let declaredSuit = null;
    let jokerCallActivated = false;
    if (isLead) {
      if (RuleEngine.isJoker(card, cfg)) {
        declaredSuit = options.declaredSuit || null;
        if (!RuleEngine.isValidSuit(declaredSuit)) {
          return { error: "조커 리드 시 따라낼 무늬(declaredSuit)를 선언해야 합니다." };
        }
      }
      if (RuleEngine.isJokerCall(card, cfg)) {
        jokerCallActivated = !!options.activateJokerCall;
      }
    }

    const legal = RuleEngine.canPlayCard({
      playerHand: hand,
      card,
      tableCards: room.tableCards,
      ruleConfig: cfg,
    });
    if (!legal) {
      const lead = room.tableCards[0];
      if (lead && RuleEngine.isJokerCallActivated(lead, cfg)) {
        return { error: "조커콜! 조커를 내야 합니다. (마이티로 막을 수 있음)" };
      }
      const leadSuit = RuleEngine.leadSuitOf(room.tableCards, cfg);
      return { error: "리드 무늬(" + leadSuit + ")를 따라내야 합니다." };
    }

    hand.splice(idx, 1); // 검증 통과 후 실제 제거
    sortHand(hand);
    const entry = {
      clientId: player.clientId,
      playerNickname: player.nickname,
      card,
    };
    if (isLead && declaredSuit) entry.declaredSuit = declaredSuit;
    if (isLead && RuleEngine.isJokerCall(card, cfg)) {
      entry.jokerCallActivated = jokerCallActivated;
    }
    room.tableCards.push(entry);

    // 09단계: 프렌드 카드가 나오면 프렌드 공개
    if (!room.friendRevealed && room.friendCardId && card.id === room.friendCardId) {
      room.friendRevealed = true;
      room.friendClientId = player.clientId;
    }

    let trickResult = null;
    if (room.tableCards.length === room.players.length) {
      // ---- 트릭 완성: 승자 판정 (08단계 룰 적용) ----
      const winnerClientId = RuleEngine.determineTrickWinner({
        tableCards: room.tableCards,
        ruleConfig: room.ruleConfig,
        trickNumber: room.trickNumber,
        numTricks: NUM_TRICKS,
      });
      const winnerIndex = room.players.findIndex((p) => p.clientId === winnerClientId);
      const winner = room.players[winnerIndex];
      const cards = room.tableCards.map((t) => t.card);

      winner.wonCards = (winner.wonCards || []).concat(cards);
      room.trickHistory.push({
        winnerClientId,
        winnerNickname: winner.nickname,
        cards,
      });
      room.lastTrickWinner = { clientId: winnerClientId, nickname: winner.nickname };
      room.trickComplete = true; // 테이블은 다음 리드 때 비움 (화면에 잠시 보이도록)
      room.currentTurnIndex = winnerIndex; // 승자가 다음 트릭 리드
      const wasLastTrick = room.trickNumber >= NUM_TRICKS;
      if (!wasLastTrick) room.trickNumber++;
      trickResult = {
        winnerClientId,
        winnerNickname: winner.nickname,
        handOver: wasLastTrick,
      };
    } else {
      // 다음 플레이어로 턴 넘김 (순환)
      room.currentTurnIndex = (room.currentTurnIndex + 1) % room.players.length;
    }
    return { card, player, trickResult };
  }

  // 10트릭 완료 여부
  isHandOver(room) {
    return room.status === "playing" && room.trickComplete && room.trickNumber >= NUM_TRICKS;
  }

  // 한 판 종료: 점수 계산 + 세션 누적 정산 + finished 상태
  finishGame(room) {
    let result = Scoring.calculateResult(room);
    result = Scoring.applySessionScores(room, result);
    room.status = "finished";
    room.lastResult = result;
    room.currentTurnIndex = null;
    return result;
  }

  // 결과 확인 후 대기방으로 복귀 (다음 판 준비)
  returnToWaiting(room) {
    room.status = "waiting";
    room.lastResult = null;
    room.kitty = undefined;
    room.discardedKitty = null;
    room.tableCards = [];
    room.trickHistory = [];
    room.lastTrickWinner = null;
    room.trickComplete = false;
    room.trickNumber = 0;
    room.currentTurnIndex = null;
    room.ruleConfig = undefined;
    room.declarerClientId = null;
    room.declaredTrump = undefined;
    room.noTrump = false;
    room.targetScore = null;
    room.friendCardId = undefined;
    room.friendClientId = null;
    room.friendType = undefined;
    room.friendRevealed = false;
    room.highestBid = null;
    room.bids = [];
    room.minBid = undefined;
    room.currentBidderIndex = null;
    room.players.forEach((p) => {
      p.hand = [];
      p.wonCards = [];
      p.isReady = !!p.isBot; // 봇은 항상 준비
    });
    this.fillWithBots(room);
  }

  // 봇이 낼 카드(+리드 옵션)를 고른다.
  // 반환: { cardId, declaredSuit?, activateJokerCall? } 또는 null
  botPickPlay(room, player) {
    if (!player.hand || player.hand.length === 0) return null;
    // 직전 트릭 완성 직후면 테이블을 비운 뒤 리드로 취급
    const tableCards = room.trickComplete ? [] : room.tableCards || [];
    const legal = player.hand.filter((c) =>
      RuleEngine.canPlayCard({
        playerHand: player.hand,
        card: c,
        tableCards,
        ruleConfig: room.ruleConfig,
      })
    );
    const pool = legal.length ? legal : player.hand;
    const card = pool[Math.floor(Math.random() * pool.length)];
    const isLead = tableCards.length === 0;
    const opts = { cardId: card.id };
    if (isLead && RuleEngine.isJoker(card, room.ruleConfig)) {
      const suits = ["SPADE", "HEART", "DIAMOND", "CLUB"];
      opts.declaredSuit = suits[Math.floor(Math.random() * suits.length)];
    }
    if (isLead && RuleEngine.isJokerCall(card, room.ruleConfig)) {
      opts.activateJokerCall = Math.random() < 0.5;
    }
    return opts;
  }

  // 하위 호환: 카드 id만 필요할 때
  botPickCardId(room, player) {
    const pick = this.botPickPlay(room, player);
    return pick ? pick.cardId : null;
  }

  // 끊긴 사람(또는 순수 봇)은 서버가 대신 행동한다.
  isBotControlled(player) {
    return !!(player && (player.isBot || !player.connected));
  }

  // 비정상 끊김: 좌석·손패·점수는 유지하고 봇이 대신 플레이.
  softDisconnect(clientId) {
    for (const roomId in this.rooms) {
      const room = this.rooms[roomId];
      const player = room.players.find((p) => p.clientId === clientId);
      if (!player || player.isBot) continue;
      if (!player.connected && !player.ws) return room; // 이미 soft
      player.connected = false;
      player.disconnectedAt = Date.now();
      player.ws = null;
      return room;
    }
    return null;
  }

  // reconnectToken으로 방/플레이어 찾기
  findByReconnectToken(token) {
    if (!token || typeof token !== "string") return null;
    for (const roomId in this.rooms) {
      const room = this.rooms[roomId];
      const player = room.players.find((p) => p.reconnectToken === token);
      if (player) return { room, player };
    }
    return null;
  }

  // 재접속: 기존 clientId를 새 ws에 붙이고 좌석 복구
  reconnectPlayer(token, ws) {
    const found = this.findByReconnectToken(token);
    if (!found) {
      return { error: "재접속 정보를 찾을 수 없습니다. (만료되었거나 잘못된 토큰)" };
    }
    const { room, player } = found;
    if (player.isBot) {
      return { error: "이미 봇으로 전환된 자리는 재접속할 수 없습니다." };
    }
    // 다른 연결이 살아 있으면 끊고 이 연결로 교체
    if (player.ws && player.ws !== ws && player.ws.readyState === 1 /* OPEN */) {
      try {
        player.ws.close();
      } catch (_) { /* ignore */ }
    }
    ws.clientId = player.clientId;
    ws.roomId = room.roomId;
    player.ws = ws;
    player.connected = true;
    player.disconnectedAt = null;
    return { room, player };
  }

  // 유예 만료 좌석 회수. 영향 받은 방 목록 반환.
  reclaimExpiredSeats(now = Date.now()) {
    const affected = [];
    for (const roomId of Object.keys(this.rooms)) {
      const room = this.rooms[roomId];
      let changed = false;
      // 뒤에서부터 제거해도 안전하도록 복사
      const targets = room.players.filter(
        (p) =>
          !p.isBot &&
          !p.connected &&
          p.disconnectedAt &&
          now - p.disconnectedAt >= RECONNECT_GRACE_MS
      );
      for (const p of targets) {
        if (room.status === "waiting") {
          this.removePlayerByClientId(p.clientId);
          // remove가 방 삭제했을 수 있음
          if (!this.rooms[roomId]) {
            changed = true;
            break;
          }
          this.fillWithBots(this.rooms[roomId]);
          changed = true;
        } else {
          // 게임 중: 영구 봇으로 전환 (손패·점수 유지, 재접속 불가)
          console.log("[reclaim] room", roomId, p.nickname, "→ 영구 봇");
          p.isBot = true;
          p.reconnectToken = null;
          p.connected = true;
          p.disconnectedAt = null;
          p.ws = null;
          p.isReady = true;
          changed = true;
        }
      }
      if (!this.rooms[roomId]) {
        affected.push(null); // 방 삭제됨
        continue;
      }
      // 사람(비봇)이 없으면 방 삭제
      if (this.rooms[roomId].players.every((p) => p.isBot)) {
        delete this.rooms[roomId];
        affected.push(null);
        continue;
      }
      if (changed) affected.push(this.rooms[roomId]);
    }
    return affected.filter(Boolean);
  }

  // 방장 전용: 세션 누적 점수 전부 0으로
  resetSessionScores(room, clientId) {
    if (room.status !== "waiting") {
      return { error: "대기 중에서만 점수를 초기화할 수 있습니다." };
    }
    if (!this.isHost(room, clientId)) {
      return { error: "방장만 점수를 초기화할 수 있습니다." };
    }
    room.players.forEach((p) => { p.sessionScore = 0; });
    return { ok: true };
  }

  // 방장 전용: 대기 중 자리(턴 순서) 셔플. clientId/점수/방장 역할은 유지.
  shuffleSeats(room, clientId) {
    if (room.status !== "waiting") {
      return { error: "게임 시작 전에만 자리를 섞을 수 있습니다." };
    }
    if (!this.isHost(room, clientId)) {
      return { error: "방장만 자리를 섞을 수 있습니다." };
    }
    const arr = room.players;
    for (let i = arr.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      const tmp = arr[i];
      arr[i] = arr[j];
      arr[j] = tmp;
    }
    return { ok: true };
  }

  // ready 상태를 토글한다. 대상 플레이어를 반환(없으면 null).
  toggleReady(room, clientId) {
    const player = room.players.find((p) => p.clientId === clientId);
    if (!player) return null;
    player.isReady = !player.isReady;
    return player;
  }

  // 게임 시작 가능 여부: 정확히 5명 + 전원 준비 완료
  canStart(room) {
    return (
      room.status === "waiting" &&
      room.players.length === MAX_PLAYERS &&
      room.players.every((p) => p.isReady)
    );
  }

  isHost(room, clientId) {
    return room.hostClientId === clientId;
  }

  // clientId로 플레이어를 찾아 방에서 제거한다.
  // 방이 비면 방도 삭제. 영향을 받은 방을 반환(없으면 null).
  removePlayerByClientId(clientId) {
    for (const roomId in this.rooms) {
      const room = this.rooms[roomId];
      const idx = room.players.findIndex((p) => p.clientId === clientId);
      if (idx !== -1) {
        const wasHost = room.hostClientId === clientId;
        room.players.splice(idx, 1);

        // 사람(비봇)이 한 명도 안 남으면 방을 삭제한다. (봇만 남겨두지 않음)
        const humans = room.players.filter((p) => !p.isBot);
        if (humans.length === 0) {
          delete this.rooms[roomId];
          return null;
        }

        // 방장이 나갔으면 남은 "사람" 중 가장 오래된 사람이 방장을 승계한다.
        if (wasHost) {
          room.hostClientId = humans[0].clientId;
        }
        return room;
      }
    }
    return null;
  }

  // 모두에게 공개 가능한 방 상태 (비밀 정보 제외: password, reconnectToken, ws)
  publicState(room) {
    const turnP = this.currentTurnPlayer(room);
    const bidder = this.currentBidder(room);
    const declarer = room.declarerClientId
      ? room.players.find((p) => p.clientId === room.declarerClientId)
      : null;
    const friend =
      room.friendRevealed && room.friendClientId
        ? room.players.find((p) => p.clientId === room.friendClientId)
        : null;
    return {
      roomId: room.roomId,
      status: room.status,
      hostClientId: room.hostClientId,
      canStart: this.canStart(room),
      reconnectGraceMs: RECONNECT_GRACE_MS,
      currentTurnClientId: turnP ? turnP.clientId : null,
      currentTurnNickname: turnP ? turnP.nickname : null,
      lastTrickWinnerNickname: room.lastTrickWinner ? room.lastTrickWinner.nickname : null,
      trickNumber: room.trickNumber || 0,
      trumpSuit: room.ruleConfig ? room.ruleConfig.trumpSuit : null,
      noTrump: !!room.noTrump,
      mightyCardId: room.ruleConfig ? room.ruleConfig.mightyCardId : null,
      jokerCallCardId: room.ruleConfig ? room.ruleConfig.jokerCallCardId : null,
      // 입찰 진행 정보
      minBid: room.minBid || MIN_BID,
      currentBidderClientId: room.status === "bidding" && bidder ? bidder.clientId : null,
      currentBidderNickname: room.status === "bidding" && bidder ? bidder.nickname : null,
      highestBid: room.highestBid
        ? {
            nickname: room.highestBid.nickname,
            targetScore: room.highestBid.targetScore,
            trumpSuit: room.highestBid.trumpSuit,
            noTrump: room.highestBid.noTrump,
          }
        : null,
      // 주공/목표점/프렌드
      declarerClientId: room.declarerClientId || null,
      declarerNickname: declarer ? declarer.nickname : null,
      targetScore: room.targetScore || null,
      friendChosen: room.friendType != null, // 지정 완료 여부(노프렌드 포함)
      friendType: room.friendType || null, // "card"|"player"|"none"
      // 카드 프렌드는 선언 내용(무슨 카드인지)을 즉시 공개. 소유자(닉네임)만 비공개.
      friendCardId: room.friendType === "card" ? room.friendCardId : null,
      friendRevealed: !!room.friendRevealed,
      friendNickname: friend ? friend.nickname : null,
      // 진행 중 주공팀 점수 / 승리까지 남은 점수 (playing·finished)
      ...(room.status === "playing" || room.status === "finished"
        ? (() => {
            const live = Scoring.liveTeamScores(room);
            return {
              declarerTeamScore: live.declarerTeamScore,
              defenderTeamScore: live.defenderTeamScore,
              kittyScore: live.kittyScore,
              pointsNeeded: live.pointsNeeded,
            };
          })()
        : {}),
      lastResult: room.status === "finished" ? room.lastResult : null,
      tableCards: (room.tableCards || []).map((t) => ({
        playerNickname: t.playerNickname,
        card: t.card,
        declaredSuit: t.declaredSuit || null,
        jokerCallActivated: !!t.jokerCallActivated,
      })),
      players: room.players.map((p) => ({
        clientId: p.clientId, // 클라이언트가 "나"를 식별하는 용도 (비밀 아님)
        nickname: p.nickname,
        isReady: p.isReady,
        connected: p.connected,
        isBot: p.isBot,
        botControlled: this.isBotControlled(p) && !p.isBot, // 사람인데 봇이 대신 플레이 중
        disconnectedAt: !p.isBot && !p.connected ? p.disconnectedAt : null,
        reconnectExpiresAt:
          !p.isBot && !p.connected && p.disconnectedAt
            ? p.disconnectedAt + RECONNECT_GRACE_MS
            : null,
        isHost: p.clientId === room.hostClientId,
        handCount: p.hand ? p.hand.length : 0, // 남은 카드 수 (내용은 비공개)
        wonCount: p.wonCards ? p.wonCards.length : 0, // 획득한 카드 수
        trickCount: p.wonCards ? Math.floor(p.wonCards.length / MAX_PLAYERS) : 0, // 이긴 트릭 수
        sessionScore: p.sessionScore || 0, // 방 세션 누적 점수
      })),
    };
  }
}

module.exports = RoomManager;
module.exports.MAX_PLAYERS = MAX_PLAYERS;
module.exports.RECONNECT_GRACE_MS = RECONNECT_GRACE_MS;
