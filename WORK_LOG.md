# WORK_LOG

## 2026-07-18 13:16 (UTC+9)

- **작업 요청**: 마이티(트럼프 카드 게임) 개발 시작. Unity WebGL 클라이언트 + Node.js 서버, 로컬호스트 테스트 후 최종적으로 라즈베리파이 배포. 개발 계획 md 파일 확인 요청.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **확인/열람한 파일**:
  - `README.md`
  - `md_files/00_project_overview.md` ~ `md_files/13_cursor_workflow.md` (총 14개 계획 문서)
- **실행 명령**: `ls -la`, `git log --oneline -10`
- **생성/갱신 파일**: `WORK_LOG.md` (신규 생성)
- **결과 요약**:
  - 프로젝트는 서버 authoritative 방식(서버가 게임 상태 관리, Unity는 렌더링/입력만) 원칙으로 설계됨.
  - 12단계 개발 로드맵 확인. 첫 목표는 Unity 버튼 클릭 → 서버 ping → pong 응답 표시.
  - 폴더 구조 제안: `client-unity/`, `server-node/`.
  - 현재 저장소 상태: `md_files/` 계획 문서와 기본 `README.md`만 존재. 실제 코드는 아직 없음(Initial commit 1개).
- **미해결 이슈/주의사항**: 없음. 다음 단계(서버 초기화) 진행 여부 사용자 확인 필요.
- **다음 단계**: 사용자 선택에 따라 `server-node/` Node.js + socket.io 서버(01단계) 구현 시작.

## 2026-07-18 13:22 (UTC+9)

- **작업 요청**: 01단계 - Node.js + socket.io 서버 초기화 및 ping-pong 테스트 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network/server-node`
- **환경**: Node v26.5.0, npm 11.17.0
- **생성/수정 파일**:
  - `server-node/package.json` (npm init + start 스크립트, main=server.js)
  - `server-node/server.js` (http 서버 + socket.io, ping_from_client → pong_from_server)
  - `server-node/public/test.html` (브라우저용 ping-pong 테스트 페이지)
  - `server-node/README.md` (실행/테스트 방법, 이벤트 약속)
  - `server-node/src/` (빈 폴더, 이후 로직 배치용)
- **실행 명령**:
  - `npm init -y`
  - `npm install socket.io` (22개 패키지 추가, 취약점 0)
  - `node server.js` (백그라운드 실행 중, PID 93826, 포트 3000)
  - `npm install --no-save socket.io-client` + 임시 테스트 클라이언트로 ping-pong 검증 후 파일 삭제
- **결과/검증**:
  - HTTP 페이지 200, `/socket.io/socket.io.js` 200 응답 확인.
  - ping-pong end-to-end 성공 (`PING-PONG OK`), 서버 콘솔에 connect/ping/disconnect 로그 확인.
- **미해결 이슈**: 없음. (서버는 백그라운드에서 계속 실행 중)
- **다음 단계**: 02단계 - Unity 프로젝트 생성 및 NetworkManager로 서버 접속.

## 2026-07-18 13:25 (UTC+9)

- **작업 요청**: 개발 방향 결정 (스프라이트 스타일 / 다음 단계).
- **결정 사항**:
  - 스프라이트 스타일: **픽셀 아트**. 카드 규격 추천 64×96px(2:3), 앞면 53장 + 뒷면 1장 동일 크기, Unity 임포트 시 Point 필터/무압축 예정.
  - Unity 프로젝트: **사용자가 직접 생성** 예정. 위치는 `client-unity/`, 템플릿 2D(Core), 이후 WebGL 스위치.
  - WebGL socket.io 연결 방식: 브라우저 socket.io.js를 .jslib 브릿지로 사용하는 방식 채택 예정(서버 변경 불필요).
- **생성/수정 파일**: `WORK_LOG.md`만 갱신.
- **다음 단계**: 사용자가 `client-unity/`에 Unity 프로젝트 생성 완료하면, NetworkManager.cs + WebGL socket.io .jslib 브릿지 + 테스트 UI 추가.

## 2026-07-18 13:27 (UTC+9)

- **작업 요청**: 맥/윈도우 오가며 git clone/push 방식으로 개발 가능한지 문의 + 협업 환경 정비.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **생성/수정 파일**:
  - `.gitignore` 수정: `node_modules/`, npm 로그, `.env`, `.DS_Store`, `Thumbs.db` 추가.
  - `.gitattributes` 신규 생성: text=auto eol=lf, 이미지/오디오/Unity 에셋 binary 지정 (LF/CRLF 문제 방지).
- **실행 명령**: `git rm --cached .DS_Store` (추적된 적 없어 no-op), `git status --short`.
- **결과/확인**:
  - 현재 git 상태: `.gitignore` 수정됨, `.gitattributes`/`WORK_LOG.md`/`md_files/`/`server-node/` 미추적. (초기 커밋에는 README.md, .gitignore만 포함되어 있었음)
  - 결론: 맥↔윈도우 크로스 개발 가능. node_modules/Library는 gitignore, 각 PC에서 `npm install` 및 Unity가 자동 복원. Unity 버전 통일 + .meta 커밋 필수 안내.
- **미해결 이슈**: 없음.
- **다음 단계**: (선택) 현재 상태 첫 커밋 정리 및 GitHub 연결. 이후 Unity 프로젝트 생성 대기.

## 2026-07-18 13:33 (UTC+9)

- **작업 요청**: 현재까지 작업 내용을 커밋 정리 후 GitHub push.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **확인 사항**:
  - 사용자가 Unity 프로젝트를 `mighty-network-unity/` 폴더에 직접 생성함 (계획상 명칭은 client-unity였으나 실제 명칭은 mighty-network-unity).
  - 루트 `.gitignore`의 Unity 규칙이 루트 기준 anchored(`/[Ll]ibrary/`)라 하위 폴더에 미적용 → `git add` 대상이 34,491개로 폭증하는 문제 발견.
- **생성/수정 파일**:
  - `mighty-network-unity/.gitignore` 신규 생성 (Unity 표준 ignore, 프로젝트 폴더 기준). 적용 후 추적 대상 34,491 → 70개로 정상화.
- **실행 명령**: `git status`, `git check-ignore`, `git add -A --dry-run | wc -l`.
- **결과**: `mighty-network-unity/Library/` 등 자동 생성 폴더 정상 무시 확인. Remote: origin=https://github.com/omona5/mighty-network.git, branch=main.
- **다음 단계**: 아래에서 add/commit/push 진행.

## 2026-07-18 13:50 (UTC+9)

- **작업 요청**: 스텝2 진행 - Unity ↔ 서버 연결. 연결 방식은 순수 WebSocket(방식 B) 채택(에디터에서 바로 테스트 가능).
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **결정 배경**: WebGL은 Unity 버전과 무관하게 System.Net.WebSockets 미지원 → socket.io C# 클라이언트는 WebGL 불가. 에디터+WebGL 동시 지원 위해 순수 WebSocket + NativeWebSocket 채택.
- **서버(server-node) 변경**:
  - `npm uninstall socket.io && npm install ws` (ws@8.21.1)
  - `server.js` 재작성: socket.io → `ws`(WebSocketServer). 메시지 규칙 `{type, data}` JSON. `ping_from_client` → `pong_from_server`.
  - `public/test.html` 재작성: 순수 WebSocket 사용 (`new WebSocket("ws://"+location.host)`).
  - `package.json` description 갱신.
  - `README.md` WebSocket 방식으로 갱신.
- **Unity(mighty-network-unity) 변경**:
  - `Packages/manifest.json`에 `com.endel.nativewebsocket` (git upm) 추가.
  - `Assets/Scripts/NetworkManager.cs` 신규 작성: ws 접속 + Ping 버튼(OnGUI 자동 UI) + pong 수신 로그. WebGL 대비 `DispatchMessageQueue`를 `#if !UNITY_WEBGL || UNITY_EDITOR`로 처리.
- **실행/검증**:
  - 이전 socket.io 서버(PID 93848) 종료, 포트 3000 확보.
  - 새 ws 서버 실행(PID 22115). 임시 ws 클라이언트로 ping-pong 테스트 → `WebSocket PING-PONG 정상 작동` 확인.
- **미해결 이슈**: Unity 측은 사용자가 에디터에서 패키지 임포트 + 스크립트를 GameObject에 부착 후 Play로 검증 필요(아직 미실행).
- **다음 단계**: 사용자가 Unity에서 NetworkManager를 빈 GameObject에 붙이고 Play → Ping 버튼으로 pong 확인. 이후 03단계(방 생성/입장).

## 2026-07-18 13:57 (UTC+9)

- **작업 요청**: 재접속 관련 계획 수정 반영 (자동 재접속/Heartbeat/세션 토큰 복구/상태 전체 재전송 배치 조정) + 순수 WebSocket 전환 명시.
- **수정 파일**:
  - `md_files/00_project_overview.md`: 기술 스택 socket.io→WebSocket, 전체 개발 단계에 reconnectToken(3단계)/Heartbeat(3~4단계) 조기 도입 및 자동재접속+상태재전송(11단계) 메모 추가.
  - `md_files/03_room_create_join.md`: 상단에 reconnectToken 조기 발급 + WebSocket 방 브로드캐스트 직접 관리 메모.
  - `md_files/11_reconnect_private_server.md`: 상단에 4기능 배치 정리 메모.
- **결과**: 계획 문서에 조정안 반영 완료. (browser ping-pong 테스트는 사용자 확인 완료. Unity 에디터 확인은 재안내 예정)
- **다음 단계**: Unity 에디터 접속 확인 → 스텝2 커밋 → 스텝3(방 생성/입장 + reconnectToken).

## 2026-07-18 14:07 (UTC+9)

- **작업 요청**: 스텝3 - 방 생성/입장/플레이어 목록 + reconnectToken 조기 도입.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js` 신규: 방 관리(생성/입장/퇴장), 4자리 방코드(헷갈리는 문자 제외), reconnectToken(crypto) 발급, 최대 5명, 공개 상태(publicState)에서 password/token/ws 제외.
  - `server.js` 갱신: create_room/join_room/leave_room 처리, 방 브로드캐스트(broadcast), 닉네임 검증(1~12자), 방 비밀번호 검증, disconnect 시 자동 퇴장 처리.
  - `public/test.html` 갱신: 방 UI(닉네임/방코드/비번 입력, 방 만들기/입장/나가기, 플레이어 목록) 추가 → 브라우저 탭으로 다중 플레이어 테스트 가능.
  - `README.md` 메시지 약속/파일 구조 갱신.
- **Unity(mighty-network-unity) 변경**:
  - `Assets/Scripts/NetworkManager.cs` 갱신: 로비 UI(닉네임/방코드/비번), 방 만들기/입장/나가기, 플레이어 목록 실시간 표시, room_created/room_joined 시 reconnectToken을 PlayerPrefs에 저장, error_message 로그.
- **실행/검증**:
  - 서버 재시작(PID 35617). 다중 클라이언트 시나리오 자동 테스트 통과:
    1) 방 생성+토큰 발급 O, 2) 4명 입장 목록 브로드캐스트, 3) 없는 방 에러, 4) 6번째 정원초과 에러(최대5명), 5) 퇴장 시 목록 갱신.
- **미해결 이슈**: Unity 측 다중 접속 UI 테스트는 사용자가 에디터+브라우저 탭으로 확인 필요.
- **다음 단계**: 사용자 Unity 확인 → 스텝3 커밋 → 스텝4(준비/게임시작 + Heartbeat).

## 2026-07-18 14:26 (UTC+9)

- **작업 요청**: 스텝4 - 준비 상태 + 5인 게임 시작. 방장 개념(혼합 방식: 방장 두되 전원준비+방장 시작버튼) + 방장 승계 + Heartbeat 보강.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: room.hostClientId 추가(첫 입장자=방장, 퇴장 시 players[0] 승계), toggleReady(), canStart()(5명 전원 준비), isHost(), publicState에 clientId/isHost/hostClientId/canStart 포함.
  - `server.js`: 접속 시 `welcome{clientId}` 전송, `ready` 토글 처리, `start_game`(방장+canStart 검증) → `game_started`+`game_state` 브로드캐스트, playing 상태에선 ready 변경 차단.
  - `server.js`: Heartbeat 추가 - 15초 주기 ws.ping(), 지난 주기 무응답(isAlive=false) 연결 terminate() → close로 자동 퇴장. (브라우저/Unity WebSocket은 ping에 자동 pong)
  - `public/test.html`: welcome 처리, 방장 표시/‹나› 표시, 준비 버튼, 방장 전용 시작 버튼(canStart 시 활성), game_started 배너.
- **Unity 변경**:
  - `Assets/Scripts/NetworkManager.cs`: welcome로 myClientId 저장, PlayerInfo/GameState에 clientId/isHost/hostClientId/canStart 추가, 준비/시작 버튼(방장+canStart), 방장·나 표시, game_started 처리.
- **검증(자동 테스트 통과)**: A)방장=첫입장자, B)비방장 시작 차단, C)미준비 시작 차단, D)4명 canStart=false, E)5명 canStart=true, F)방장 시작→전원 game_started+playing, G)방장 퇴장→자동 승계.
- **계획 문서**: `md_files/04_ready_and_start_game.md`에 방장/시작조건/Heartbeat 수정 메모 추가.
- **미해결 이슈**: 없음. Unity 다중 접속 UI 확인은 사용자 검증 예정.
- **다음 단계**: 사용자 Unity 확인 → 스텝4 커밋 → 스텝5(카드 덱/셔플/배분).

## 2026-07-18 14:36 (UTC+9)

- **작업 요청**: 5명 미만(사람)일 때도 게임 시작 가능하도록 봇 도입. 사람 부족 시 봇이 빈자리 채움.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: player에 isBot 추가. addBot()(대기중+빈자리, 봇 항상 준비, ws=null, 닉네임 봇N), removeBot()(마지막 봇 제거). 방장 승계는 "사람"만 대상, 사람이 0명이면 방 삭제(봇만 남기지 않음). publicState에 isBot 포함.
  - `server.js`: add_bot / remove_bot 처리(방장만, 대기 중). start_game은 기존 canStart(5명 전원 준비) 재사용 - 봇은 항상 준비라 사람만 준비하면 시작 가능.
  - `public/test.html`: 봇 추가/제거 버튼(방장 전용), 플레이어 목록에 [봇] 표시.
- **Unity 변경**:
  - `Assets/Scripts/NetworkManager.cs`: add_bot/remove_bot 메시지, PlayerInfo.isBot, 방장 전용 봇 추가/제거 버튼, 목록에 [봇] 표시.
- **검증(자동 테스트 통과)**: A)사람2명, B)봇3 추가→5명 완성(봇 자동준비), C)비방장 봇추가 차단, D)사람 미준비 시작불가, E)사람 전원준비 canStart=true, F)시작 성공 playing, G)봇 제거, H)방장 퇴장으로 봇만 남으면 방 삭제.
- **계획 문서**: `md_files/04_ready_and_start_game.md`에 봇 도입 메모 추가.
- **미해결/후속**: 봇의 실제 카드 플레이 AI는 스텝5~6(카드/턴) 이후 구현 예정.
- **다음 단계**: 사용자 Unity 확인 → 스텝4(봇 포함) 커밋 → 스텝5(카드 덱/셔플/배분).

## 2026-07-18 14:42 (UTC+9)

- **작업 요청**: 봇 방식을 "수동 추가"에서 "항상 5명 자동 유지"로 변경. 방 생성 시 나+봇4, 사람 입장 시 봇 1명 제거, 사람 퇴장 시 봇 보충.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: humanCount(), fillWithBots()(대기 중 5명까지 봇 채움) 추가.
  - `server.js`: create_room 후 fillWithBots (방장+봇4). join_room: status/humanCount(>=5 거절) 검증 → 자리 있으면 removeBot으로 봇 1명 빼고 사람 추가 후 fillWithBots. leaveCurrentRoom: 대기 중이면 fillWithBots로 봇 보충. 수동 add_bot/remove_bot 핸들러 제거.
  - `public/test.html`: 봇 추가/제거 버튼 및 관련 로직 제거.
- **Unity 변경**: `NetworkManager.cs`: AddBot/RemoveBot 메시지·메서드·버튼 제거 (자동이므로).
- **검증(자동 테스트 통과)**: A)생성=사람1+봇4, B)입장→사람2봇3, C)사람3봇2, D)퇴장→봇보충 5유지, E)사람5봇0, F)6번째 사람 차단, G)사람 전원준비 canStart=true.
- **계획 문서**: `md_files/04_ready_and_start_game.md` 봇 방식 "항상 5명 유지"로 갱신.
- **미해결/후속**: 봇 카드 플레이 AI는 스텝5~6 이후.
- **다음 단계**: 사용자 확인 → 스텝4 커밋 → 스텝5.

## 2026-07-18 14:47 (UTC+9)

- **작업 요청**: 플레이어 목록 정렬 개선 - 사람 입장 시 봇 뒤(3·4·5번 자리)로 밀리고 사람은 위쪽에 오도록.
- **수정 파일**: `server-node/src/RoomManager.js` - addPlayer에서 사람을 `insertIndex = 사람 수` 위치에 splice 삽입(항상 [사람...][봇...] 순서 유지).
- **검증**: 생성=오모나/봇1~4, 일모나 입장=오모나/일모나/봇1~3, 이모나 입장=오모나/일모나/이모나/봇1~2. 정상.
- **주의(운영 메모)**: 테스트 중 `node ... &` 방식으로 서버를 띄웠다가 명령 종료 시 서버가 함께 종료됨 → 이후 block_until_ms:0 백그라운드로 재기동(PID 69109). 서버 정상 실행 확인.
- **다음 단계**: 사용자 확인 → 스텝4 커밋 → 스텝5(카드 덱/셔플/배분).

## 2026-07-18 15:30 (UTC+9)

- **작업 요청**: 개발 방향 A(Unity 본격 사용) 확정 → 스텝5-1 서버 카드 배분 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 신규/변경**:
  - `src/game/Card.js`(신규): 카드 데이터 구조(id/suit/rank/point), 52장 + 조커, 점수카드(10·J·Q·K·A) point=1.
  - `src/game/Deck.js`(신규): createDeck(53장), Fisher-Yates shuffle, deal(5명×10장 + 바닥패3장), createShuffledDeal.
  - `src/RoomManager.js`: dealCards(room) 추가(섞어 배분, room.kitty 보관, p.hand 설정), publicState에 handCount 추가.
  - `server.js`: start_game 시 dealCards → 각 사람에게 your_hand(본인 손패만) 개별 전송, 이후 game_state 브로드캐스트(handCount 포함).
  - `public/test.html`: your_hand 수신 시 카드 UI 렌더(무늬 기호/색/조커), 목록에 (N장) 표시.
- **검증(자동 테스트 통과, ws_test.js)**: 방 생성→준비→canStart→start_game→your_hand 10장 수신, 시작 후 handCounts=10,10,10,10,10. 바닥패 3장 서버 보관.
- **미해결/후속**: 스텝5-2 Unity 화면에 실제 카드 렌더(본격 Unity 시작). 바닥패는 스텝9(입찰/주공) 때 주공에게 전달 예정.
- **다음 단계**: Unity 손패 시각화(Canvas/카드 오브젝트).

## 2026-07-18 15:45 (UTC+9)

- **작업 요청**: 스텝5-2 Unity 손패 시각화. 방식은 editor_guided(정석 워크플로우: Canvas/프리팹/Inspector 연결을 사용자가 직접).
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **Unity(mighty-network-unity) 신규/변경**:
  - `Assets/Scripts/CardData.cs`(신규): 서버 카드와 동일한 직렬화 클래스(id/suit/rank/point).
  - `Assets/Scripts/CardView.cs`(신규): 카드 프리팹용. background(Image)/label(Text) 연결, SetCard(CardData)로 무늬 기호·색·조커 표시.
  - `Assets/Scripts/HandView.cs`(신규): cardPrefab/cardContainer 연결, ShowHand(cards)로 프리팹 생성, Clear()로 정리.
  - `Assets/Scripts/NetworkManager.cs`: public HandView handView 필드, YourHandMsg 파싱, your_hand 수신 시 handView.ShowHand, 방 나갈 때 Clear.
- **미해결/후속**: 사용자가 에디터에서 Canvas + Card 프리팹 + HandContainer 제작 및 Inspector 연결 필요(가이드 제공). 스프라이트는 추후 프리팹 교체로 반영.
- **다음 단계**: 사용자 에디터 세팅 → Play로 손패 렌더 확인 → 확인되면 스텝5 커밋.

## 2026-07-18 16:35 (UTC+9)

- **작업 요청**: Unity에서 카드는 뜨는데 게임 시작 후에도 대기방 UI(플레이어 리스트/준비 버튼)가 남아있음 → 게임 화면으로 전환 필요.
- **수정 파일**: `mighty-network-unity/Assets/Scripts/NetworkManager.cs`
  - OnGUI 방 화면을 DrawWaitingRoom()/DrawGameHud()로 분리. status=="playing"(또는 gameStarted) 시 게임 HUD로 전환.
  - 게임 중에는 오버레이 패널 축소(320x320), 로그 높이 축소(90), 제목 "게임 중"으로 변경.
  - DrawGameHud: 방 코드 + 상대들 남은 카드 수 + 방 나가기만 표시(준비/시작/Ping 제거). 내 손패는 Canvas 카드로 표시됨.
  - PlayerInfo에 handCount 필드 추가(game_state 파싱).
- **검증**: 린트 통과. 사용자 Play 확인 예정.
- **다음 단계**: 사용자 확인 → 스텝5 커밋.

## 2026-07-18 16:55 (UTC+9)

- **작업 요청**: 계획상 다음 단계 스텝6(턴 기반 카드 내기) 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: startPlay(첫턴/빈테이블), currentTurnPlayer, playCard(턴·손패 검증→테이블 push→턴 순환, 5장차면 새 트릭으로 비움), botPickCardId(무작위) 추가. publicState에 currentTurnClientId/Nickname, tableCards 추가.
  - `server.js`: start_game 시 startPlay+maybeBotPlay. play_card 핸들러(검증→game_state 브로드캐스트→본인 your_hand→maybeBotPlay). maybeBotPlay(700ms 후 봇이 자동으로 냄, 연쇄).
- **Unity 변경**:
  - `CardView.cs`: IPointerClickHandler, Card 프로퍼티, Clicked 콜백.
  - `HandView.cs`: onCardClicked 콜백을 각 카드에 연결.
  - `NetworkManager.cs`: tableView(HandView) 필드, PlayCardMsg, OnHandCardClicked/PlayCard, GameState에 currentTurn·tableCards(TableCardInfo) 파싱, UpdateTable로 테이블 렌더, HUD에 현재 차례 표시. 방 나갈 때 tableView.Clear.
- **검증(자동 ws_test)**: 방 생성→시작→내가 S_Q 냄→봇1~4 자동으로 순서대로 냄→테이블 5장 후 턴 복귀. 정상.
- **미해결/후속**: 스텝7(트릭 승자 판정) 전까지 테이블은 다음 리드 때 비워짐(임시). 마이티 카드내기 규칙(문양 따라내기 등) 미적용.
- **다음 단계**: 사용자 에디터에서 TableContainer/TableView 추가 후 Play 확인 → 스텝6 커밋.

## 2026-07-18 16:52 (UTC+9)

- **작업 요청**: 브라우저(localhost) 테스트 페이지에서 카드가 안 눌림 → test.html에도 스텝6 카드 내기 반영.
- **수정 파일**: `server-node/public/test.html`
  - 손패 카드 클릭 시 play_card 전송(onCardClick, 내 차례 아닐 때 안내). 내 차례일 때만 #hand.myturn으로 클릭 강조.
  - 테이블 영역(#tableArea) 추가: 현재 차례(turnInfo) + 낸 카드 목록(카드 위 닉네임 표시).
  - makeCardEl 공통화, exitRoomView에서 latestState/테이블 초기화.
- **주의**: test.html은 서버가 매 요청마다 읽어 제공하므로 서버 재시작 불필요, 브라우저 강력 새로고침(Cmd+Shift+R)만 하면 됨.
- **다음 단계**: 사용자 브라우저/Unity 확인 → 스텝6 커밋 → 스텝7(트릭 승자).

## 2026-07-18 17:15 (UTC+9)

- **작업 요청**: 스텝7(기본 트릭 승자 판정) 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: rankValue(Card.RANKS 순서), determineTrickWinner(리드 무늬 중 최고 랭크). startPlay에서 trickHistory/lastTrickWinner/trickComplete/wonCards 초기화. playCard: 5장 완성 시 승자 판정→wonCards 적립→trickHistory 기록→lastTrickWinner 갱신→승자가 다음 리드(trickComplete=true, 다음 리드 때 테이블 비움). publicState에 lastTrickWinnerNickname, players.wonCount/trickCount 추가.
  - `server.js`: play_card/봇 플레이에서 trickResult 승자 로그.
- **Unity 변경**: `NetworkManager.cs`: GameState.lastTrickWinnerNickname, PlayerInfo.wonCount/trickCount 파싱. 게임 HUD에 현재차례/직전 트릭 승자/각 플레이어 남은·획득트릭 표시(패널 340x400).
- **test.html**: 직전 트릭 승자(trickWinner), 플레이어별 남은·획득트릭 표시.
- **검증(자동 ws_test)**: 한 판 진행 중 트릭#1~4 승자 판정·wonCount 누적(5→20)·승자 리드 확인. (봇 딜레이로 12초 내 4트릭)
- **미해결/후속**: 특수룰(기루다/마이티/조커/조커콜)은 스텝8. 판 종료(10트릭)/점수는 스텝10.
- **다음 단계**: 사용자 확인 → 스텝7 커밋 → 스텝8(마이티 룰).

## 2026-07-18 17:40 (UTC+9)

- **작업 요청**: 스텝8(마이티 특수 룰) 구현. 룰은 표준룰로 확정.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **서버(server-node) 신규/변경**:
  - `src/game/RuleEngine.js`(신규): makeRuleConfig(trumpSuit,noTrump), canPlayCard(따라내기 강제), determineTrickWinner(마이티>조커>기루다>리드무늬), leadSuitOf. 마이티=기루SPADE면 D_A 아니면 S_A, 조커콜=기루CLUB면 S_3 아니면 C_3, 조커는 첫/마지막 트릭·조커콜 리드 시 무효.
  - `test/ruleEngine.test.js`(신규): 14개 케이스(설정/따라내기/기루다>리드/마이티>기루다/조커 서열/첫트릭 조커무효/조커콜) 전부 통과.
  - `src/RoomManager.js`: 기존 determineTrickWinner/rankValue 제거, RuleEngine 사용. startPlay에서 ruleConfig(임시 기루다 HEART)·trickNumber 설정. playCard에 따라내기 검증, 트릭 완성 시 RuleEngine.determineTrickWinner(trickNumber 반영). botPickCardId(room,player)=합법 카드 중 무작위. publicState에 trumpSuit/mightyCardId/jokerCallCardId/trickNumber 추가.
  - `server.js`: botPickCardId 호출 시 room 전달.
- **Unity 변경**: `NetworkManager.cs`: GameState에 trickNumber/trumpSuit/mightyCardId/jokerCallCardId 파싱. HUD에 기루다·마이티·조커콜·트릭번호 표시(SuitKor/CardKor 헬퍼). 패널 360x460.
- **test.html**: 룰 정보(기루다/마이티/조커콜/트릭번호) 표시.
- **검증**: 유닛테스트 14/14 통과. ws_test로 한 판 10트릭 완주, 마이티/기루다/따라내기 정상 확인.
- **임시/후속**: 기루다는 스텝8 임시 HEART 고정 → 스텝9 입찰에서 실제 선언으로 대체. 노기루/조커 리드 선언은 단순화 상태. 프렌드 공개(revealFriendWhenPlayed)는 구조만.
- **다음 단계**: 사용자 확인 → 스텝8 커밋 → 스텝9(입찰/주공/프렌드).

## 2026-07-19 23:20 (UTC+9)

- **작업 요청**: 서버 가동 후 스텝9(입찰/주공/프렌드) 구현.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **게임 흐름 변경**: `start_game` 시 바로 `playing`이 아니라 `bidding` → `choosing_friend` → `playing` 단계로 진행.
- **서버(server-node) 변경**:
  - `src/RoomManager.js`: 상수 MIN_BID(13)/MAX_BID(20)/SUITS, TEMP_TRUMP_SUIT→FALLBACK_TRUMP_SUIT. 신규 메서드 startBidding/currentBidder/_advanceBidder/placeBid/passBid(단판 한 바퀴 입찰)/resolveBidding(최고공약자=주공, 기루다·목표점 확정, ruleConfig 생성; 전원패스면 redeal)/chooseFriend(주공만, NONE=노프렌드, 소유자 비공개 저장)/botFriendCardId(봇=마이티). startPlay: 주공이 첫 리드(currentTurnIndex=주공), ruleConfig 없으면 폴백. playCard: 프렌드 카드가 나오면 friendRevealed=true 공개. publicState: noTrump/입찰(currentBidder·highestBid)/주공(declarer·targetScore)/프렌드(friendChosen·friendRevealed·friendNickname) 추가.
  - `server.js`: bid/pass_bid/choose_friend 핸들러. sendHandsToHumans/handleBidStep(마감 시 resolve→choosing_friend 전이, redeal 시 재배분)/maybeBotBid(봇 자동 패스, 600ms)/maybeBotChooseFriend/startPlaying 헬퍼. start_game이 dealCards→startBidding→game_state→maybeBotBid로 진행.
- **클라이언트 변경**:
  - `public/test.html`: 입찰 영역(#bidArea: 공약 입력·기루다 select·공약/패스 버튼) + 프렌드 영역(#friendArea: 마이티/노프렌드/직접지정). renderBidding/renderFriend, ruleInfo에 주공·공약·프렌드 표시, bid/pass_bid/choose_friend 전송 핸들러.
  - `Assets/Scripts/NetworkManager.cs`: GameState 확장(noTrump/입찰·주공·프렌드 필드, HighestBid). Bid/PassBid/ChooseFriend 메시지 클래스·전송 메서드. OnGUI를 phase별(bidding→DrawBidding, choosing_friend→DrawChoosingFriend, playing→DrawGameHud)로 분기. DrawGameHud에 주공·공약·프렌드 추가. 손패 클릭은 status=="playing"에서만.
- **검증**:
  - `ws_test.js`(스텝9용 재작성): 입찰(14 SPADE)→봇 패스→주공 확정→프렌드(마이티 D_A)→본게임 진행 확인. 기루다 SPADE→마이티 D_A 자동 변경 확인.
  - 브라우저(test.html): 방생성→준비→시작→bidding(공약14 ♥)→봇 패스→choosing_friend(마이티 프렌드)→playing. ruleInfo "주공 오모나/공약14/기루다♥/마이티♠A/조커콜♣3/프렌드 지정됨(비공개)", 주공이 첫 리드 확인.
- **미해결/후속**: 입찰은 단판 한 바퀴(정식 오름차순 다회전 아님), 봇은 항상 패스(테스트 편의). 점수계산·승패판정은 스텝10.
- **Unity 에디터 작업 필요**: 없음(NetworkManager.cs 스크립트만 갱신, 기존 씬 구성 그대로 동작). Play로 실행하면 입찰/프렌드 UI가 OnGUI에 표시됨.
- **다음 단계**: 사용자 확인 → 스텝9 커밋 → 스텝10(점수 계산/승패 판정).

## 2026-07-19 23:55 (UTC+9)

- **작업 요청**: 딜미스(노게임) 규칙 추가 — 점수카드가 적게 들어오면 다시 돌리기.
- **규칙 확정(사용자 선택)**: 표준 조건 + 수동 선언.
  - 조건: 점수카드(A·K·Q·J·10) 0장, 또는 점수카드가 마이티(♠A) 1장뿐이고 조커도 없을 때. (기루다 결정 전이라 마이티는 기본값 ♠A로 판정)
  - 방식: 조건 맞는 사람이 '딜미스' 버튼으로 선언 → 전원 재배분 후 재입찰.
- **서버 변경**:
  - `src/RoomManager.js`: POINT_RANKS 상수, canDeclareDealMiss(hand), declareDealMiss(room,clientId) 추가.
  - `server.js`: `declare_deal_miss` 핸들러. sendHandsToHumans가 입찰 단계면 your_hand에 canDealMiss 동봉. 재배분 로직을 redealAndRestartBidding(room) 헬퍼로 통합(전원 패스/딜미스 공용). broadcast "deal_miss".
- **클라이언트 변경**:
  - `public/test.html`: #dealMissBtn(조건 충족 시 노출), your_hand.canDealMiss 수신→updateDealMissBtn, declare_deal_miss 전송. deal_miss/bid_result/friend_chosen 로그 추가.
  - `Assets/Scripts/NetworkManager.cs`: YourHandData.canDealMiss 파싱, myCanDealMiss 보관, DrawBidding에 딜미스 버튼(조건 충족 시), DeclareDealMiss() 전송.
- **검증**: canDeclareDealMiss 단위 테스트 5케이스 통과(0장/마이티만=true, 마이티+조커/타A만/KQ=false). 랭크 "10" 표기 Card.js와 일치.
- **다음 단계**: 사용자 확인 → 스텝9(입찰/프렌드/딜미스) 커밋 → 스텝10(점수 계산/승패 판정).

## 2026-07-20 00:15 (UTC+9)

- **작업 요청**: 봇 입찰 로직 부재 지적("봇이 어떻게 공약을 내는지"). 기존엔 봇이 무조건 패스였음.
- **서버 변경(`src/RoomManager.js`)**:
  - `_evaluateHandForBid(hand)`: 무늬별 기루다 가정 손패 평가. tricks = 9 + 기루다장수*0.7 + 높은끗(A/K/Q)*0.7 + 오프수트A*0.9 + 마이티(1.5) + 조커(1.3). 최고 무늬를 기루다 후보로.
  - `botDecideBid(room, player)`: 추정치(floor, ≤20)가 13 미만이면 패스, 현재최고+1(minAllowed)을 못 넘기면 패스, 이길 수 있으면 minAllowed만 보수적으로 공약.
  - `botFriendCardId(room)`: 자기가 안 가진 카드 중 마이티>조커>에이스 순 선택(주공 자기자신 프렌드 방지).
- **서버 변경(`server.js`)**: `maybeBotBid`가 botDecideBid로 공약/패스 결정(검증 실패 시 패스 폴백).
- **휴리스틱 튜닝 검증(2000판 시뮬)**: 평균 개인추정 12.98, 판당 누군가 13+ 가능 100%, 판별 최고추정 분포 대부분 14~15(16+ 소수). → 매 판 주공이 나오되 과도한 공약은 드묾.
- **통합 검증(ws_test)**:
  - 사람 패스 시: 봇2가 13 SPADE로 주공 → 봇이 프렌드 지정 → 10트릭 완주.
  - 사람 14 SPADE 시: 테스터 주공, 프렌드(D_A) 소유자 봇4로 트릭1에서 공개(프렌드=타인 정상).
- **미해결/후속**: 봇 입찰은 단순 휴리스틱(오름차순 다회전 아님, 노기루 미사용). 점수계산(스텝10) 붙으면 공약 성공/실패 판정으로 튜닝 필요.
- **다음 단계**: 사용자 확인 → 스텝9 커밋 → 스텝10.

## 2026-07-20 00:35 (UTC+9)

- **작업 요청**: "전원 패스 시 공약 수치가 줄어드는 룰" 확인 요청.
- **규칙 조사(웹)**: 나무위키/위키백과/우만위키 확인 — 표준은 "전원 패스 → 재딜", 최소공약 13(5마, 지역따라 14/12). "전원 패스 시 공약 하향"은 표준 문서엔 없고 하우스룰/일부 구현 방식. 사용자 선택: **변형(하향)** 적용.
- **적용 규칙**: 전원 패스 시 같은 패로 **최소공약을 1 낮춰 재입찰**, 바닥(11)까지 내려가도 전원 패스면 그때 **재딜**.
- **서버 변경**:
  - `src/RoomManager.js`: BID_FLOOR(11) 상수. startBidding(room, startMinBid=13)로 room.minBid 세팅. placeBid/botDecideBid가 room.minBid 기준으로 검증/결정. resolveBidding: 전원 패스 시 minBid>11이면 {lowerBid,newMin}, 아니면 {redeal}. publicState에 minBid 추가.
  - `server.js`: handleBidStep에 lowerBid 분기(재배분 없이 startBidding(newMin)→재입찰), broadcast "bid_lowered".
- **클라이언트 변경**:
  - `public/test.html`: bidStatus에 "최소공약 N" 표시, 입력 min/기본값을 minBid로, bid_lowered 로그.
  - `Assets/Scripts/NetworkManager.cs`: GameState.minBid 파싱, DrawBidding에 "최소 공약" 표시 및 입력 하한 보정.
- **검증(단위)**: resolveBidding 하향(13→12→11) 및 바닥 재딜, 봇이 최소11에서 약패(추정11)로 11 공약하는지 확인. 일반 ws_test 흐름 회귀 없음.
- **미해결/후속**: 최소공약 시작 13/바닥 11은 상수(원하면 방 옵션화 가능).
- **다음 단계**: 사용자 확인 → 스텝9 커밋 → 스텝10.

## 2026-07-20 23:18 (UTC+9)

- **작업 요청**: 서버 재가동.
- **작업 디렉토리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network/server-node`
- **명령**: `node server.js` (백그라운드)
- **결과**: WebSocket 서버 `ws://localhost:3000` 정상 기동. 테스트 페이지 `http://localhost:3000`.
- **다음 단계**: 사용자 요청 시 스텝10(점수 계산/승패 판정) 진행.

## 2026-07-20 23:25 (UTC+9)

- **작업 요청**: 빈칸 프렌드 지정 오류 수정 + 특정 플레이어 프렌드 선언.
- **서버**: `chooseFriend`가 빈값/공백을 노프렌드로 취급하던 버그 수정(명시적 `NONE`만 노프렌드). `friendClientId`로 플레이어 프렌드 추가(즉시 공개, 주공 자신 불가). `friendType`: card|player|none.
- **클라이언트**: test.html/Unity에 플레이어 버튼 목록, 빈 카드 입력 클라이언트 가드.
- **검증**: 빈값/공백 오류, NONE/플레이어/카드/자기자신 단위 테스트 통과. 서버 재기동.

## 2026-07-20 23:30 (UTC+9)

- **작업 요청**: 프렌드 지정 후 "무슨 프렌드인지"는 즉시 공개되어야 함.
- **변경**: publicState에 friendCardId 공개(카드 프렌드). 소유자(friendNickname)만 friendRevealed 전까지 비공개. UI: "마이티 프렌드 (♠A) (소유자 비공개)" / 공개 후 "→ 닉네임". 조커 프렌드 버튼 추가.
- **파일**: RoomManager.js, server.js, test.html, NetworkManager.cs

## 2026-07-20 23:50 (UTC+9)

- **작업 요청**: 스텝10(점수 계산/승패 판정) 구현.
- **서버 신규**: `src/game/Scoring.js` — scoreOfCards, calculateResult(주공팀=주공+공개프렌드, 미공개 카드프렌드=주공단독, 바닥패점수는 주공팀, 목표점수 이상이면 주공승).
- **RoomManager**: playCard 마지막 트릭 handOver, finishGame(status=finished+lastResult), returnToWaiting(대기 초기화).
- **server.js**: handOver 시 finishAndBroadcast(game_finished), return_to_lobby 핸들러, maybeBotPlay에서도 종료 처리.
- **클라이언트**: test.html 결과 패널+대기방 복귀, NetworkManager DrawFinished/game_finished/ReturnToLobby.
- **검증**: scoring.test.js 통과. ws_test 10트릭 후 `판종료: 승=수비팀 주공팀=8/14 수비팀=12 바닥패=1` 확인.
- **미구현(후속)**: 주공 바닥패 교환(먹고 3장 버리기), 런/백런 배수 점수. 다음=스텝11 재접속.

## 2026-07-20 23:55 (UTC+9)

- **작업 요청**: 주공+프렌드 승리까지 몇 점 필요한지 잘 보이게.
- **변경**: publicState에 진행 중 declarerTeamScore/defenderTeamScore/kittyScore/pointsNeeded. HUD에 "★ 주공팀 승리 조건: N점 이상 (현재 X점 / 앞으로 Y점)" 표시. 결과 화면에도 목표·부족 점수 강조.

## 2026-07-21 00:05 (UTC+9)

- **작업 요청**: 개발 순서 확정 — 1) 바닥패 교환 → 2) 런/백런 → 3) 스텝11. 1번 구현.
- **흐름**: bidding → **exchanging_kitty** → choosing_friend → playing.
- **서버**: startKittyExchange(주공 손패+3), discard_kitty(정확히 3장), botPickDiscardIds, discardedKitty=묻힌 카드(점수=주공팀). beginFriendSelection/maybeBotDiscardKitty.
- **클라이언트**: test.html/Unity에서 카드 3장 선택 후 버리기 UI.
- **검증**: ws_test — 입찰마감(봇2 15) → 바닥패 버리기 완료 → 10트릭 → 판종료 주공팀 15/15.
- **다음**: 런/백런 배수 점수.

## 2026-07-21 00:10 (UTC+9)

- **작업 요청**: 손패 받을 때 무늬/숫자별 정렬.
- **정렬 규칙**: ♠→♥→♦→♣→조커, 같은 무늬는 A>K>...>2.
- **서버**: Card.sortHand, dealCards/kitty/discard/playCard 후 정렬. sendHandsToHumans에서도 정렬.
- **클라이언트**: HandView.SortCards, test.html sortHandClient.

## 2026-07-21 00:05 (UTC+9)

- **작업 요청**: 여기까지 커밋·푸시 후 서버 종료.
- **커밋**: `498bd36` Step 10: scoring/finish, kitty exchange, live goal HUD, hand sort
- **푸시**: `origin/main` (`b93872e..498bd36`)
- **서버**: 포트 3000 종료 완료.
- **다음**: 런/백런 → 스텝11.

## 2026-07-21 23:35 (UTC+9)

- **작업 요청**: 서버 기동 + 런/백런 배수 점수 구현.
- **규칙**: 런=주공팀 20점(×2), 백런=주공팀≤10점(×2). 승리 시 노기루/노프렌드도 각 ×2(중첩). stakeBase/stakeTotal 정산 단위 표시.
- **파일**: Scoring.js, scoring.test.js, server.js 로그, NetworkManager/test.html 결과 UI.
- **검증**: scoring.test.js 통과(일반/백런/런+노기+노프=8배).
- **다음**: 스텝11 재접속.

## 2026-07-21 23:58 (UTC+9)

- **작업 요청**: 누적 스코어보드 — 계획에 없어서 지금 추가.
- **정산**: 주공 2배/프렌드 1배/야당 1배(영합). 노프렌드면 주공=야당수×단위. 런·백런 등 multiplier 적용.
- **누적**: player.sessionScore, finishGame에서 applySessionScores. 대기방 복귀해도 유지(퇴장 시 소멸).
- **UI**: 결과 화면 정산/누적, 대기·플레이 목록에 누적 점수. md_files/10에 계획 추가.
- **검증**: scoring.test.js 통과.

## 2026-07-22 00:01 (UTC+9)

- **작업 요청**: 새 플레이어 입장 시 점수 정책 + 방장 초기화 버튼.
- **정책**: 기존 멤버 sessionScore 유지, 신규 조인 0점. 퇴장 시 해당 플레이어 누적 소멸.
- **구현**: `reset_scores` (대기중·방장만). RoomManager.resetSessionScores → scores_reset + game_state.
- **UI**: test.html / Unity 대기방에 "점수 초기화(방장)" 버튼.
- **서버**: 포트 3000 재기동.
- **다음**: 스텝11 재접속, 또는 커밋/푸시.

## 2026-07-22 00:12 (UTC+9)

- **작업 요청**: 여기까지 커밋·푸시.
- **커밋**: `1ae2bb2` Add run/backrun multipliers, session scoreboard, and host score reset.
- **푸시**: `origin/main` (`958ec35..1ae2bb2`)
- **포함**: 런/백런, 세션 누적 스코어보드, 방장 reset_scores, UI/문서/테스트.
- **다음**: 스텝11 재접속.

## 2026-07-23 22:41 (UTC+9)

- **작업 요청**: 다음 개발 내용 안내.
- **답변**: 다음 우선순위는 스텝11(재접속·개인서버 안정화), 이후 스텝12(WebGL·라즈베리파이 배포).
- **파일 수정 없음** (질의 응답만).

## 2026-07-23 22:43 (UTC+9)

- **작업 요청**: 스텝11 재접속 구현 방식 제안.
- **권장**: soft disconnect(좌석 유지) + reconnectToken 복구 + clientId 유지 + your_hand/game_state 재전송. 의도적 leave는 즉시 퇴장.
- **유예**: 60~120초 후 좌석 회수. 봇 대타/관전자는 후순위.
- **파일 수정 없음** (설계 논의).

## 2026-07-23 22:44 (UTC+9)

- **작업 요청**: 끊김 시 봇 대타 여부 확인.
- **답변**: 아직 미구현. 권장 A는 soft hold(자리만 유지)이고 봇 대타는 선택 옵션. 사용자 확인 대기.

## 2026-07-23 22:47 (UTC+9)

- **작업 요청**: HTML로 개발하는지 vs Unity로 옮겨야 하는지(카드 52개 에디터 배치 등) 구조 질문.
- **답변**: test.html은 서버 검증용 임시 클라이언트. 최종 클라이언트는 Unity WebGL. 카드는 서버 데이터 + CardView 프리팹 동적 생성. HTML→Unity 이식/스킨 전환이 아님.
- **파일 수정**: WORK_LOG.md만 append.
- **다음**: 없음 (질의 응답).

## 2026-07-23 22:50 (UTC+9)

- **작업 요청**: 끊김 시 봇 대타(옵션2)로 스텝11 재접속 구현.
- **서버**: softDisconnect, reconnect(clientId 유지), isBotControlled, 유예 5분(대기=제거/게임중=영구봇).
- **봇**: 입찰·바닥패·프렌드·카드내기를 끊긴 사람 자리에서도 진행.
- **클라**: Unity/test.html 자동 재접속, leave 시 토큰 삭제.
- **테스트**: reconnect.test.js / scoring.test.js 통과.
- **다음**: 서버 재기동 확인, 수동 끊김 테스트, 커밋 여부 확인.

## 2026-07-23 22:52 (UTC+9)

- **작업 요청**: 끊김 유예 시간 안내 + 카운트다운 표시.
- **유예**: 5분(`RECONNECT_GRACE_MS`).
- **UI**: publicState에 reconnectExpiresAt/reconnectGraceMs, test.html·Unity 목록에 `mm:ss` 카운트다운.

## 2026-07-23 22:52 (UTC+9)

- **작업 요청**: 카드 날아가기/배분 애니메이션을 넣고 싶으면 어떻게?
- **답변**: 애니메이션은 Unity 클라이언트 전용. 서버는 상태만 통지. 현재 ShowHand 즉시 갱신을 트윈/코루틴으로 교체. DOTween 등 권장. test.html은 선택.
- **파일 수정**: WORK_LOG.md만 append.

## 2026-07-23 22:53 (UTC+9)

- **작업 요청**: 재접속 시 동일 인물 판별 방식 설명.
- **답변**: reconnectToken(방 입장 시 발급, 클라 저장)으로 서버가 좌석 매칭. clientId는 재접속 시 기존 좌석 id로 재바인딩.

## 2026-07-23 22:54 (UTC+9)

- **작업 요청**: 애니메이션 등은 md 13번까지 끝난 뒤 추가로 하면 되는지 확인.
- **답변**: 맞음. 1~12가 핵심 기능/배포, 13은 워크플로 가이드. 연출·스킨은 그 이후 polish.
- **파일 수정**: WORK_LOG.md만 append.

## 2026-07-23 22:56 (UTC+9)

- **작업 요청**: 끊김 감지 방식 설명 + 뒤로가기 시 즉시 미감지 문제.
- **원인**: close 미발생 시 heartbeat(기존 15s)까지 대기. test.html에 unload close 없음.
- **수정**: pagehide/beforeunload에서 socket.close, heartbeat 10초, Unity pause/destroy 시 Close.

## 2026-07-23 22:58 (UTC+9)

- **작업 요청**: 마이티 플레이어 순서/자리 배치 룰 질문.
- **답변**: 표준 룰상 주공이 마음대로 자리 재배치하는 룰은 없음. 좌석은 고정, 순서는 시계방향. 첫 선은 보통 주공. 다음판 입찰 시작 위치 등은 로컬룰.

## 2026-07-23 23:01 (UTC+9)

- **작업 요청**: 자리 섞기 구현 방식 제안.
- **권장**: 대기중·방장만 seat shuffle (players 배열 permute). 옵션으로 판 종료 후 자동 로테이션/주공 기준 재배치.

## 2026-07-23 23:03 (UTC+9)

- **작업 요청**: A안 자리 섞기 버튼 (게임 시작 전·방장).
- **구현**: shuffle_seats → RoomManager.shuffleSeats (Fisher-Yates), test.html/Unity 버튼.
- **검증**: 방장·대기만 가능, 비방장/게임중 거부.

## 2026-07-23 23:06 (UTC+9)

- **작업 요청**: 여기까지 커밋·푸시.
- **커밋**: `7a08f42` Add reconnect with bot takeover, disconnect countdown, and seat shuffle.
- **푸시**: `origin/main` (`cd8677f..7a08f42`)
- **포함**: soft disconnect+봇대타, 5분 유예/카운트다운, unload close, shuffle_seats, reconnect.test.js.

## 2026-07-23 23:09 (UTC+9)

- **작업 요청**: 다음 할 일 안내.
- **답변**: 계획상 스텝12 WebGL·라즈베리파이 배포. 11 잔여(닉네임 중복·관전자)는 선택.

## 2026-07-23 23:10 (UTC+9)

- **작업 요청**: 스텝11 잔여 개발. 관전자는 범위에서 제외.
- **구현**: uniqueNickname(동건→동건2). 문서에서 관전자 제외. room_created/joined에 nickname, 변경 시 안내.
- **검증**: 동건/동건2/동건3 + 봇 닉네임 충돌 없음.

## 2026-07-23 23:16 (UTC+9)

- **작업 요청**: 스텝12 배포 vs Unity 비주얼/애니메이션 선후 조언.
- **권장**: 먼저 얇은 배포(접속·재접속·플레이 검증) 후 스프라이트/애니 반복. 배포 전엔 WebGL용 서버주소 분리 정도만.

## 2026-07-23 23:18 (UTC+9)

- **작업 요청**: WebGL 배포 시작 여부 + 라즈베리파이 정보 필요 여부.
- **답변**: WebGL 빌드/로컬 검증은 Pi 정보 불필요. Pi 배포 단계에서 SSH·IP·OS·도메인/터널 정보 필요.

## 2026-07-23 23:19 (UTC+9)

- **작업 요청**: 서버 주소 설정 분리 여부 확인.
- **답변**: Inspector public serverUrl만 있음(기본 localhost). WebGL용 빌드/런타임 설정·URL 쿼리 등은 미구현.

## 2026-07-23 23:22 (UTC+9)

- **작업 요청**: 서버 URL 설정 분리 + WebGL 빌드 체크리스트.
- **구현**: ServerUrlResolver (?ws= → PlayerPrefs → WebGL 호스트 → Inspector), 로비 UI 저장·재연결.
- **문서**: md_files/12 체크리스트·URL·로컬 테스트 절차. public/webgl placeholder.

## 2026-07-23 23:26 (UTC+9)

- **작업 요청**: 유니티에서 프리팹 만들라는 건지 확인.
- **답변**: URL 설정/WebGL 단계에서는 프리팹 불필요. NetworkManager 기존 GO + WebGL 빌드만.

## 2026-07-23 23:28 (UTC+9)

- **작업 요청**: 현재 단계 Unity에서 할 일 상세 안내.
- **답변**: 프로젝트 열기 → 컴파일 확인 → (선택) Play로 URL UI 확인 → WebGL Switch/Build → public/webgl 복사 → 브라우저 테스트.

## 2026-07-23 23:31 (UTC+9)

- **작업 요청**: Unity CS0103 Math 컴파일 오류 수정.
- **수정**: NetworkManager.cs Math.Max → Mathf.Max.

## 2026-07-23 23:35 (UTC+9)

- **작업 요청**: Unity Build Settings(WebGL) 상세 안내.
- **답변**: Switch Platform, Scenes In Build, Player Settings, Build 폴더, 주의사항 단계별 설명.

## 2026-07-23 23:46 (UTC+9)

- **작업 요청**: public/webgl 기대 파일 구조 설명.
- **답변**: Unity WebGL 빌드 루트(index.html, Build/, TemplateData/)를 public/webgl/에 그대로 복사.

## 2026-07-23 23:46 (UTC+9)

- **후속**: public/webgl/index.html이 placeholder여서 testbuild/index.html로 교체.

## 2026-07-23 23:48 (UTC+9)

- **문제**: /webgl/ 디렉터리 readFile → 404. index.html은 200이었음.
- **수정**: 디렉터리면 index.html, MIME(.wasm 등) 설정. 서버 재기동.

## 2026-07-23 23:50 (UTC+9)

- **작업 요청**: WebGL 화면이 작아 테스트 어려움.
- **수정**: public/webgl 캔버스 거의 전체창, NetworkManager HUD 패널/폰트 Screen 기준 확대.
- **안내**: 브라우저 강력 새로고침. UI 코드 반영은 WebGL 재빌드 필요.

## 2026-07-23 23:52 (UTC+9)

- **문제**: WebGL에서 한글 미표시.
- **원인**: 기본 GUI 폰트에 한글 글리프 없음.
- **수정**: NotoSansKR Resources/Fonts + NetworkManager GUI.skin.font 적용.
- **필요**: Unity에서 WebGL 재빌드 후 public/webgl 복사.

## 2026-07-23 23:53 (UTC+9)

- **작업 요청**: Unity에서 한글 폰트·재빌드 절차 상세 안내.

## 2026-07-24 00:02 (UTC+9)

- **작업 요청**: 여기까지 커밋·푸시.
- **커밋**: `9058d3b` Add WebGL deploy prep: server URL config, Korean font, and static hosting.
- **푸시**: `origin/main` (`b92c84b..9058d3b`)
- **참고**: `public/webgl/Build/`는 gitignore (로컬 빌드 복사). 한글 폰트 OTF 포함.

## 2026-07-24 16:30 (UTC+9)

- **작업 요청**: 다음에 뭘 해야 하는지.
- **답변**: 로컬 WebGL 검증 완료 상태. 선택지 — Pi 배포(12) / 카드 UI·애니 / 커밋된 상태 유지 후 플레이테스트.

## 2026-07-24 16:32 (UTC+9)

- **문제**: WebGL에서 카드 숫자만 보이고 문양(♠♥♦♣) 안 보임.
- **원인**: UI Text/폰트에 슈트 심볼 글리프 없음.
- **수정**: CardView/NetworkManager 표시를 S/H/D/C로 변경, 카드 Text에 Noto 폰트 적용. 빨강/검정은 색으로 구분.
- **필요**: WebGL 재빌드 후 public/webgl 복사.

## 2026-07-23 23:59 (UTC+9)

- **작업 요청**: 나무위키 마이티 룰과 현재 구현 비교 참고점 확인.
- **참고 문서**: https://namu.wiki/w/마이티 , https://namu.wiki/w/마이티/세부_규칙 , https://mightyfriend.net/?p=25 , https://mightyfriend.net/?p=56
- **이미 일치**: 마이티/조커콜 정의, 서열, 조커 첫·막턴 무효, 런/백런, 노기·노프 배수(승), 자리섞기(호스트).
- **차이/미구현**: 초구 기루다 금지, 조커 리드 무늬선언, 조커콜 선택 활성화, 바닥패 후 기루변경(+1/+2), 딜미스 0.5점식, 백런 로컬기준 등.

## 2026-07-31 23:11 (UTC+9)

- **작업 요청**: 표준 5마 기준으로 2·3·5번만 반영 (조커 리드 무늬선언, 조커콜 선택, 딜미스 세부).
- **서버**: RuleEngine — declaredSuit follow, activateJokerCall(+조커강제/마이티예외), dealMiss 0.5점식; RoomManager playCard 옵션; 봇 리드 옵션; 입찰/패스 후 딜미스 불가.
- **클라**: Unity NetworkManager + test.html 선택 UI; ws_test 대응.
- **테스트**: `node test/ruleEngine.test.js` 23개 통과.
- **문서**: md_files/08_mighty_rules.md 표준 5마 반영 메모.
- **미반영(의도적)**: 초구 기루다 금지, 바닥패 후 기루변경, 백런 로컬배수.
2026-07-31 23:13 (UTC+9)

## 2026-07-31 스프라이트 필요 목록 안내

- **요청**: 트럼프 53장 보유 상태에서 추가 필요 스프라이트 문의.
- **답변**: 필수/권장/선택 우선순위로 안내 (카드백, UI 버튼, 기루다 아이콘, 테이블 배경 등). 코드 변경 없음.

## 2026-08-03 22:59 (UTC+9)

- **작업**: 카드 스프라이트 시트(320×448, 10×6, 54장) 연동.
- **시트 순서**: ♠A..K → ♦ → ♥ → ♣ → JOKER → BACK (A 먼저).
- **추가**: `Resources/Cards/card_sheet.png` (+meta max 4096), `CardSpriteAtlas.cs`, `CardView` 스프라이트 표시/`SetFaceDown`.
- **원본**: `Assets/Sprites/card.ss.png` 유지.
- **확인**: Unity에서 Play 후 손패 그림 표시. 무늬 순서가 어긋나면 시트 순서만 알려주면 수정.

## 2026-08-03 23:01 (UTC+9)

- **요청**: Cursor 업데이트 위해 서버 일시 종료.
- **조치**: PID 1686 (port 3000) kill. 포트 해제 확인.

## 2026-08-03 23:02 (UTC+9)

- **결과**: 서버(PID 1686, :3000) 종료 완료. 포트 비움.

## 2026-08-03 23:02 (UTC+9)

- **알림**: 백그라운드 서버 태스크 exit 143 (SIGTERM) — Cursor 업데이트용 수동 종료와 동일 원인. 이상 아님.

## 2026-08-03 23:03 (UTC+9)

- **요청**: 서버 재기동.
- **조치**: `server-node`에서 `node server.js` 백그라운드 기동 (PID 29321). :3000 대기.

## 2026-08-03 23:06 (UTC+9)

- **요청**: 상대 손패를 뒷면 스프라이트+닉네임으로 표시.
- **추가**: OpponentHandsView (좌/상/우 배치), HandView.ShowFaceDown, NetworkManager game_state 연동.
- **동작**: 내 손패=앞면(handView), 상대=BACK 스프라이트×handCount + 이름(턴 하이라이트).

## 2026-08-03 23:08 (UTC+9)

- **질문**: 카드 렌더링 방식 / 스크린 비율 스케일 필요 여부.
- **답변**: CanvasScaler(1920×1080)로 전체 스케일. 카드는 프리팹 고정 70×98(상대는 36×50). 스프라이트 320×448은 Rect에 맞춰 축소될 뿐. 작게 보이는 건 기준 크기가 작아서 — % 높이 기반 스케일 권장.

## 2026-08-03 23:09 (UTC+9)

- **질문**: 픽셀 깨짐 방지 = 원본 픽셀(320×448) 또는 n배 크기?
- **답변**: 픽셀아트 원칙상 맞음(정수배 + Point 필터). CanvasScaler·Bilinear면 깨짐/번짐. 320은 1배도 큼 → 보통 시트 다운스케일 자산 + 정수배, 또는 Point+정수배 UI.

## 2026-08-03 23:11 (UTC+9)

- **요청**: Point 필터 + 손패 160×224.
- **적용**: card_sheet Point(meta+런타임), Card 프리팹 160×224, HandView 강제 사이즈, HandContainer 1700×240 spacing -40, Table 900×240, 상대 손패 80×112.

## 2026-08-03 23:13 (UTC+9)

- **버그**: 에디터 Play 시 PlayerPrefs reconnectToken으로 진행 중 게임에 자동 재입장.
- **수정**: 에디터 기본 `autoReconnectInEditor=false` — Play 시작 시 토큰 삭제, OnOpen/OnClose 자동 재접속 스킵. WebGL은 기존대로. 재접속 테스트 시 Inspector에서 체크.

## 2026-08-03 23:15 (UTC+9)

- **요청**: 여기까지 커밋·푸시.
- **커밋**: `27555f2` Add standard 5마 rule gaps and card sprite UI.
- **푸시**: SSH로 `origin/main` 반영 (`78c1d53..27555f2`). HTTPS remote는 인증 실패 → tracking은 fetch로 동기화.

## 2026-08-03 23:21 (UTC+9)

- **문제**: Sprites/card.ss.png 수정해도 게임에 미반영.
- **원인**: 런타임은 Resources/Cards/card_sheet.png 로드 (복사본이 구버전).
- **조치**: 시트 동기화 복사 + CardSpriteAtlas Play 시 정적 캐시 리셋. 스크립트 재실행 불필요, Play 재시작하면 됨.

## 2026-08-03 23:24 (UTC+9)

- **문제**: Unity Player에서 판 종료 후 대기방 복귀 안 됨.
- **원인**: OnGUI 결과 상세가 길어 하단「대기방으로 돌아가기」버튼이 패널 밖으로 잘림.
- **수정**: 버튼을 상단 고정 + 상세 스크롤, 8초 후 자동 return_to_lobby.

## 2026-08-03 23:28 (UTC+9)

- **질문**: 특정 폰트로 리더보드/점수판 UI 가능 여부 (구현 요청 아님).
- **답변**: 가능. Resources 폰트 + UI Text/TextMeshPro 또는 기존 Noto 경로. 세션 점수는 이미 game_state/game_finished에 있음.

## 2026-08-03 23:29 (UTC+9)

- **논의**: 추가 TODO 초안 — (1) 상시 리더보드 (2) 기루다/마이티/조커콜 표시 (3) 실시간 점수카드 시각화(손패 앞).
- **메모**: sessionScore·룰 id·팀점수는 이미 game_state에 있음. UI/시각화 작업 위주.

## 2026-08-03 23:29 (UTC+9)

- **질문**: 리더보드용 폰트 파일 형식.
- **답변**: TTF/OTF 권장. Resources/Fonts 배치. WebGL은 한글 포함 폰트 필요. TMP 쓰면 Font Asset 생성.

## 2026-08-03 23:32 (UTC+9)

- **요청**: 테이블 카드 낸 순서 유지 + 내 차례 못 내는 카드 음영.
- **수정**: HandView.ShowCardsInOrder, CardView.SetPlayable, CardPlayLegality, NetworkManager 테이블/하이라이트 연동.

## 2026-08-03 23:36 (UTC+9)

- **질문**: 조커 선 낼 때 카드 활성/비활성 판정 로직 확인.
- **결론**: 조커 리드=선언 무늬 따라내기(+마이티/조커 예외). 조커콜 활성=조커(또는 마이티)만. 「특정 카드만」은 보통 선언 무늬(+마이티)가 켜진 정상 동작.

## 2026-08-03 23:37 (UTC+9)

- **확인**: 조커 선 리드 시 그 트릭에 한해 따라낼 무늬 선언 — 맞음. 이미 declaredSuit로 구현됨.

## 2026-08-03 23:39 (UTC+9)

- **요청**: 여기까지 커밋·푸시 후 서버 종료.
- **커밋**: `8ccad63` Improve Unity play UX: table order, illegal-card dim, lobby return.
- **푸시**: `origin/main` (`531c4f8..8ccad63`, SSH).
- **서버**: PID 29327 종료, :3000 해제.

## 2026-08-03 23:39 (UTC+9)

- **알림**: 서버 태스크 exit 143 — 요청하신 수동 종료(SIGTERM)와 동일. 이상 아님.

## 2026-08-04 22:33 (UTC+9)

- **요청**: 서버 재기동.
- **조치**: `node server.js` 기동 (PID 55634). ws://localhost:3000 대기.

## 2026-08-05 14:52 (UTC+9)

- **질문**: 커서 종료 후 서버 상태.
- **결과**: :3000 비어 있음 — 서버 내려간 상태.

## 2026-08-05 14:53 (UTC+9)

- **질문**: 커밋/푸시할 내용 있는지.
- **결과**: 코드는 origin/main과 동기화. 미커밋은 WORK_LOG.md만.

## 2026-08-05 14:54 (UTC+9)

- **질문**: 이전에 리스트업한 추가 개발 항목 회상.
- **답변**: (1) 상시 리더보드 (2) 기루다/마이티/조커콜 표시 (3) 실시간 점수카드 시각화.

## 2026-08-05 16:12 (UTC+9)

- **질문**: 카드 스프라이트 적용이 프리팹+스크립트 자동인지 수동 오브젝트 생성인지.
- **답변**: Card 프리팹 1장 + CardSpriteAtlas가 시트에서 런타임 슬라이스/매핑. HandView가 인스턴스화 후 SetCard.

## 2026-08-05 16:43 (UTC+9)

- **요청**: 서버 기동.
- **조치**: `node server.js` (PID 66230). ws://localhost:3000.

## 2026-08-09 16:12 (UTC+9)

- **요청**: 복귀 후 서버 기동 + 작업 시작.
- **조치**: `node server.js` 기동. 대기 중인 TODO: 리더보드 / 기루다·마이티·조커콜 표시 / 점수카드 시각화 — 우선순위 확인 중.

## 2026-08-09 16:14 (UTC+9)

- **요청**: Fonts/ 갈무리 11로 전체 폰트 업데이트.
- **적용**: UiFonts → Galmuri11. NetworkManager OnGUI, CardView, OpponentHandsView, Card.prefab Label.

## 2026-08-09 16:16 (UTC+9)

- **요청**: 카드 겹침 구분용 우측·하단 그림자.
- **적용**: CardView에 UI Shadow (offset 5,-5 / alpha 0.4). 스프라이트 수정 불필요.

## 2026-08-09 16:16 (UTC+9)

- **요청**: 카드 우측·하단 그림자 적용 확인/강화.
- **조치**: Card.prefab에 UI Shadow 컴포넌트 추가, CardView OnEnable에서 설정 동기화.

## 2026-08-09 16:18 (UTC+9)

- **질문**: 겹친 카드 구분용 그림자가 적용됐는지 확인.
- **답변**: 예. 카드마다 우측·하단 UI Shadow로 겹침 시 뒤 카드가 살짝 비쳐 구분.

## 2026-08-09 16:18 (UTC+9)

- **요청**: 카드 그림자 더 크게.
- **적용**: offset 10/-10, alpha 0.65 (CardView + prefab).

## 2026-08-09 16:20 (UTC+9)

- **요청**: 우하단 그림자 유지 + 좌/상 그림자 offset 5 추가.
- **적용**: Shadow 3개 — BR(10,-10,a0.65), L(-5,0,a0.4), T(0,5,a0.4).

## 2026-08-09 16:26 (UTC+9)

- **요청**: 상대(작은) 카드 그림자도 크기 비율로 축소.
- **적용**: CardView 그림자 = DisplayWidth 대비 sizeDelta 스케일. 상대 0.5배면 offset도 절반. size 변경 후 RefreshDropShadow.

## 2026-08-09 16:29 (UTC+9)

- **요청**: 카드 제출 시 핸드→테이블 이동 애니. 상대는 작은 손패가 아니라 테이블 크기 카드가 좌석 쪽에서 날아옴.
- **작업 디렉터리**: mighty-network-unity/Assets/Scripts
- **추가/수정**:
  - `CardPlayAnimator.cs` (신규): ease-out 위치·크기 보간 비행
  - `HandView.cs`: 카드 월드 좌표 / 테이블 슬롯 좌표
  - `OpponentHandsView.cs`: 닉네임→좌석 월드 좌표
  - `NetworkManager.cs`: play_card 시 시작점 스냅샷+손패 낙관적 제거, game_state에서 상대 손패 먼저 갱신 후 테이블 애니
- **결과**: 본인=손패에서 날아감, 상대=좌석에서 테이블 사이즈로 날아와 중앙에 모임

## 2026-08-09 16:31 (UTC+9)

- **요청**: 테이블 카드 아래에 제출자 닉네임 표기.
- **적용**: HandView.ShowTableCards — 카드+닉 세로 슬롯(Galmuri11). NetworkManager.UpdateTable이 TableCardEntry로 갱신. 긴 닉 8자 절삭.

## 2026-08-09 17:03 (UTC+9)

- **요청**: 카드 배치 결정 방식 / 작은 화면 겹침 가능성 설명.
- **분석**: 손패·테이블=고정 160×224 + HLG(손패 spacing -40 의도적 겹침, 테이블 +8). CanvasScaler 1920×1080 Match Width. 반응형 축소 없음. 상대=앵커 % + overlap 22. 좁은 높이/장수 많으면 영역 간·화면 밖 겹침 가능.

## 2026-08-09 17:05 (UTC+9)

- **요청**: 테이블 제출 카드 아래 닉네임 폰트 키우기.
- **적용**: HandView TableLabelFontSize 14→20, TableLabelHeight 22→28.

## 2026-08-09 17:11 (UTC+9)

- **요청**: 트릭 승리 토스트+점수카드 이동 애니, 플레이어별 점수/주공/마이티 표시.
- **서버**: `trickComplete`, player `score`/`isDeclarer`/`isMightyPlayer`, `mightyRevealed`/`mightyPlayerNickname`. 마이티 제출 시 공개. 트릭 후 봇 딜레이 1.8s.
- **Unity**: `TrickWinAnimator` (토스트+점수카드 비행). OpponentHandsView/Self에 점수·[주공]·[마이티]. NetworkManager 연동.
- **서버**: node server.js 재기동.

## 2026-08-09 18:02 (UTC+9)

- **요청**: 승리 토스트 흰 배경, 텍스트 사라지며 비전수 fade / 점수카드는 중앙→승자 이동.
- **적용**: TrickWinAnimator 시퀀스 재작성. HandView HidePointCardSlots + CoFadeNonPointSlots.

## 2026-08-09 18:39 (UTC+9)

- **요청**: 승리 문구 → 검정 박스 + 흰 글씨 + Galmuri7.
- **적용**: TrickWinAnimator toastBg 검정, toastText 흰색, Fonts/Galmuri7.

## 2026-08-09 18:41 (UTC+9)

- **요청**: OnGUI 창 화면 가운데 + 좌상단 기루다/마이티/조커콜/주공/프렌드 HUD.
- **적용**: OnGUI BeginArea 중앙 정렬. GameRuleHud(Canvas) 좌상단. DrawGameHud에서 중복 룰 문구 정리.

## 2026-08-09 18:44 (UTC+9)

- **요청**: 승리 문구 Galmuri11 복귀. 게임 시작 시 중앙 OnGUI를 우상단 최소화 토글.
- **적용**: TrickWinAnimator 폰트 Primary(Galmuri11). hudCollapsed — 시작 시 자동 최소화, 「메뉴 열기」「최소화」 토글.

## 2026-08-09 18:46 (UTC+9)

- **요청**: 공약 단계는 메뉴 펼침, 본게임(playing)만 최소화, 종료 시 자동 펼침.
- **적용**: hudCollapsed는 status==playing 진입 시에만 true, finished/bidding/kitty/friend/waiting이면 false.

## 2026-08-09 22:21 (UTC+9)

- **요청**: 서버 끊김 후 재시작 시 손패 ~20장 버그 — 초기화 로직 점검.
- **원인 후보**: (1) HandView Clear가 spawned만 지워 컨테이너 고아 잔존 (2) startKittyExchange 중복 concat (3) 방 입장 시 클라 손패 미클리어.
- **수정**: dealCards가 wonCards/테이블도 리셋. kitty 교환 idempotent. addPlayer hand:[]. HandView Clear가 컨테이너 자식 전부 제거. 클라 ResetLocalHandState + your_hand 중복 id 제거.

## 2026-08-09 22:25 (UTC+9)

- **요청**: 서버 재시작.
- **조치**: :3000 프로세스 종료 후 `server-node/node server.js` 재기동.

## 2026-08-09 22:26 (UTC+9)

- **질문**: 출마 성공 시 패를 한장씩 걷었다가 다시 나누는 룰이 문서에 있는지.
- **결론**: 프로젝트 문서/5마 구현에 없음. 출마 성공=바닥패 3장 교환. 재배분은 딜미스·전원패스(바닥) 때만. (6마의 제외자 패 재배분과는 별개)

## 2026-08-09 22:27 (UTC+9)

- **요청**: 지금까지 변경 커밋 후 푸시.
- **커밋**: `a4de8f3` Add play animations, rule HUD, and hand reset hardening. (34 files)
- **푸시**: 실패 — HTTPS GitHub 인증 없음 (`could not read Username for https://github.com`). 로컬은 origin/main 대비 1커밋 ahead.

## 2026-08-09 22:29 (UTC+9)

- **질문**: 13 공약 후 남이 14 하면 15로 올릴지 묻는 로직이 없는 것 같다.
- **확인**: 맞음. 현재 입찰은 **한 바퀴 단판**(각자 1회 공약/패스). 표준식 연속 재입찰(올려 부르기) 미구현. WORK_LOG에도 "오름차순 다회전 아님" 명시.

## 2026-08-09 22:30 (UTC+9)

- **요청**: 입찰을 계속 올릴 수 있게 (연속 재입찰).
- **적용**: RoomManager — passedClientIds, 패스 제외 시계방향 진행, 최고공약자 외 전원 패스 시 종료. publicState nextMinBid. Unity/test.html 올릴 최소 표시.

## 2026-08-09 22:33 (UTC+9)

- **질문**: 바닥패가 3장 받아 총 6장 중 3장 선택하는 룰인지.
- **답**: 아님. 5마 표준·현 구현은 손패10+바닥3=13장 중 아무 3장 버림(다시 10장).

## 2026-08-09 22:38 (UTC+9)

- **요청**: 바닥패 버리기를 손패 클릭으로 / 선택 시 카드 위로 / 출마 성공 시 중앙 바닥패→주공 핸드 애니 / 딜 후 중앙에 바닥패 3장.
- **서버**: `RoomManager.publicState`에 `kittyCount` 추가 (내용은 비공개).
- **Unity**:
  - `KittyView.cs` — 입찰 중 중앙 뒷면 3장, 주공 확정 시 손패/상대 좌석으로 비행.
  - `CardView` — `SetSelectedRaised` (LateUpdate Y+32).
  - `HandView` — `ApplyDiscardSelectionRaise`.
  - `NetworkManager` — OnGUI 카드 버튼 제거(손패 클릭만), 13장 손패는 비행 후 반영, 버리기 확정 버튼만 유지.
- **서버**: kittyCount 반영 위해 node 재기동.
- **다음**: Unity Play로 입찰→주공→버리기 UX 확인.

## 2026-08-09 22:50 (UTC+9)

- **요청**: 출마 성공 시 승리 토스트와 동일 형식의 당선 문구 + 비주공 \"카드 고르는 중...\" + 봇 주공도 ~2초 고르는 시간.
- **Unity**: `TrickWinAnimator`에 Announce/ShowStickyToast/ClearStickyToast. `NetworkManager`가 `bid_result`/입찰→교환 전환 시 \"{닉}가 당선되었습니다!\n공약: N장\" 표시, 비주공은 fade 후 sticky \"카드 고르는 중...\".
- **서버**: `BOT_KITTY_DELAY = 2000` (봇 discard 대기).
- **서버 재기동**: 반영.

## 2026-08-09 22:53 (UTC+9)

- **요청**: 주공에게도 박스 안내 \"버릴 카드 3장을 선택하세요.\" + 이때부터 메뉴 최소화.
- **적용**: 당선 토스트 후 주공 sticky 안내 / 비주공 \"카드 고르는 중...\". `exchanging_kitty` 진입 시 HUD 자동 최소화. 축소 UI에 선택 n/3 + 3장 버리기 버튼 유지.

## 2026-08-09 22:56 (UTC+9)

- **버그**: 버릴 카드 선택이 안 되거나 올림 표시가 안 됨.
- **원인**: HorizontalLayoutGroup이 LateUpdate 이후 카드 Y를 덮어씀 → 선택 올림이 렌더에 반영되지 않음. Self 닉네임 Text raycast가 손패와 겹칠 수 있음.
- **수정**: `CardView` — `Canvas.willRenderCanvases`에서 raise 재적용 + 선택 시 tint. `OpponentHandsView` nameText.raycastTarget=false. 버리기 중 HandContainer를 맨 앞으로.

## 2026-08-09 22:58 (UTC+9)

- **버그**: 내 차례가 아닐 때도 카드 클릭으로 내기 가능(또는 그렇게 보임).
- **원인**: `RefreshHandPlayability`가 내 차례가 아닐 때 `SetAllPlayable(true)`로 클릭을 열어 둠. 낙관적 제거 후 서버 거절 시 손패 미복구.
- **수정**: 내 차례 아니면 `SetAllPlayable(false)`. `PlayCard` 직전 턴 재검증. 서버 `play_card` 실패 시 `your_hand` 재전송.

## 2026-08-09 23:00 (UTC+9)

- **요청**: 토스트 텍스트 상자 좌우 여백 확대.
- **적용**: `TrickWinAnimator` 텍스트 inset 16→48, 박스 560→620.

## 2026-08-09 23:03 (UTC+9)

- **요청**: 게임 종료 시 중앙 테이블 패 숨김. 카드 비행 도착점을 실제 5장 배치 위치와 일치.
- **적용**: `game_finished`/`status==finished`에서 tableView.Clear. 테이블을 항상 5슬롯 고정 배치(HLG 끄고 수동 좌표). 비행 endPos=`GetTableSlotWorldPosition(i, playerCount)`.

## 2026-08-09 23:04 (UTC+9)

- **요청**: Unity 기본 파란 배경 → 진한 초록.
- **적용**: SampleScene Main Camera `m_BackGroundColor` → 딥 포레스트 그린 (≈#0A381F).

## 2026-08-09 23:07 (UTC+9)

- **버그**: 카드 비행 가로 위치는 맞는데 애니 종료 후 위로 순간이동.
- **원인**: 도착 Y를 수식(라벨 보정)으로 추정 → 실제 닉네임 라벨 포함 슬롯 Y와 불일치.
- **수정**: 최종 테이블을 먼저 배치 후 마지막 슬롯만 숨기고, 그 CardView 실제 world position으로 비행 → 도착 시 슬롯 표시.

## 2026-08-09 23:09 (UTC+9)

- **요청**: 중앙 텍스트 잠깐 비활성화해 애니 이상 원인 확인.
- **적용**: `HandView.showTableNicknames = false` (테이블 닉네임 OFF). 카드만 y=0 슬롯에 배치. 닉네임 켤 때는 컬럼을 -labelH/2 내려 카드 중심이 동일하게 y=0.

## 2026-08-09 23:10 (UTC+9)

- **버그**: 첫 트릭 이후 중앙패 안 보임(애니만 재생).
- **원인**: 닉네임 OFF 시 카드가 TableContainer 직속인데, 트릭승 `HidePointCardSlots`가 parent(컨테이너)를 SetActive(false).
- **수정**: 슬롯 루트가 컨테이너면 카드 자신만 숨김. Clear/Show 시 컨테이너 재활성화.

## 2026-08-09 23:14 (UTC+9)

- **요청**: 닉네임 텍스트 재활성화 + 애니 도착점 불일치 원인 조사.
- **원인**: 테이블 슬롯이 VerticalLayoutGroup 의존 → 생성 직후(ForceUpdate 포함) 레이아웃이 확정되기 전 CardView.worldPosition을 찍어 endPos로 씀. 애니 종료 후 레이아웃이 잡히며 Y로 점프. 닉네임 라벨 자체가 직접 원인은 아님.
- **수정**: 닉네임 ON. VLG 제거, 카드 피벗=슬롯 원점·닉네임은 카드 아래 절대좌표 배치. 비행 타깃=카드 피벗과 동일.

## 2026-08-09 23:15 (UTC+9)

- **버그**: 애니 도착점과 중앙패 위치 여전히 불일치.
- **원인**: 비행 카드는 Canvas `CardFlyLayer`(스트레치) 소속, 테이블 카드는 `TableContainer` 소속 → 월드 position 대입이 UI에서 어긋남.
- **수정**: `CardPlayAnimator`가 목적 카드와 **같은 부모**에서 `anchoredPosition` 보간. 목적 카드/닉네임은 alpha=0 후 착지 시 복구.

## 2026-08-09 23:19 (UTC+9)

- **요청**: 업데이트한 카드 스프라이트 다시 반영.
- **조치**: `Assets/Sprites/card.ss.png` → `Assets/Resources/Cards/card_sheet.png` 동기화 복사 (3200×2688, 동일 바이트). 런타임은 Resources 경로만 로드.
- **확인**: Unity Play 재시작 시 CardSpriteAtlas 캐시 리셋으로 새 시트 적용.

## 2026-08-09 23:20 (UTC+9)

- **버그**: 닉네임 텍스트 사라짐 + 1턴 이후 애니 없음(카드는 보임).
- **원인**:
  1) `showTableNicknames=false`가 씬에 직렬화되어 닉 미생성.
  2) 애니 busy 중 다음 game_state가 `ShowTableCards`로 Clear → dest 파괴 → busy 고착/애니 스킵.
- **수정**: 닉네임 항상 생성. 테이블 애니는 순차 큐(`CoProcessTableAnimQueue`)로 Clear 타이밍 보호. CardPlayAnimator도 큐+finally.

## 2026-08-09 23:26 (UTC+9)

- **요청**: 스프라이트 재반영 + 점수/바닥패 애니를 사람 방향 화면 밖으로 + 내 닉/점수 손패 가림 해소.
- **스프라이트**: card.ss.png → Resources/Cards/card_sheet.png 동기화.
- **애니**: `BeyondSeatWorld`로 좌석을 지나 화면 밖까지 연장. 트릭승·kitty FlyToTarget에 적용. 비행 시간 약간 증가.
- **Self HUD**: Y 0.22→0.34, Canvas 맨 앞으로 재부모화, 패널/폰트 약간 확대.

## 2026-08-09 23:29 (UTC+9)

- **버그**: 2번째 순서일 때 첫 사람 패 애니 후 안 보임 → 내가 내면 보임.
- **원인**: 비행 중 dest `CanvasGroup.alpha=0`이 복구되지 않거나 잔류. 다음 ShowTableCards 때만 새 인스턴스로 보임.
- **수정**: Graphic.color alpha로 숨김/복구 + CanvasGroup 정리. 애니 종료 후 `ShowTableCards`로 테이블 강제 재배치.

## 2026-08-09 23:34 (UTC+9)

- **요청**: 닉/점수를 손패 아래 배치, 토스트와 동일 폰트, 패 장수 제거, 좌상단 주공팀·공약 표기.
- **수정**:
  - `OpponentHandsView`: Self Y 0.06(손패 아래), 상대도 닉을 카드 아래. `FormatStatusLine`에서 패 N장 제거. fontSize=40(토스트와 동일).
  - `GameRuleHud`/`NetworkManager`: 주공팀(마이티 공개 전=주공 개인점, 후=`declarerTeamScore`), 공약(`targetScore`/`highestBid`) 행 추가. 패널 높이 220.
- **다음**: Unity Play로 손패 아래 레이아웃·좌상단 HUD 확인.

## 2026-08-09 23:35 (UTC+9)

- **요청**: 닉/점수 폰트를 이전·좌상단 UI와 동일하게.
- **수정**: `OpponentHandsView` StatusFontSize 40→15 (GameRuleHud body와 동일). Self/Opp 패널 크기 축소.

## 2026-08-09 23:41 (UTC+9)

- **요청**: 여기까지 커밋 푸시.
- **포함**: kitty/play UX, 테이블 애니, 손패 아래 닉·점수, 좌상단 주공팀·공약 HUD, 카드 시트 동기화, KittyView 등.

## 2026-08-13 23:03 (UTC+9)

- **요청**: 서버 재기동 + 최근 작업 요약.
- **서버**: `server-node` `node server.js` → ws/http `localhost:3000`.
- **최근(8/9)**: kitty/플레이 UX·테이블 애니·손패 아래 닉·좌상단 주공팀/공약 HUD 후 `3cbdb4c` 푸시. 이후 새 작업 없음.

## 2026-08-13 23:14 (UTC+9)

- **요청**: Unity+Node 딜/핸드 분배·카드 애니 패턴 탐색 리포트.
- **작업 디렉터리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **조사**: `server.js`/`RoomManager.js`/`Deck.js`, `NetworkManager.cs`, `HandView`/`OpponentHandsView`/`CardPlayAnimator`/`TrickWinAnimator`/`KittyView`.
- **결과**: 딜 시점=start_game·redeal(딜미스/바닥패스); 클라 즉시 ShowHand/Show(handCount); 딜 애니 스텁 없음; 훅 권장=`game_started`/bidding 진입 + kitty식 pendingHand.

## 2026-08-13 23:19 (UTC+9)

- **요청**: 게임 시작/재시작 시 셔플+딜 애니.
- **추가**: `DealAnimator.cs` (중앙 셔플 → 5좌석 라운드로빈 뒷면 딜).
- **연동**: `NetworkManager` — `game_started`/`deal_miss`/재배분 `your_hand`(10장)에서 손패 보류 후 애니, 완료 시 공개. 애니 중 상대 handCount=0·키티/입찰 UI 대기.
- **확인**: Unity Play → 게임 시작 또는 딜미스/전원패스 재배분.

## 2026-08-13 23:24 (UTC+9)

- **버그**: 딜 애니가 한 명(한 지점)으로만 날아감.
- **원인**: 상대 좌석을 닉네임 패널 `position` 조회로만 잡아 실패 시 전부 (0,0) 또는 동일 좌표.
- **수정**: `OpponentHandsView.TryGetAnchorWorldPosition`으로 고정 4석 앵커→월드 변환. 셔플 직후 `BuildDealSeatTargets` 재계산. 나=손패 중앙, 상대=좌/상좌/상우/우.

## 2026-08-13 23:26 (UTC+9)

- **버그**: 게임 시작 직후(딜 전) 「바닥패」 텍스트가 보임.
- **원인**: `game_state`가 `your_hand`/딜 애니보다 먼저 오면 `UpdateKittyPile`이 즉시 표시.
- **수정**: `IsDealInProgress`(pending/busy)면 kitty Clear·표시 스킵. 딜 완료 후에만 바닥패 표시.

## 2026-08-13 23:29 (UTC+9)

- **버그**: 딜/점수패 비행이 가운데·왼쪽·오른쪽만 (상단 2석이 좌우와 중복).
- **원인**: 좌석 좌표를 패널/`root.rect` TransformPoint로 구해 Y가 붕괴.
- **수정**: Canvas `GetWorldCorners` 보간으로 5앵커 좌표. `TryResolveSeatWorld`(플레이어 시계방향 인덱스)로 딜·트릭승·키티·제출 시작점 통일.

## 2026-08-13 23:37 (UTC+9)

- **리뷰/수정**: 비행 목적지 Y가 동일해 보이던 문제.
- **원인**: Screen Space Overlay + CanvasScaler 환경에서 `transform.position`(월드)로 비행 → Y가 뭉개지거나 좌우만 구분됨.
- **수정**: 좌석은 정규화 앵커(왼 0.52 / 상 0.90 / 오 0.52 / 나 0.14). 비행은 `flyLayer.anchoredPosition` 로컬 보간. 딜·트릭승·키티 동일.

## 2026-08-13 23:39 (UTC+9)

- **요청**: 애니 도착지 디버그 표시.
- **추가**: `SeatDebugOverlay` — 노란 십자=좌석(나/왼/상좌/상우/오+좌표), 청록=beyond(화면 밖이면 가장자리 클램프). `NetworkManager.debugSeatMarkers=true` 기본 ON.

## 2026-08-13 23:43 (UTC+9)

- **관찰**: 노란 마커(좌석)는 5곳 정상, 카드는 좌·우 중석으로만 비행.
- **원인 추정**: DealFlyLayer가 DealAnimatorRoot 하위라 활성 직후 rect/좌표가 마커(Canvas 직속)와 달랐음.
- **수정**: DealFlyLayer를 Canvas 직속으로. 비행 전 1프레임 대기. 실제 endLocal에 **마젠타 점+닉** 표시 + Console `[DealFly]` 로그.
- **확인**: 노란 마커와 마젠타 FLY 점이 겹치는지, 카드가 그쪽으로 가는지.

## 2026-08-13 23:46 (UTC+9)

- **원인(확정)**: `CardView.ApplyRaiseAfterLayout`가 `willRenderCanvases`마다 `anchoredPosition.y`를 0(또는 raise 36)으로 강제 → 비행 카드가 수평만 이동.
- **수정**: `SetFlightMode(true)`로 비행 중 raise 보정 비활성. Deal/Trick/Kitty/CardPlay/덱 셔플 카드에 적용.

## 2026-08-13 23:50 (UTC+9)

- **요청**: 딜을 1P부터 한 장씩 순회 + 착지 시 뒷면 손패 1장씩 생성.
- **수정**: `DealAnimator` 순차 yield 딜. `OnDealCardLanded` → `ShowFaceDown`/상대 handCount 증가. 완료 시 내 손패 앞면 공개.

## 2026-08-13 23:52 (UTC+9)

- **요청**: 딜 중 중앙 더미 흔들림, 남은 더미→바닥패 펼침, 딜 속도 ~3배.
- **수정**: `flyDuration` 0.07 / `dealGap` 0.005. `CoShakeDeckLoop` 딜 중 유지. `TrimDeckTowardRemain`으로 더미 감소 후 `CoSpreadKittyFromDeck(3)`.

## 2026-08-13 23:54 (UTC+9)

- **요청**: 바닥패 텍스트 제거(위치 밀림), 좌석 디버그 마커 OFF.
- **수정**: `KittyView`/`DealAnimator` hint 비표시. `debugSeatMarkers=false`.

## 2026-08-13 23:59 (UTC+9)

- **버그**: 바닥패 3장 펼침 후 살짝 위로 점프.
- **원인**: DealDeck y=36 → KittyPile y=40 교체 + Kitty HLG 높이 240.
- **수정**: DealDeck y=40으로 통일, Kitty pile 높이를 카드 크기에 맞춤.

## 2026-08-14 00:04 (UTC+9)

- **버그**: 바닥패 펼침 종료 후에도 위로 점프.
- **원인**: ClearDeck 후 KittyView가 HLG로 재생성 + CardView raise Y 보정.
- **수정**: 펼친 카드를 `AdoptFaceDown`(worldPositionStays)로 인계. Kitty HLG 제거·절대배치. FlightMode 유지.

## 2026-08-14 00:09 (UTC+9)

- **버그**: 딜 애니가 입장/패스/공약 때마다 반복 재생.
- **원인**: `pass_bid` 등도 `your_hand`(10장) 재전송 → 클라이언트가 재배분(`isRedeal`)으로 오인.
- **수정**: 애니는 `game_started`/`deal_miss`/`redeal`일 때만. 서버 `redealAndRestartBidding`에 `redeal` 브로드캐스트 추가. 서버 재기동.

## 2026-08-14 00:09 (UTC+9)

- **요청**: 딜미스 시 패를 가운데로 모은 뒤 재배분 + 테스트용 딜미스 버튼/서버 강제 허용.
- **클라이언트**: `DealAnimator.PlayCollectToCenter` 회수 애니. `NetworkManager`는 `deal_miss`/`redeal`에서 회수→클리어→재딜. `forceDealMissButton=true`로 입찰 HUD에 「딜미스 [테스트 강제]」 표시.
- **서버**: `RoomManager.FORCE_DEAL_MISS_FOR_TEST=true` (조건/기행 무시 허용). 서버 :3000 재기동.
- **테스트 후**: `FORCE_DEAL_MISS_FOR_TEST=false`, `forceDealMissButton=false`로 되돌릴 것.

## 2026-08-14 00:09 (UTC+9)

- **수정**: `RoomManager.js`에서 클래스 본문에 잘못 넣은 `const FORCE_DEAL_MISS_FOR_TEST` → 모듈 상단 상수로 이동 (SyntaxError 해결). 서버 재기동 확인.

## 2026-08-14 00:12 (UTC+9)

- **요청**: 딜미스 회수 애니가 좌석별 순차가 아니라, 손패가 한 장씩 줄면서 5명이 라운드마다 동시에 한 장씩 쏘도록.
- **수정**: `DealAnimator.CoCollectToCenter` — 라운드 루프 + 좌석 병렬 `CoFlyOneLocalThenAppend`, `onCardCollected` 콜백. `NetworkManager`는 회수 중 `dealProgressCounts`로 손패 감소 표시.

## 2026-08-14 00:15 (UTC+9)

- **요청**: 딜미스 회수 시에도 가운데 모이는 패 더미가 보이고, 회수 애니 끝난 뒤 흔들리는 더미가 유지되도록.
- **수정**: 카드 발사 즉시 `AppendDeckCardVisual`. 회수 종료 후 `BeginIdleDeckShake`로 더미 유지. `CoPlay`는 모인 더미를 재사용해 셔플·재딜.

## 2026-08-14 00:17 (UTC+9)

- **요청**: 강제 딜미스 테스트 조건 제거.
- **서버**: `FORCE_DEAL_MISS_FOR_TEST` 및 관련 분기 삭제 → 정상 점수/기행 조건만 허용. 서버 재기동.
- **클라**: `forceDealMissButton` 제거, 딜미스 버튼은 `myCanDealMiss`일 때만 표시.

## 2026-08-14 00:18 (UTC+9)

- **커밋/푸시**: `613bafc` — deal/redeal 비행 애니 + 딜미스 회수→재딜, 서버 `redeal` 브로드캐스트. `main` → `origin/main` 푸시 완료.

## 2026-08-15 23:02 (UTC+9)

- **요청**: 서버 재기동.
- **결과**: `server-node/server.js`를 `:3000`에서 재시작. WebSocket 정상 기동.

## 2026-08-15 23:04 (UTC+9)

- **요청**: 딜미스 시 검정 박스 흰 글씨로 「닉네임 딜미스! / 카드 재분배 중...」 표시. 테스트용 강제 딜미스 버튼 재활성화.
- **클라**: `ShowDealMissToast` sticky 토스트, 딜 애니 완료 시 해제. `forceDealMissButton=true`.
- **서버**: `FORCE_DEAL_MISS_FOR_TEST=true` 복구 후 :3000 재기동.

## 2026-08-15 23:06 (UTC+9)

- **요청**: 딜미스 강제 테스트 해제.
- **서버**: `FORCE_DEAL_MISS_FOR_TEST` 제거, 정상 점수/기행 조건만 허용. :3000 재기동.
- **클라**: `forceDealMissButton` 제거, 조건 충족 시에만 버튼 표시.

## 2026-08-16 22:41 (UTC+9)

- **요청**: 서버 기동 + 카드/문양/주공·프렌드 아이콘 시트 적용.
- **리소스**: `Resources/Icons/icon_card|shape|etc.png` (128×64 / 64×64). `IconSpriteAtlas` + `IconGui`.
- **UI**: 좌상단 HUD 기루다·마이티·조커콜 아이콘. 입찰 기루 선택 / 프렌드 카드 선택 / 조커 무늬·조커콜을 아이콘 버튼으로 대체. 주공(왕관)·프렌드(F)를 닉네임·테이블 제출자 옆에 표시.
- **서버**: `:3000` 기동.

## 2026-08-16 22:48 (UTC+9)

- **요청**: 주공/프렌드 아이콘을 카드 상단으로. 새 아이콘 스케일을 마이티(96×48, 정사각 48×48)와 통일. 좌상단 HUD 조커콜 아래 겹침 해소.
- **수정**: `IconSpriteAtlas.DisplayCard/DisplaySquare`. `GameRuleHud` 행 간격·패널 높이 확대. 테이블/상대 손패/내 손패 카드 위에 역할 아이콘.

## 2026-08-16 22:57 (UTC+9)

- **수정**: `HandView.cs`에서 `FormatTableNickname` 메서드 시그니처가 잘려 CS1519 등 컴파일 오류 발생 → 복구.

## 2026-08-16 23:08 (UTC+9)

- **수정**: `GameRuleHud.cs`에서 누락된 `uiFont`/`canvasGroup` 필드 복구.

## 2026-08-16 23:15 (UTC+9)

- **요청**: 좌상단에서 주공/프렌드 텍스트 제거. 핸드 쪽은 이름 아래에 아이콘, 테이블 카드는 상단 유지.
- **수정**: `GameRuleHud` body는 주공팀/공약만. `OpponentHandsView` 이름 아래 역할 아이콘. 손패 위 배지 표시 중단.

## 2026-08-16 23:23 (UTC+9)

- **수정**: `OpponentHandsView.BuildPanel`에서 `rrt` 중복 선언(CS0136) → `roleRt`로 변경. 연결 끊김은 컴파일 오류로 Play 종료 시 발생.

## 2026-08-16 23:26 (UTC+9)

- **요청**: 주공/프렌드(핸드 이름) 아이콘을 이름·점수 좌측으로.
- **수정**: `OpponentHandsView` NameRow — 아이콘 왼쪽 세로 중앙, 이름/점수는 그 오른쪽.

## 2026-08-16 23:55 (UTC+9)

- **요청**: 핸드 아이콘을 더 오른쪽+이름/점수 좌측 정렬. 플레이어 프렌드면 프렌드카드 없음. 공약 제출 시 중앙 토스트(기루 아이콘). 내 차례 아니면/제출 후 메뉴 숨김.
- **수정**: OpponentHandsView 클러스터. GameRuleHud `friendCardNone`. TrickWinAnimator.AnnounceBid. 입찰 대기 시 HUD 접고 방 나가기만.

## 2026-08-17 00:02 (UTC+9)

- **수정**: `dealMissToastVisible` 필드 복구 (공약 토스트 키 추가 시 덮어씀).

## 2026-08-17 00:15 (UTC+9)

- **요청**: 중앙 패 주공/프렌드 아이콘을 카드와 같이 비행. 봇 이름 노태우/김영삼/김대중/이승만.
- **작업 디렉터리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **수정**: `CardPlayAnimator` — 비행 카드에 역할·조커무늬 배지 부착, 목적지 TopIcon은 비행 중 숨김. `NetworkManager.AnimateToTable`에 isDeclarer/isFriend/declaredSuit 전달. `RoomManager.addBot` 고정 닉네임 풀.
- **다음**: 봇 이름 반영을 위해 Node 서버 재시작. 이미 방에 있는 봇은 새 방에서만 새 이름.

## 2026-08-17 00:12 (UTC+9)

- **요청**: 서버 재시작 여부 확인.
- **명령**: PID 13288(구 서버) 종료 후 `server-node`에서 `node server.js` 재기동.
- **결과**: `WebSocket server running on ws://localhost:3000`. 봇 이름은 새 방부터 적용.

## 2026-08-17 00:16 (UTC+9)

- **요청**: 공약 제출·당선 토스트를 약 1초 더 길게. 당선에 기루 아이콘+장수 표시.
- **수정**: `TrickWinAnimator` 홀드 1.7→2.7(제출), 당선 1.35→2.35. `AnnounceElection`이 기루 아이콘+N장. `NetworkManager`가 bid_result/state의 trump 전달.

## 2026-08-17 00:22 (UTC+9)

- **요청**: 테스트용으로 내 손에 조커+조커콜 고정, 기루다 고정.
- **수정**: `RoomManager.js` `DEBUG_FORCE_JOKER_HAND` — 사람 손에 JOKER+C_3, 입찰 마감 시 주공=사람·기루다 HEART 13. 봇은 패스. 딜/키티 후에도 재주입.
- **다음**: 확인 끝나면 `DEBUG_FORCE_JOKER_HAND`를 false.

## 2026-08-17 00:26 (UTC+9)

- **요청**: 조커콜/조커 무늬 선택 UI가 안 보임.
- **원인**: 선택은 OnGUI 게임 HUD 안에 있었고, 플레이 중 HUD는 기본 최소화라 숨겨짐.
- **수정**: `DrawPlayExtraChoice` 중앙 오버레이. 최소화 상태에서도 조커 리드 무늬·조커콜 여부 표시.

## 2026-08-17 00:27 (UTC+9)

- **요청**: 조커 무늬/조커콜 선택을 메뉴가 아니라 팝업으로.
- **수정**: `PlayChoicePopup.cs` uGUI 중앙 팝업(딤+검정 박스). 조커 리드 시 무늬 아이콘, 조커콜 카드 리드 시 사용/일반. OnGUI 메뉴 선택 UI 제거.

## 2026-08-17 00:32 (UTC+9)

- **요청**: 조커/조커콜 강제 디버그 해제. 선택이 필요할 때만 메뉴 활성.
- **수정**: `DEBUG_FORCE_JOKER_HAND=false`. OnGUI 전체 메뉴는 대기/내 입찰/프렌드/종료만. 그 외는 우상단 방 나가기(+주공 키티 버리기). 서버 재시작.

## 2026-08-17 00:35 (UTC+9)

- **요청**: 메뉴에서 로그 제거. 셔플/딜 중·주공 버리기 직후(비주공 프렌드 대기) 메뉴 숨김.
- **수정**: OnGUI 로그 스크롤 삭제. `IsDealInProgress`면 메뉴 비활성. `choosing_friend`는 주공만 메뉴.

## 2026-08-17 00:39 (UTC+9)

- **요청**: 글씨 크기 전체 약 1.5배.
- **수정**: `UiFonts.Size` (Scale 1.5). HUD/핸드 이름/테이블 닉/토스트/팝업/OnGUI/딜 힌트에 적용. HUD·토스트 박스도 조금 키움.

## 2026-08-17 10:42 (UTC+9)

- **요청**: 폰트 1.5배에 맞춰 UI 박스 가로 확대 (줄바꿈 해소).
- **수정**: `UiFonts.Layout`. 좌상단 HUD(라벨 열 160), 로비/입찰 OnGUI 패널·입력, 토스트·조커 팝업, 상대 이름 패널, 딜/바닥패 힌트 너비 확대.

## 2026-08-17 10:49 (UTC+9)

- **요청**: 조커 이미 나왔으면 조커콜 카드는 일반 제출. 조커 선언 무늬 표시 확인.
- **확인**: 테이블 `HandView`/`CardPlayAnimator`가 `declaredSuit` 아이콘을 카드 위에 표시 중.
- **수정**: 서버 `jokerPlayed` 플래그 + publicState. 클라 조커콜 팝업 스킵. 봇도 조커 출현 후 activateJokerCall 안 함.

## 2026-08-17 10:53 (UTC+9)

- **요청**: icon_etc 3번째 회색 F — 미공개 프렌드 본인 표식, 공개 시 보라 F.
- **수정**: Resources `icon_etc` 갱신. `GetFriendSecret`. 공개 전 본인만 회색, 공개 후 보라. 이름/테이블/비행 배지 반영.

## 2026-08-17 10:56 (UTC+9)

- **요청**: 왼쪽 좌석 역할 아이콘 잘림 → 이름 UI 오른쪽 이동. 공약 선택 시 서버 정보 숨김.
- **수정**: 왼쪽 좌석 피벗/클러스터 왼쪽 정렬로 안쪽 펼침. 입찰 차례에 Mighty/연결/서버 라벨 숨기고 입찰 UI 간소화.

## 2026-08-17 11:01 (UTC+9)

- **요청**: 좌상단 HUD를 약 3/5로 축소, 프렌드카드 아이콘 오른쪽에 딱 맞게.
- **수정**: `GameRuleHud` 너비를 라벨+아이콘(+없음) 기준으로 동적 맞춤. 행 간격·라벨 열·글자 소폭 축소.

## 2026-08-17 11:04 (UTC+9)

- **요청**: 대기실 — 서버 정보 제거, 버튼 2×2, 설명 제거, Ping→1초 지연 표시, 방 나가기 유지.
- **수정**: `DrawWaitingRoom` 레이아웃. `Update`에서 waiting 중 1초 자동 ping, pong RTT ms 표시.

## 2026-08-17 11:07 (UTC+9)

- **요청**: 왼쪽 좌석 이름만 오른쪽 이동, 핸드 카드 위치는 유지.
- **수정**: 패널 피벗 중앙 복구. 이름 클러스터만 `nameShiftX`로 안쪽 이동.

## 2026-08-17 11:09 (UTC+9)

- **요청**: 로비·대기실 상자 아래 여백 축소, 버튼 가로 폭 통일.
- **수정**: 로비/waiting `panelH`를 콘텐츠에 맞게. 버튼·입력 `ExpandWidth`로 동일 폭.

## 2026-08-17 11:12 (UTC+9)

- **요청**: 행이 달라도 버튼 가로 폭을 박스 기준 동일하게(하드코딩).
- **수정**: `guiColW = (panelW - 24 - gap) / 2`. 로비·대기실 모든 2열 버튼에 `GUILayout.Width(guiColW)` 적용.

## 2026-08-17 00:41 (UTC+9)

- **요청**: 여기까지 커밋·푸시.
- **작업 디렉터리**: `/Users/omona/Documents/gameDEV/mighty-network/mighty-network`
- **포함**: 아이콘 HUD, 역할 배지 비행, 공약/당선 토스트, 조커 팝업, 메뉴 선택시에만 표시, UI 1.5배, 봇 이름.

- **결과**: `ed58f5a` 커밋 후 `origin/main` 푸시 완료.

## 2026-08-17 10:39 (UTC+9)

- **요청**: 서버 재기동.
- **명령**: `server-node`에서 `node server.js`.
- **결과**: `WebSocket server running on ws://localhost:3000`.

## 2026-08-17 11:15 (UTC+9)

- **요청**: 이름 UI를 핸드 바로 아래, 내 이름은 손패와 겹치지 않게 살짝 조절.
- **수정**: `OpponentHandsView.BuildPanel` — 상대 이름은 카드 하단+6px, 내 이름은 `SelfHandAnchor` 기준 카드 하단+12px.
- **파일**: `mighty-network-unity/Assets/Scripts/OpponentHandsView.cs`

## 2026-08-26 14:31 (UTC+9)

- **요청**: 서버 재기동.
- **명령**: `server-node`에서 `node server.js`.
- **결과**: WebSocket 서버 `ws://localhost:3000` 기동 확인.

## 2026-08-26 14:32 (UTC+9)

- **요청**: master에 최신 커밋 없으면 최근 수정사항 커밋·푸시.
- **결과**: `2b3be9c` 커밋 후 `origin/main` 푸시 완료.

## 2026-08-26 15:12 (UTC+9)

- **요청**: 주공/프렌드·아이디·점수용 박스, 양옆 핸드 90° 회전으로 공간 확보.
- **수정**: `OpponentHandsView` — InfoBox(반투명 배경), 좌/우 카드 행 ±90° 회전·박스는 안쪽 배치, 상단은 카드 아래.
- **파일**: `mighty-network-unity/Assets/Scripts/OpponentHandsView.cs`

## 2026-08-26 15:21 (UTC+9)

- **요청**: 상대 핸드 절반만 보이게 가장자리로, 내 핸드는 상단 ~80% 보이게.
- **수정**: SeatAnchors를 화면 끝(0/1)으로. SelfHandVisibleFraction=0.8로 HandContainer Y 도킹. 내 InfoBox는 카드 위로.
- **파일**: OpponentHandsView.cs, HandView.cs, SampleScene.unity

## 2026-08-26 15:28 (UTC+9)

- **요청**: 내 핸드를 화면/캔버스 기준 정확히, 상대 카드 크기를 내 핸드와 동일하게.
- **수정**: `HandView`가 Canvas.rect 높이로 SelfHand 도킹(리사이즈 재적용). 상대 `CardSize`=풀사이즈, 딜 `oppSize`=selfSize.
- **파일**: HandView.cs, OpponentHandsView.cs, NetworkManager.cs

## 2026-08-26 15:32 (UTC+9)

- **요청**: 내 핸드가 여전히 떠 있음 — 전체 재점검.
- **원인**: TableView도 HandView라 도킹이 섞일 수 있음; pivot 중앙+컨테이너 240으로 체감 오차.
- **수정**: HandContainer만 도킹. pivot 하단, y=-cardH*(1-0.8), HLG LowerCenter, 매 프레임 재적용.
- **파일**: HandView.cs, OpponentHandsView.cs, SampleScene.unity

## 2026-08-26 15:35 (UTC+9)

- **요청**: 도킹 포인트 표식 표시, 바닥패 카드 크기=핸드와 동일.
- **수정**: SeatDebugOverlay에 핸드 하단/중심/상단 표식 + debugSeatMarkers=true. KittyView/DealAnimator 바닥패를 DisplayWidth/Height.
- **파일**: SeatDebugOverlay.cs, NetworkManager.cs, KittyView.cs, DealAnimator.cs

## 2026-08-26 15:42 (UTC+9)

- **요청**: 분홍(상단) 표식이 카드 중심에 있음.
- **원인**: HandContainer 하단 pivot + CardView가 y=0 강제 → 카드 중심이 컨테이너 하단으로 밀림. 표식은 컨테이너 모서리 기준이라 어긋남.
- **수정**: 도킹을 중앙 pivot+MiddleCenter로 복구. 표식은 실제 CardView GetWorldCorners 사용.

## 2026-08-26 15:56 (UTC+9)

- **요청**: 상대처럼 절반 걸친 뒤 카드높이×0.3 위로 올리는 단순 도킹.
- **수정**: `centerY = 0 + cardH * SelfHandLiftFromEdge(0.3)`. visibleFraction 수식 제거.

## 2026-08-26 16:01 (UTC+9)

- **요청**: 노란 마커가 카드 가야 할 위치인데 카드 하단처럼 보임.
- **원인**: SelfHandAnchor가 고정 1080 높이로 정규화되어 실제 Canvas 높이와 어긋남.
- **수정**: GetSelfHandAnchor(space)로 실제 rect 높이 사용. 노랑 "나"는 HandContainer.position(목표 중심)에 표시.

## 2026-08-26 20:48 (UTC+9)

- **요청**: 노랑·초록 불일치 — 카드 위치 스크립트 전체 스캔.
- **원인**: HandContainer HLG와 CardView.ApplyRaiseAfterLayout(y=0 강제)가 Y를 서로 덮어씀.
- **수정**: 손패 HLG 비활성 + RelayoutHandCards 수동 배치(중심 Y=0). CardView는 restY+raise만 적용. 노랑도 GetWorldCorners 중심 사용.

## 2026-08-26 20:52 (UTC+9)

- **요청**: 섞기/모으기 애니·중앙 카드 등 전부 핸드 크기로 통일.
- **수정**: DealAnimator.DeckCardSize 풀사이즈, 딜/회수 endSize 축소 제거. Kitty/TrickWin 비행도 풀사이즈 유지. NetworkManager 딜 endSize=selfSize.
