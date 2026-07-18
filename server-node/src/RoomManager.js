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

const MAX_PLAYERS = 5;

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
  }

  // 현재 차례인 플레이어
  currentTurnPlayer(room) {
    if (room.currentTurnIndex == null) return null;
    return room.players[room.currentTurnIndex] || null;
  }

  // 카드 제출 처리. 성공 { card, player }, 실패 { error }.
  playCard(room, clientId, cardId) {
    if (room.status !== "playing") return { error: "게임 중이 아닙니다." };
    const player = room.players[room.currentTurnIndex];
    if (!player || player.clientId !== clientId) {
      return { error: "당신의 차례가 아닙니다." };
    }
    // 새 트릭 시작: 이전 트릭 카드가 5장 차 있으면 비운다. (승자 판정은 스텝7)
    if (room.tableCards && room.tableCards.length >= room.players.length) {
      room.tableCards = [];
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
    // 다음 플레이어로 턴 넘김 (순환)
    room.currentTurnIndex = (room.currentTurnIndex + 1) % room.players.length;
    return { card, player };
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
      })),
    };
  }
}

module.exports = RoomManager;
module.exports.MAX_PLAYERS = MAX_PLAYERS;
