// Mighty 게임 서버 - 순수 WebSocket 버전
//
// 통신 규칙: 모든 메시지는 JSON 문자열
//   { "type": "이벤트이름", "data": { ... } }
//
// 현재 지원 단계:
//   01~02) ping_from_client -> pong_from_server
//   03)    create_room / join_room / leave_room -> room_created / room_joined / game_state / error_message
//   04)    ready / start_game -> game_started
//   05)    카드 배분 -> your_hand (본인 손패만), game_state에 handCount 포함
//   06)    play_card -> 턴 검증 후 테이블에 표시, 봇은 자동으로 냄
//   07)    트릭 5장 완성 시 승자 판정(리드 무늬 최고 랭크), 승자가 다음 리드
//   08)    마이티 룰(기루다/마이티/조커/조커콜/따라내기) - RuleEngine 적용
//   09)    입찰 → 바닥패 교환(discard_kitty) → 프렌드 → playing
//   10)    10트릭 종료 → 점수 계산/승패(game_finished) → return_to_lobby
//   11)    soft disconnect → 봇 대타, reconnect로 복구 (your_hand + game_state)

const http = require("http");
const fs = require("fs");
const path = require("path");
const { WebSocketServer } = require("ws");
const RoomManager = require("./src/RoomManager");
const { sortHand } = require("./src/game/Card");

const PORT = 3000;

let nextClientId = 1;
const rooms = new RoomManager();

// 1) HTTP 서버: 테스트 페이지 + WebGL 정적 파일
const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".ico": "image/x-icon",
  ".svg": "image/svg+xml",
  ".wasm": "application/wasm",
  ".data": "application/octet-stream",
  ".bundle": "application/octet-stream",
};

const httpServer = http.createServer((req, res) => {
  // 쿼리/해시 제거
  let urlPath = (req.url || "/").split("?")[0].split("#")[0];
  if (urlPath === "/") urlPath = "/test.html";

  // public 밖으로 못 나가게
  const publicRoot = path.join(__dirname, "public");
  let filePath = path.normalize(path.join(publicRoot, urlPath));
  if (!filePath.startsWith(publicRoot)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  fs.stat(filePath, (err, stat) => {
    if (!err && stat.isDirectory()) {
      filePath = path.join(filePath, "index.html");
    }
    fs.readFile(filePath, (readErr, content) => {
      if (readErr) {
        res.writeHead(404);
        res.end("Not found: " + urlPath);
        return;
      }
      const ext = path.extname(filePath).toLowerCase();
      const type = MIME[ext] || "application/octet-stream";
      res.writeHead(200, { "Content-Type": type });
      res.end(content);
    });
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
  ws.isAlive = true; // Heartbeat용: 살아있는지 표시
  console.log("[connect] client connected:", ws.clientId);

  // 접속하자마자 클라이언트에게 자신의 id를 알려준다.
  // (클라이언트가 플레이어 목록에서 "나"와 "방장"을 구분하는 데 사용)
  send(ws, "welcome", { clientId: ws.clientId });

  // Heartbeat: 서버 ping에 대한 브라우저/Unity의 pong 응답을 받으면 살아있다고 표시
  ws.on("pong", () => {
    ws.isAlive = true;
  });

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
        // 빈자리를 봇으로 채워 항상 5명 유지 (방장 + 봇4)
        rooms.fillWithBots(room);
        console.log("[room] created", room.roomId, "by", data.nickname, "(봇으로 5명 채움)");
        // 본인에게만 방 코드 + 재접속 토큰 전달
        send(ws, "room_created", {
          roomId: room.roomId,
          reconnectToken: result.player.reconnectToken,
          nickname: result.player.nickname,
        });
        if (result.renamed) {
          send(ws, "error_message", {
            message: "닉네임이 겹쳐 '" + result.player.nickname + "'(으)로 입장했습니다.",
          });
        }
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
        if (room.status !== "waiting") {
          send(ws, "error_message", { message: "이미 시작된 방에는 입장할 수 없습니다." });
          break;
        }
        // 사람이 이미 5명이면 봇을 뺄 자리도 없으니 가득 찬 것.
        if (rooms.humanCount(room) >= 5) {
          send(ws, "error_message", { message: "방이 가득 찼습니다. (사람 5명)" });
          break;
        }
        if (ws.roomId) leaveCurrentRoom(ws);

        // 항상 5명 유지: 자리가 꽉 차 있으면 봇 하나를 빼서 사람이 들어올 자리를 만든다.
        if (room.players.length >= 5) rooms.removeBot(room);

        const result = rooms.addPlayer(room, data.nickname.trim(), ws);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        ws.roomId = room.roomId;
        // 혹시 5명이 안 됐으면 봇으로 다시 채움 (안전장치)
        rooms.fillWithBots(room);
        console.log("[room] joined", room.roomId, "by", result.player.nickname,
          result.renamed ? "(요청: " + data.nickname.trim() + ")" : "", "(봇 1명 교체)");
        send(ws, "room_joined", {
          roomId: room.roomId,
          reconnectToken: result.player.reconnectToken,
          nickname: result.player.nickname,
        });
        if (result.renamed) {
          send(ws, "error_message", {
            message: "닉네임이 겹쳐 '" + result.player.nickname + "'(으)로 입장했습니다.",
          });
        }
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      // ---- 03단계: 방 나가기 (의도적 — 즉시 퇴장) ----
      case "leave_room":
        leaveCurrentRoom(ws); // hard leave
        break;

      // ---- 11단계: 재접속 ----
      case "reconnect": {
        const token = data.reconnectToken || data.token;
        const result = rooms.reconnectPlayer(token, ws);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          send(ws, "reconnect_failed", { message: result.error });
          break;
        }
        const { room, player } = result;
        console.log("[reconnect] room", room.roomId, "player", player.nickname, player.clientId);
        send(ws, "reconnected", {
          roomId: room.roomId,
          clientId: player.clientId,
          reconnectToken: player.reconnectToken,
          nickname: player.nickname,
        });
        // 손패 + 전체 상태 재전송
        if (player.hand && player.hand.length) {
          sortHand(player.hand);
          send(ws, "your_hand", {
            cards: player.hand,
            canDealMiss:
              room.status === "bidding"
              && rooms.canDeclareDealMiss(
                player.hand,
                rooms.hasActedInBidding(room, player.clientId)
              ),
          });
        }
        broadcast(room, "game_state", rooms.publicState(room));
        if (room.status === "finished" && room.lastResult) {
          send(ws, "game_finished", room.lastResult);
        }
        break;
      }

      // ---- 04단계: 준비 토글 ----
      case "ready": {
        const room = rooms.getRoom(ws.roomId);
        if (!room) break;
        if (room.status !== "waiting") {
          send(ws, "error_message", { message: "이미 시작된 게임에서는 준비를 바꿀 수 없습니다." });
          break;
        }
        const player = rooms.toggleReady(room, ws.clientId);
        if (player) {
          console.log("[ready]", ws.clientId, "=>", player.isReady);
          broadcast(room, "game_state", rooms.publicState(room));
        }
        break;
      }

      // ---- 04단계: 게임 시작 (방장만, 5명 전원 준비 시) ----
      case "start_game": {
        const room = rooms.getRoom(ws.roomId);
        if (!room) break;
        if (!rooms.isHost(room, ws.clientId)) {
          send(ws, "error_message", { message: "방장만 게임을 시작할 수 있습니다." });
          break;
        }
        if (!rooms.canStart(room)) {
          send(ws, "error_message", { message: "5명 전원이 준비해야 시작할 수 있습니다." });
          break;
        }
        // 09단계: 바로 playing이 아니라 입찰(bidding) 단계로 진입
        room.status = "bidding";
        // 05단계: 카드 셔플 + 배분 (5명에게 10장씩, 바닥패 3장)
        rooms.dealCards(room);
        // 09단계: 입찰 세팅
        rooms.startBidding(room);
        console.log("[start] room", room.roomId, "게임 시작 + 카드 배분 + 입찰 시작");
        broadcast(room, "game_started", { roomId: room.roomId });
        // 각 사람에게 "본인 손패"만 개별 전송 (입찰 판단에 필요)
        sendHandsToHumans(room);
        broadcast(room, "game_state", rooms.publicState(room));
        // 첫 입찰자가 봇이면 자동 패스
        maybeBotBid(room);
        break;
      }

      // ---- 09단계: 입찰 (공약) ----
      case "bid": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "bidding") break;
        const result = rooms.placeBid(room, ws.clientId, data);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[bid]", ws.clientId, data.targetScore, data.noTrump ? "노기루" : data.trumpSuit);
        const bidder = room.players.find((p) => p.clientId === ws.clientId);
        if (bidder && bidder.hand) {
          send(ws, "your_hand", { cards: bidder.hand, canDealMiss: false });
        }
        handleBidStep(room, result.complete);
        break;
      }

      // ---- 09단계: 입찰 패스 ----
      case "pass_bid": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "bidding") break;
        const result = rooms.passBid(room, ws.clientId);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[pass_bid]", ws.clientId);
        const passer = room.players.find((p) => p.clientId === ws.clientId);
        if (passer && passer.hand) {
          send(ws, "your_hand", { cards: passer.hand, canDealMiss: false });
        }
        handleBidStep(room, result.complete);
        break;
      }

      // ---- 09단계: 딜미스(노게임) 선언 ----
      case "declare_deal_miss": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "bidding") break;
        const result = rooms.declareDealMiss(room, ws.clientId);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[deal_miss] 딜미스 선언:", result.nickname, "→ 재배분/재입찰");
        broadcast(room, "deal_miss", { nickname: result.nickname });
        redealAndRestartBidding(room);
        break;
      }

      // ---- 바닥패 교환: 주공이 3장 버리기 ----
      case "discard_kitty": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "exchanging_kitty") break;
        const result = rooms.discardKitty(room, ws.clientId, data.cardIds);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[discard_kitty]", ws.clientId, data.cardIds);
        send(ws, "your_hand", { cards: result.hand, canDealMiss: false });
        broadcast(room, "kitty_discarded", { ok: true });
        beginFriendSelection(room);
        break;
      }

      // ---- 09단계: 프렌드 선택 (주공만) ----
      case "choose_friend": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "choosing_friend") break;
        const result = rooms.chooseFriend(room, ws.clientId, data || {});
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[choose_friend]", ws.clientId, result.friendType,
          data.friendCardId || data.friendClientId || "");
        broadcast(room, "friend_chosen", {
          friendChosen: true,
          friendType: result.friendType,
          friendCardId: room.friendType === "card" ? room.friendCardId : null,
          friendNickname: result.friendNickname || null,
        });
        startPlaying(room);
        break;
      }

      // ---- 06단계: 카드 내기 ----
      case "play_card": {
        const room = rooms.getRoom(ws.roomId);
        if (!room) break;
        const result = rooms.playCard(room, ws.clientId, data.cardId, {
          declaredSuit: data.declaredSuit,
          activateJokerCall: data.activateJokerCall,
        });
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[play]", ws.clientId, data.cardId);
        if (result.trickResult) {
          console.log("[trick] 승자:", result.trickResult.winnerNickname);
        }
        broadcast(room, "game_state", rooms.publicState(room));
        send(ws, "your_hand", { cards: result.player.hand });
        if (result.trickResult && result.trickResult.handOver) {
          // 마지막 트릭: 승리 연출 여유 후 종료
          setTimeout(() => {
            const r = rooms.getRoom(room.roomId);
            if (r) finishAndBroadcast(r);
          }, TRICK_RESOLVE_DELAY);
        } else {
          maybeBotPlay(room, result.trickResult ? TRICK_RESOLVE_DELAY : undefined);
        }
        break;
      }

      // ---- 세션 누적 점수 초기화 (방장, 대기 중) ----
      case "reset_scores": {
        const room = rooms.getRoom(ws.roomId);
        if (!room) break;
        const result = rooms.resetSessionScores(room, ws.clientId);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[scores] room", room.roomId, "누적 점수 초기화 by", ws.clientId);
        broadcast(room, "scores_reset", { ok: true });
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      // ---- 자리 섞기 (방장, 대기 중 / 게임 시작 전) ----
      case "shuffle_seats": {
        const room = rooms.getRoom(ws.roomId);
        if (!room) break;
        const result = rooms.shuffleSeats(room, ws.clientId);
        if (result.error) {
          send(ws, "error_message", { message: result.error });
          break;
        }
        console.log("[seats] room", room.roomId, "자리 셔플 by", ws.clientId,
          "→", room.players.map((p) => p.nickname).join(", "));
        broadcast(room, "seats_shuffled", { ok: true });
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      // ---- 10단계: 결과 확인 후 대기방 복귀 ----
      case "return_to_lobby": {
        const room = rooms.getRoom(ws.roomId);
        if (!room || room.status !== "finished") break;
        rooms.returnToWaiting(room);
        console.log("[lobby] room", room.roomId, "대기방 복귀");
        broadcast(room, "game_state", rooms.publicState(room));
        break;
      }

      default:
        console.log("[warn] 알 수 없는 type:", msg.type);
    }
  });

  ws.on("close", () => {
    console.log("[disconnect] client disconnected:", ws.clientId);
    // 의도적 leave_room이면 이미 방에서 빠졌음 → soft 불필요
    softDisconnectCurrent(ws);
  });
});

// 각 사람에게 본인 손패를 개별 전송 (입찰 단계면 딜미스 가능 여부도 함께)
function sendHandsToHumans(room) {
  const bidding = room.status === "bidding";
  for (const p of room.players) {
    if (!p.isBot && p.connected && p.ws) {
      if (p.hand) sortHand(p.hand);
      const n = p.hand ? p.hand.length : 0;
      if (n > 13) {
        console.warn("[your_hand] abnormal hand size", p.nickname, n, "status", room.status);
      }
      send(p.ws, "your_hand", {
        cards: p.hand || [],
        canDealMiss:
          bidding
          && rooms.canDeclareDealMiss(
            p.hand,
            rooms.hasActedInBidding(room, p.clientId)
          ),
      });
    }
  }
}

// 재배분 후 재입찰 (딜미스 / 전원 패스 공통)
function redealAndRestartBidding(room) {
  rooms.dealCards(room);
  rooms.startBidding(room);
  sendHandsToHumans(room);
  broadcast(room, "game_state", rooms.publicState(room));
  maybeBotBid(room);
}

// 09단계: 입찰 한 스텝 후 처리 (완료면 마감, 아니면 다음 봇 자동 진행)
function handleBidStep(room, complete) {
  broadcast(room, "game_state", rooms.publicState(room));
  if (!complete) {
    maybeBotBid(room);
    return;
  }
  // 입찰 마감
  const result = rooms.resolveBidding(room);
  if (result.lowerBid) {
    // 전원 패스 → 같은 패로 최소공약 1 낮춰 재입찰
    console.log("[bid] 전원 패스 → 최소공약", result.newMin, "로 낮춰 재입찰");
    broadcast(room, "bid_lowered", { minBid: result.newMin });
    rooms.startBidding(room, result.newMin); // 재배분 없이 최소공약만 하향
    sendHandsToHumans(room);
    broadcast(room, "game_state", rooms.publicState(room));
    maybeBotBid(room);
    return;
  }
  if (result.redeal) {
    // 바닥까지 내려도 전원 패스 → 재배분 후 재입찰
    console.log("[bid] 바닥 공약에서도 전원 패스 → 재배분/재입찰");
    redealAndRestartBidding(room);
    return;
  }
  // 주공 결정 → 바닥패 교환 → (이후) 프렌드 선택
  room.status = "exchanging_kitty";
  console.log("[bid] 주공:", result.declarerNickname, "공약:", result.targetScore,
    "기루다:", result.noTrump ? "노기루" : result.trumpSuit);
  broadcast(room, "bid_result", {
    declarerNickname: result.declarerNickname,
    targetScore: result.targetScore,
    trumpSuit: result.trumpSuit,
    noTrump: result.noTrump,
  });
  const kittyStart = rooms.startKittyExchange(room);
  if (kittyStart.error) {
    console.log("[kitty] error:", kittyStart.error);
  }
  // 주공에게 13장 손패 재전송
  sendHandsToHumans(room);
  broadcast(room, "game_state", rooms.publicState(room));
  maybeBotDiscardKitty(room);
}

// 바닥패 버리기 완료 → 프렌드 선택
const BOT_BID_DELAY = 600;

function beginFriendSelection(room) {
  room.status = "choosing_friend";
  broadcast(room, "game_state", rooms.publicState(room));
  maybeBotChooseFriend(room);
}

// 주공이 봇(또는 끊긴 사람)이면 자동으로 약한 카드 3장 버림
function maybeBotDiscardKitty(room) {
  if (!room || room.status !== "exchanging_kitty") return;
  const decl = room.players.find((p) => p.clientId === room.declarerClientId);
  if (!rooms.isBotControlled(decl)) return;
  setTimeout(() => {
    const r = rooms.getRoom(room.roomId);
    if (!r || r.status !== "exchanging_kitty") return;
    const d = r.players.find((p) => p.clientId === r.declarerClientId);
    if (!rooms.isBotControlled(d)) return; // 재접속했으면 중단
    const ids = rooms.botPickDiscardIds(r);
    const result = rooms.discardKitty(r, r.declarerClientId, ids);
    if (result.error) {
      console.log("[bot] discard error:", result.error);
      return;
    }
    console.log("[bot] 바닥패 버림:", d.nickname, ids.join(","));
    broadcast(r, "kitty_discarded", { ok: true });
    beginFriendSelection(r);
  }, BOT_BID_DELAY);
}

// 09단계: 입찰 차례가 봇(또는 끊긴 사람)이면 잠시 후 자동
function maybeBotBid(room) {
  if (!room || room.status !== "bidding") return;
  const bidder = rooms.currentBidder(room);
  if (!rooms.isBotControlled(bidder)) return;
  setTimeout(() => {
    const r = rooms.getRoom(room.roomId);
    if (!r || r.status !== "bidding") return;
    const b = rooms.currentBidder(r);
    if (!rooms.isBotControlled(b)) return;
    // 손패 평가로 공약할지 패스할지 결정
    const decision = rooms.botDecideBid(r, b);
    let result;
    if (decision) {
      result = rooms.placeBid(r, b.clientId, decision);
      if (result.error) {
        result = rooms.passBid(r, b.clientId); // 혹시 검증 실패하면 패스
        console.log("[bot] 패스:", b.nickname, "(" + result.error + ")");
      } else {
        console.log("[bot] 공약:", b.nickname, decision.targetScore, decision.trumpSuit);
      }
    } else {
      result = rooms.passBid(r, b.clientId);
      console.log("[bot] 패스:", b.nickname);
    }
    if (result.error) return;
    handleBidStep(r, result.complete);
  }, BOT_BID_DELAY);
}

// 09단계: 주공이 봇(또는 끊긴 사람)이면 자동 프렌드 지정
function maybeBotChooseFriend(room) {
  if (!room || room.status !== "choosing_friend") return;
  const decl = room.players.find((p) => p.clientId === room.declarerClientId);
  if (!rooms.isBotControlled(decl)) return;
  setTimeout(() => {
    const r = rooms.getRoom(room.roomId);
    if (!r || r.status !== "choosing_friend") return;
    const d = r.players.find((p) => p.clientId === r.declarerClientId);
    if (!rooms.isBotControlled(d)) return;
    const cardId = rooms.botFriendCardId(r);
    rooms.chooseFriend(r, r.declarerClientId, { friendCardId: cardId });
    console.log("[bot] 프렌드 지정:", d.nickname, cardId);
    broadcast(r, "friend_chosen", {
      friendChosen: true,
      friendType: "card",
      friendCardId: cardId,
    });
    startPlaying(r);
  }, BOT_BID_DELAY);
}

// 09단계: 프렌드 선택 완료 → 본게임 시작
function startPlaying(room) {
  room.status = "playing";
  rooms.startPlay(room);
  console.log("[play] room", room.roomId, "본게임 시작");
  broadcast(room, "game_state", rooms.publicState(room));
  maybeBotPlay(room);
}

// 10단계: 한 판 종료 처리
function finishAndBroadcast(room) {
  const result = rooms.finishGame(room);
  console.log(
    "[finish] room", room.roomId,
    "승:", result.winnerLabel,
    "주공팀", result.declarerTeamScore, "/", result.targetScore,
    "수비팀", result.defenderTeamScore,
    "(바닥패", result.kittyScore + ")",
    result.multipliers && result.multipliers.length
      ? ("배수 " + result.multiplier + "x [" + result.multipliers.join("+") + "]")
      : "배수 1x"
  );
  broadcast(room, "game_finished", result);
  broadcast(room, "game_state", rooms.publicState(room));
}

// 현재 차례가 봇(또는 끊긴 사람)이면 잠시 후 자동으로 카드를 낸다.
const BOT_PLAY_DELAY = 700; // ms
// 트릭 종료 연출(토스트+점수카드 이동) 여유
const TRICK_RESOLVE_DELAY = 1800; // ms
function maybeBotPlay(room, delayMs) {
  if (!room || room.status !== "playing") return;
  const player = rooms.currentTurnPlayer(room);
  if (!rooms.isBotControlled(player)) return;

  const wait = delayMs != null ? delayMs : BOT_PLAY_DELAY;
  setTimeout(() => {
    const r = rooms.getRoom(room.roomId);
    if (!r || r.status !== "playing") return;
    const bot = rooms.currentTurnPlayer(r);
    if (!rooms.isBotControlled(bot)) return;

    const pick = rooms.botPickPlay(r, bot);
    if (!pick || !pick.cardId) return;
    const result = rooms.playCard(r, bot.clientId, pick.cardId, {
      declaredSuit: pick.declaredSuit,
      activateJokerCall: pick.activateJokerCall,
    });
    if (result.error) {
      console.log("[bot] play error:", result.error);
      return;
    }
    console.log(
      "[bot]", bot.nickname, "냄:", pick.cardId
        + (pick.declaredSuit ? (" (" + pick.declaredSuit + ")") : "")
        + (pick.activateJokerCall ? " [조커콜]" : "")
    );
    if (result.trickResult) {
      console.log("[trick] 승자:", result.trickResult.winnerNickname);
    }
    broadcast(r, "game_state", rooms.publicState(r));
    if (result.trickResult && result.trickResult.handOver) {
      setTimeout(() => {
        const rr = rooms.getRoom(r.roomId);
        if (rr) finishAndBroadcast(rr);
      }, TRICK_RESOLVE_DELAY);
    } else {
      maybeBotPlay(r, result.trickResult ? TRICK_RESOLVE_DELAY : undefined);
    }
  }, wait);
}

// 끊김/재접속 직후 현재 단계에 맞는 봇 행동을 재개
function resumeBotActions(room) {
  if (!room) return;
  maybeBotBid(room);
  maybeBotDiscardKitty(room);
  maybeBotChooseFriend(room);
  maybeBotPlay(room);
}

// 의도적 퇴장: 좌석 즉시 삭제
function leaveCurrentRoom(ws) {
  if (!ws.roomId) return;
  const room = rooms.removePlayerByClientId(ws.clientId);
  const leftRoomId = ws.roomId;
  ws.roomId = null;
  if (room) {
    if (room.status === "waiting") rooms.fillWithBots(room);
    broadcast(room, "game_state", rooms.publicState(room));
  }
  console.log("[room] left", leftRoomId, "by", ws.clientId);
}

// 비정상 끊김: 좌석 유지 + 봇 대타
function softDisconnectCurrent(ws) {
  // leave_room으로 이미 빠진 경우 roomId가 없음. 그래도 clientId로 soft 시도.
  const room = rooms.softDisconnect(ws.clientId);
  ws.roomId = null;
  if (!room) return;
  console.log("[soft-disconnect] room", room.roomId, "client", ws.clientId, "→ 봇 대타");
  broadcast(room, "game_state", rooms.publicState(room));
  resumeBotActions(room);
}

// ---- Heartbeat: 좀비 연결(조용히 끊긴 클라이언트) 감지 ----
// 브라우저 뒤로가기 등으로 close가 안 올 수 있어, ping 무응답이면 강제 종료.
// 주기 10초 → 대략 10~20초 안에 감지 (close가 오면 즉시 soft-disconnect).
const HEARTBEAT_INTERVAL = 10000; // 10초
const heartbeat = setInterval(() => {
  wss.clients.forEach((ws) => {
    if (ws.isAlive === false) {
      console.log("[heartbeat] 응답 없는 연결 종료:", ws.clientId);
      return ws.terminate(); // close → softDisconnectCurrent
    }
    ws.isAlive = false;
    ws.ping();
  });
}, HEARTBEAT_INTERVAL);

// 재접속 유예 만료 좌석 회수 (대기: 제거, 게임중: 영구 봇)
const RECLAIM_INTERVAL = 30000;
const reclaimTimer = setInterval(() => {
  const affected = rooms.reclaimExpiredSeats();
  for (const room of affected) {
    console.log("[reclaim] room", room.roomId, "상태 갱신");
    broadcast(room, "game_state", rooms.publicState(room));
    resumeBotActions(room);
  }
}, RECLAIM_INTERVAL);

wss.on("close", () => {
  clearInterval(heartbeat);
  clearInterval(reclaimTimer);
});

httpServer.listen(PORT, () => {
  console.log(`WebSocket server running on ws://localhost:${PORT}`);
  console.log(`테스트 페이지: 브라우저에서 http://localhost:${PORT} 접속`);
});
