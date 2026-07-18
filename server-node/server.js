// Mighty 게임 서버 - 순수 WebSocket 버전 (1~2단계: ping-pong)
//
// 통신 방식:
//   Unity(에디터/WebGL)와 브라우저 모두 "순수 WebSocket"으로 접속한다.
//   메시지는 JSON 문자열로 주고받으며, 규칙은 아래와 같다.
//
//   { "type": "이벤트이름", "data": { ... } }
//
//   type 을 보고 어떤 요청인지 구분한다. (socket.io의 이벤트 이름 역할)
//
// 이 서버가 하는 일:
//   1) 3000번 포트에서 HTTP(테스트 페이지) + WebSocket 을 함께 제공한다.
//   2) 클라이언트가 접속하면 콘솔에 로그를 남긴다.
//   3) type이 "ping_from_client" 인 메시지를 받으면
//      type "pong_from_server" 메시지로 응답한다.

const http = require("http");
const fs = require("fs");
const path = require("path");
const { WebSocketServer } = require("ws");

const PORT = 3000;

// 접속한 클라이언트에게 부여할 간단한 id 카운터 (socket.id 대용)
let nextClientId = 1;

// 1) HTTP 서버: 브라우저 테스트 페이지(public/test.html) 제공
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

// 2) WebSocket 서버를 위 HTTP 서버에 붙인다. (같은 3000 포트 공유)
const wss = new WebSocketServer({ server: httpServer });

// 클라이언트에게 JSON 메시지를 보내는 헬퍼
function send(ws, type, data) {
  ws.send(JSON.stringify({ type, data }));
}

// 3) 클라이언트가 접속할 때마다 실행된다.
wss.on("connection", (ws) => {
  ws.clientId = "C" + nextClientId++;
  console.log("[connect] client connected:", ws.clientId);

  // 클라이언트가 메시지를 보내면 실행된다.
  ws.on("message", (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch (e) {
      console.log("[warn] JSON 파싱 실패:", raw.toString());
      return;
    }

    // type 으로 어떤 요청인지 구분한다.
    switch (msg.type) {
      case "ping_from_client":
        console.log("[ping] from", ws.clientId, ":", msg.data);
        // 보낸 그 클라이언트에게만 pong 으로 응답한다.
        send(ws, "pong_from_server", {
          message: "pong",
          serverTime: new Date().toISOString(),
          youSent: msg.data,
        });
        break;

      default:
        console.log("[warn] 알 수 없는 type:", msg.type);
    }
  });

  // 접속이 끊기면 로그를 남긴다.
  ws.on("close", () => {
    console.log("[disconnect] client disconnected:", ws.clientId);
  });
});

httpServer.listen(PORT, () => {
  console.log(`WebSocket server running on ws://localhost:${PORT}`);
  console.log(`테스트 페이지: 브라우저에서 http://localhost:${PORT} 접속`);
});
