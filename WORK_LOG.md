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
