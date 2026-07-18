// Mighty 게임 서버 - 1단계: ping-pong 테스트
//
// 이 서버가 하는 일:
//   1) 3000번 포트에서 실행된다.
//   2) 클라이언트(브라우저/Unity)가 접속하면 콘솔에 socket.id를 찍는다.
//   3) 클라이언트가 "ping_from_client" 이벤트를 보내면
//      "pong_from_server" 이벤트로 응답한다.
//
// http 서버를 함께 두는 이유:
//   Unity가 아직 없어도 브라우저로 접속해서 테스트할 수 있도록
//   간단한 테스트 페이지(public/test.html)를 제공하기 위함이다.

const http = require("http");
const fs = require("fs");
const path = require("path");
const { Server } = require("socket.io");

const PORT = 3000;

// 1) 기본 HTTP 서버: 테스트용 HTML 페이지만 제공한다.
const httpServer = http.createServer((req, res) => {
  // 접속 주소가 "/" 이면 test.html을 돌려준다.
  const filePath =
    req.url === "/" || req.url === "/index.html"
      ? path.join(__dirname, "public", "test.html")
      : path.join(__dirname, "public", req.url);

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

// 2) socket.io 서버를 위 HTTP 서버에 붙인다.
//    CORS는 개발 편의를 위해 모든 origin을 허용한다.
const io = new Server(httpServer, {
  cors: {
    origin: "*",
  },
});

// 3) 클라이언트가 접속할 때마다 실행된다.
io.on("connection", (socket) => {
  console.log("[connect] client connected:", socket.id);

  // 클라이언트가 ping_from_client 를 보내면
  socket.on("ping_from_client", (data) => {
    console.log("[ping] ping_from_client:", data);

    // 보낸 그 클라이언트에게만 pong_from_server 로 응답한다.
    socket.emit("pong_from_server", {
      message: "pong",
      serverTime: new Date().toISOString(),
      youSent: data, // 무엇을 받았는지 되돌려줘서 확인하기 쉽게 함
    });
  });

  // 접속이 끊기면 로그를 남긴다.
  socket.on("disconnect", () => {
    console.log("[disconnect] client disconnected:", socket.id);
  });
});

httpServer.listen(PORT, () => {
  console.log(`socket.io server running on http://localhost:${PORT}`);
  console.log(`테스트 페이지: 브라우저에서 http://localhost:${PORT} 접속`);
});
