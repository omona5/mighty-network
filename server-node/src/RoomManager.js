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

const MAX_PLAYERS = 5;

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
      ws,
    };
    room.players.push(player);
    return { player };
  }

  // clientId로 플레이어를 찾아 방에서 제거한다.
  // 방이 비면 방도 삭제. 영향을 받은 방을 반환(없으면 null).
  removePlayerByClientId(clientId) {
    for (const roomId in this.rooms) {
      const room = this.rooms[roomId];
      const idx = room.players.findIndex((p) => p.clientId === clientId);
      if (idx !== -1) {
        room.players.splice(idx, 1);
        if (room.players.length === 0) {
          delete this.rooms[roomId];
          return null; // 방 자체가 사라졌으니 브로드캐스트할 대상 없음
        }
        return room;
      }
    }
    return null;
  }

  // 모두에게 공개 가능한 방 상태 (비밀 정보 제외: password, reconnectToken, ws)
  publicState(room) {
    return {
      roomId: room.roomId,
      status: room.status,
      players: room.players.map((p) => ({
        nickname: p.nickname,
        isReady: p.isReady,
        connected: p.connected,
      })),
    };
  }
}

module.exports = RoomManager;
module.exports.MAX_PLAYERS = MAX_PLAYERS;
