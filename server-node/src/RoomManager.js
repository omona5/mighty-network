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
//     ws                 // WebSocket 참조 (브로드캐스트용, 외부로 노출 금지)
//   }

const crypto = require("crypto");
const Deck = require("./game/Deck");
const Card = require("./game/Card");

const MAX_PLAYERS = 5;

// 랭크 서열 (숫자가 클수록 높음). 07단계 기본 룰용.
// Card.RANKS = ["2".."10","J","Q","K","A"] 순서를 그대로 사용.
function rankValue(rank) {
  return Card.RANKS.indexOf(rank); // 없는 값(JOKER 등)은 -1
}

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
  addPlayer(room, nickname, ws) {
    if (room.players.length >= MAX_PLAYERS) {
      return { error: "방이 가득 찼습니다. (최대 5명)" };
    }
    if (room.status !== "waiting") {
      return { error: "이미 시작된 방에는 입장할 수 없습니다." };
    }

    const player = {
      clientId: ws.clientId,
      nickname,
      isReady: false,
      reconnectToken: makeToken(),
      connected: true,
      isBot: false,
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
    return { player };
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
      nickname: "봇" + botNumber,
      isReady: true, // 봇은 항상 준비 완료
      reconnectToken: null,
      connected: true,
      isBot: true,
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
      p.hand = hands[i] || [];
    });
    room.kitty = kitty; // 바닥패 (아직 아무에게도 공개 안 함)
    return { hands, kitty };
  }

  // 06단계: 플레이 시작 준비 - 첫 턴/빈 테이블 세팅
  startPlay(room) {
    room.currentTurnIndex = 0; // 첫 번째 플레이어(방장)부터
    room.tableCards = []; // 이번 트릭에 나온 카드들
    room.trickComplete = false; // 방금 트릭이 완성되었는지(다음 리드 때 테이블 비움)
    room.trickHistory = []; // 완료된 트릭들의 기록
    room.lastTrickWinner = null; // { clientId, nickname }
    room.players.forEach((p) => {
      p.wonCards = []; // 이 판에서 획득한 카드들
    });
  }

  // 현재 차례인 플레이어
  currentTurnPlayer(room) {
    if (room.currentTurnIndex == null) return null;
    return room.players[room.currentTurnIndex] || null;
  }

  // 07단계: 트릭 승자 판정 (기본 룰)
  //   - leadSuit = 첫 카드의 무늬
  //   - leadSuit와 같은 무늬 중 랭크가 가장 높은 카드를 낸 사람이 승자
  //   - trump/joker/mighty는 아직 미적용
  determineTrickWinner(tableCards, leadSuit) {
    let best = null;
    for (const t of tableCards) {
      if (t.card.suit !== leadSuit) continue; // 리드 무늬만 후보
      if (!best || rankValue(t.card.rank) > rankValue(best.card.rank)) best = t;
    }
    // 첫 카드가 항상 leadSuit이므로 best는 반드시 존재
    return best ? best.clientId : tableCards[0].clientId;
  }

  // 카드 제출 처리. 성공 { card, player, trickResult }, 실패 { error }.
  // trickResult: 트릭이 완성됐으면 { winnerClientId, winnerNickname }, 아니면 null.
  playCard(room, clientId, cardId) {
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

    const [card] = hand.splice(idx, 1);
    room.tableCards.push({
      clientId: player.clientId,
      playerNickname: player.nickname,
      card,
    });

    let trickResult = null;
    if (room.tableCards.length === room.players.length) {
      // ---- 트릭 완성: 승자 판정 ----
      const leadSuit = room.tableCards[0].card.suit;
      const winnerClientId = this.determineTrickWinner(room.tableCards, leadSuit);
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
      trickResult = { winnerClientId, winnerNickname: winner.nickname };
    } else {
      // 다음 플레이어로 턴 넘김 (순환)
      room.currentTurnIndex = (room.currentTurnIndex + 1) % room.players.length;
    }
    return { card, player, trickResult };
  }

  // 봇이 낼 카드 id를 고른다. (스텝6: 규칙 없이 무작위)
  botPickCardId(player) {
    if (!player.hand || player.hand.length === 0) return null;
    const i = Math.floor(Math.random() * player.hand.length);
    return player.hand[i].id;
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
    return {
      roomId: room.roomId,
      status: room.status,
      hostClientId: room.hostClientId,
      canStart: this.canStart(room),
      currentTurnClientId: turnP ? turnP.clientId : null,
      currentTurnNickname: turnP ? turnP.nickname : null,
      lastTrickWinnerNickname: room.lastTrickWinner ? room.lastTrickWinner.nickname : null,
      tableCards: (room.tableCards || []).map((t) => ({
        playerNickname: t.playerNickname,
        card: t.card,
      })),
      players: room.players.map((p) => ({
        clientId: p.clientId, // 클라이언트가 "나"를 식별하는 용도 (비밀 아님)
        nickname: p.nickname,
        isReady: p.isReady,
        connected: p.connected,
        isBot: p.isBot,
        isHost: p.clientId === room.hostClientId,
        handCount: p.hand ? p.hand.length : 0, // 남은 카드 수 (내용은 비공개)
        wonCount: p.wonCards ? p.wonCards.length : 0, // 획득한 카드 수
        trickCount: p.wonCards ? Math.floor(p.wonCards.length / MAX_PLAYERS) : 0, // 이긴 트릭 수
      })),
    };
  }
}

module.exports = RoomManager;
module.exports.MAX_PLAYERS = MAX_PLAYERS;
