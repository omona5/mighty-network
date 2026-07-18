// Mighty 게임 서버 - 순수 WebSocket 버전
//
// 통신 규칙: 모든 메시지는 JSON 문자열
//   { "type": "이벤트이름", "data": { ... } }
//
// 현재 지원 단계:
//   01~02) ping_from_client -> pong_from_server
//   03)    create_room / join_room / leave_room -> room_created / room_joined / game_state / error_message

const http = require("http");
const fs = require("fs");
const path = require("path");
const { WebSocketServer } = require("ws");
const RoomManager = require("./src/RoomManager");

const PORT = 3000;

let nextClientId = 1;
const rooms = new RoomManager();

// 1) HTTP 서버: 브라우저 테스트 페이지 제공
const httpServer = http.createServer((req, res) => {
  const urlPath = req.url === "/" ? "/test.html" : req.url;
  const filePath = path.join(__dirname, "public", urlPath);
  fs.readFile(filePath, (err, content) => {
    if (err) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }
    res.writeHead(200);
    res.end(content);
  });
});

// 2) WebSocket 서버 (같은 3000 포트)
const wss = new WebSocketServer({ server: httpServer });

// 한 클라이언트에게 보내기
function send(ws, type, data) {
  if (ws.readyState === ws.OPEN) {
    ws.send(JSON.stringify({ type, data }));
  }
}

// 방 안 모든 플레이어에게 보내기 (방 브로드캐스트)
function broadcast(room, type, data) {
  for (const p of room.players) {
    if (p.ws && p.ws.readyState === p.ws.OPEN) {
      p.ws.send(JSON.stringify({ type, data }));
    }
  }
}

// 닉네임 유효성 검사
function isValidNickname(name) {
  return typeof name === "string" && name.trim().length > 0 && name.length <= 12;
}

// 3) 접속 처리
wss.on("connection", (ws) => {
  ws.clientId = "C" + nextClientId++;
  ws.roomId = null;
  console.log("[connect] client connected:", ws.clientId);

  ws.on("message", (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch (e) {
      console.log("[warn] JSON 파싱 실패:", raw.toString());
      return;
    }
    const data = msg.data || {};

    switch (msg.type) {
      // ---- 01~02단계: ping-pong ----
      case "ping_from_client":
        console.log("[ping] from", ws.clientId, ":", data);
        send(ws, "pong_from_server", {
          message: "pong",
          serverTime: new Date().toISOString(),
          youSent: data,
        });
        break;

      // ---- 03단계: 방 생성 ----
      case "create_room": {
        if (!isValidNickname(data.nickname)) {
          send(ws, "error_message", { message: "닉네임을 입력하세요. (1~12자)" });
          break;
        }
        // 이미 방에 있으면 먼저 나가게 처리
        if (ws.roomId) leaveCurrentRoom(ws);

        const room = rooms.createRoom(data.password);
        const result = rooms.addPlayer(room, data.nickname.trim(), ws);
        // 방금 만든 방이라 실패할 일은 거의 없지만 방어적으로 처리
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        ws.roomId = room.roomId;
        console.log("[room] created", room.roomId, "by", data.nickname);
        // 본인에게만 방 코드 + 재접속 토큰 전달
        send(ws, "room_created", {
          roomId: room.roomId,
          reconnectToken: result.player.reconnectToken,
        });
        // 방 전체에 현재 상태 전송
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      // ---- 03단계: 방 입장 ----
      case "join_room": {
        if (!isValidNickname(data.nickname)) {
          send(ws, "error_message", { message: "닉네임을 입력하세요. (1~12자)" });
          break;
        }
        const roomId = (data.roomId || "").toUpperCase();
        const room = rooms.getRoom(roomId);
        if (!room) {
          send(ws, "error_message", { message: "방을 찾을 수 없습니다: " + roomId });
          break;
        }
        if (room.password && room.password !== data.password) {
          send(ws, "error_message", { message: "방 비밀번호가 틀렸습니다." });
          break;
        }
        if (ws.roomId) leaveCurrentRoom(ws);

        const result = rooms.addPlayer(room, data.nickname.trim(), ws);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        ws.roomId = room.roomId;
        console.log("[room] joined", room.roomId, "by", data.nickname);
        send(ws, "room_joined", {
          roomId: room.roomId,
          reconnectToken: result.player.reconnectToken,
        });
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      // ---- 03단계: 방 나가기 ----
      case "leave_room":
        leaveCurrentRoom(ws);
        break;

      default:
        console.log("[warn] 알 수 없는 type:", msg.type);
    }
  });

  ws.on("close", () => {
    console.log("[disconnect] client disconnected:", ws.clientId);
    leaveCurrentRoom(ws);
  });
});

// 현재 방에서 플레이어를 빼고, 남은 사람들에게 상태를 갱신해준다.
function leaveCurrentRoom(ws) {
  if (!ws.roomId) return;
  const room = rooms.removePlayerByClientId(ws.clientId);
  const leftRoomId = ws.roomId;
  ws.roomId = null;
  if (room) {
    // 방에 사람이 남아있으면 갱신된 상태를 알려준다
    broadcast(room, "game_state", rooms.publicState(room));
  }
  console.log("[room] left", leftRoomId, "by", ws.clientId);
}

httpServer.listen(PORT, () => {
  console.log(`WebSocket server running on ws://localhost:${PORT}`);
  console.log(`테스트 페이지: 브라우저에서 http://localhost:${PORT} 접속`);
});
