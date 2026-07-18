# Mighty 게임 서버 (server-node)

Unity 클라이언트와 통신하는 Node.js + **순수 WebSocket(ws)** 게임 서버.

> 통신 방식: socket.io 대신 순수 WebSocket을 사용한다.
> 이유는 Unity 에디터 Play 모드와 WebGL 빌드 양쪽에서 동일하게 동작해
> 개발/테스트가 빠르기 때문. 메시지는 `{ "type": ..., "data": ... }` JSON 규칙을 따른다.

## 현재 단계

**01~02단계: Ping-Pong 테스트** 완료.
클라이언트가 `type: ping_from_client` 메시지를 보내면 서버가 `type: pong_from_server`로 응답한다.

## 폴더 구조

```text
server-node/
├─ package.json
├─ server.js          # 서버 진입점 (socket.io + http)
├─ public/
│  └─ test.html       # 브라우저용 ping-pong 테스트 페이지
└─ src/               # (앞으로 RoomManager, game 로직 등이 들어갈 자리)
```

## 실행 방법

```bash
cd server-node
npm install       # 최초 1회
npm start         # = node server.js
```

실행되면 콘솔에 다음이 뜬다.

```text
socket.io server running on http://localhost:3000
```

## 테스트 방법

1. 서버를 실행한다.
2. 브라우저에서 `http://localhost:3000` 접속.
3. **[Ping 보내기]** 버튼 클릭.
4. 화면 로그에 서버의 `pong` 응답이 뜨고, 서버 콘솔에도 접속/ping 로그가 찍히면 성공.

## 메시지 약속 (01~02단계)

모든 메시지는 `{ "type": ..., "data": ... }` JSON 형식.

| 방향 | type | data |
|------|--------|--------|
| Client → Server | `ping_from_client` | `{ message }` |
| Server → Client | `pong_from_server` | `{ message, serverTime, youSent }` |

## 다음 단계

03단계: 방 생성/입장 (RoomManager). 순수 WebSocket이므로 방 관리는 서버에서 직접 구현한다.
