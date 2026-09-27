# Mighty 게임 서버 (server-node)

Unity 클라이언트와 통신하는 Node.js + **순수 WebSocket(ws)** 게임 서버.

> 통신 방식: socket.io 대신 순수 WebSocket을 사용한다.
> 이유는 Unity 에디터 Play 모드와 WebGL 빌드 양쪽에서 동일하게 동작해
> 개발/테스트가 빠르기 때문. 메시지는 `{ "type": ..., "data": ... }` JSON 규칙을 따른다.

## 현재 구현

- ping/pong 및 Heartbeat
- 방 생성·입장·퇴장, 봇 충원, 준비 및 게임 시작
- 덱·셔플·배분, 입찰, 바닥패 교환, 프렌드 선택
- 마이티·조커·조커콜을 포함한 서버 권위형 카드 판정
- 트릭 및 최종 점수 계산
- 연결 해제 시 봇 대타와 reconnectToken 기반 복구

## 폴더 구조

```text
server-node/
├─ package.json
├─ server.js          # HTTP + WebSocket 서버 진입점
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

저장소 루트의 PowerShell 도구를 사용하면 백그라운드 서버를 한 번에 관리할 수 있다.

```powershell
.\scripts\Start-LocalServer.ps1
.\scripts\Stop-LocalServer.ps1
```

상태 확인: `http://localhost:3000/health`

실행되면 콘솔에 다음이 뜬다.

```text
WebSocket server running on ws://localhost:3000
```

## 테스트 방법

1. 서버를 실행한다.
2. 브라우저에서 `http://localhost:3000` 접속.
3. **[Ping 보내기]** 버튼 클릭.
4. 화면 로그에 서버의 `pong` 응답이 뜨고, 서버 콘솔에도 접속/ping 로그가 찍히면 성공.

## 메시지 약속

모든 메시지는 `{ "type": ..., "data": ... }` JSON 형식.

| 방향 | type | data |
|------|--------|--------|
| C → S | `ping_from_client` | `{ message }` |
| S → C | `pong_from_server` | `{ message, serverTime, youSent }` |
| C → S | `create_room` | `{ nickname, password? }` |
| C → S | `join_room` | `{ roomId, nickname, password? }` |
| C → S | `leave_room` | `{}` |
| S → C (본인) | `room_created` | `{ roomId, reconnectToken }` |
| S → C (본인) | `room_joined` | `{ roomId, reconnectToken }` |
| S → C (방 전체) | `game_state` | `{ roomId, status, players[] }` |
| S → C (본인) | `error_message` | `{ message }` |

- `players[]` 항목: `{ nickname, isReady, connected }` (비공개 정보 제외)
- 방 최대 5명. 방이 비면 자동 삭제.

## 파일 구조

```text
server.js            # 진입점: HTTP + WebSocket, 메시지 라우팅
src/RoomManager.js   # 방 목록 관리 (생성/입장/퇴장, 토큰 발급)
public/test.html     # 브라우저 테스트 페이지 (방 UI 포함)
```

## 테스트

```bash
npm test
```

규칙, 점수 계산, 재접속 테스트를 순서대로 실행한다.

## 방 초대 URL (로컬)

- 멀티플레이 로비: `http://localhost:3000/multiplayer`
- 초대 입장: `http://localhost:3000/room/ABCD` — 닉네임 입력 후 입장. 실제 방 코드로 바꿔 사용합니다.
- 싱글플레이: `http://localhost:3000/singleplayer`
- 튜토리얼: `http://localhost:3000/tutorial`
- 타이틀: `http://localhost:3000/webgl/` (기존 `/test.html` 진단 페이지 유지)

방 생성·참여 성공 시 주소가 `/room/{roomCode}`로 바뀌며, 대기실의 **초대 링크 복사**는 현재 브라우저의 origin을 사용합니다. 다른 기기에서 접속할 때는 `localhost` 대신 LAN IP 또는 도메인으로 먼저 접속하세요. 비밀번호가 설정된 방은 기존 비밀번호 검증을 유지하며, 초대 입장 시 서버가 요구하면 비밀번호 입력란이 표시됩니다. 초대 토큰은 적용하지 않았습니다.

Unity WebGL 빌드는 `Responsive` 템플릿을 사용하고 `public/webgl/`에 배치합니다. 템플릿의 `/webgl/` base 경로 덕분에 `/room/...` 새로고침 시에도 Build·StreamingAssets를 같은 위치에서 읽습니다. C# 또는 `.jslib` 수정은 WebGL 재빌드가 필요합니다.

`npm test`에 `test/roomInvite.test.js`가 포함되어 있습니다. 임시 포트의 실제 HTTP/WebSocket 서버로 경로, 정상 입장, 잘못된 코드, 없는/종료된 방, 정원 초과, 게임 진행 중 입장 및 비밀번호 검증을 검사합니다. 브라우저 브리지의 origin 기반 링크·URL 변경·클립보드 성공/실패/폴백도 검사합니다.
