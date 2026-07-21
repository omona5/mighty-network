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
