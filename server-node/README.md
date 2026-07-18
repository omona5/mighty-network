# Mighty 게임 서버 (server-node)

Unity WebGL 클라이언트와 통신하는 Node.js + socket.io 게임 서버.

## 현재 단계

**01단계: Ping-Pong 테스트** 완료.
클라이언트가 `ping_from_client`를 보내면 서버가 `pong_from_server`로 응답한다.

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

## 이벤트 약속 (01단계)

| 방향 | 이벤트 | 데이터 |
|------|--------|--------|
| Client → Server | `ping_from_client` | `{ message }` |
| Server → Client | `pong_from_server` | `{ message, serverTime, youSent }` |

## 다음 단계

02단계: Unity 클라이언트(`client-unity/`)에서 이 서버에 접속하는 NetworkManager 구현.
