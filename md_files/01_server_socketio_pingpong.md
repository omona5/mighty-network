# 01. Node.js + socket.io 서버 초기화 및 Ping-Pong 테스트

## 목표

Node.js 서버를 만들고 socket.io를 이용해 클라이언트와 실시간 이벤트를 주고받을 수 있게 한다.

이 단계에서는 Unity 없이도 서버가 실행되는지만 먼저 확인한다.

## 구현 범위

- Node.js 프로젝트 생성
- socket.io 설치
- 서버 포트 3000에서 실행
- 클라이언트 접속 로그 출력
- `ping_from_client` 이벤트 수신
- `pong_from_server` 이벤트 응답

## 서버 폴더 구조

```text
server-node/
├─ package.json
├─ server.js
└─ README.md
```

## 설치 명령

```bash
mkdir server-node
cd server-node
npm init -y
npm install socket.io
node server.js
```

## Cursor 프롬프트

```text
Node.js와 socket.io를 사용해서 아주 간단한 게임 서버를 만들어줘.

요구사항:
1. 서버는 3000번 포트에서 실행한다.
2. 클라이언트가 접속하면 socket.id를 콘솔에 출력한다.
3. 클라이언트가 `ping_from_client` 이벤트를 보내면 서버는 콘솔에 데이터를 출력한다.
4. 서버는 같은 클라이언트에게 `pong_from_server` 이벤트로 응답한다.
5. CORS는 개발 편의를 위해 모든 origin을 허용한다.
6. 코드는 server.js 하나로 작성한다.
```

## 예상 server.js 구조

```javascript
const { Server } = require("socket.io");

const io = new Server(3000, {
  cors: {
    origin: "*"
  }
});

io.on("connection", (socket) => {
  console.log("client connected:", socket.id);

  socket.on("ping_from_client", (data) => {
    console.log("ping_from_client:", data);

    socket.emit("pong_from_server", {
      message: "pong",
      serverTime: new Date().toISOString()
    });
  });

  socket.on("disconnect", () => {
    console.log("client disconnected:", socket.id);
  });
});

console.log("socket.io server running on port 3000");
```

## 완료 기준

- `node server.js` 실행 시 서버가 꺼지지 않고 대기한다.
- 클라이언트가 붙으면 접속 로그가 찍힌다.
- `ping_from_client` 이벤트를 받으면 `pong_from_server`로 응답할 수 있다.

## 다음 단계

Unity에서 이 서버에 접속하는 NetworkManager를 만든다.
