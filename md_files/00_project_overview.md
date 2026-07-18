# Mighty WebGL 개인서버 프로젝트 개요

## 목표

친구들과만 플레이할 수 있는 마이티 카드게임을 Unity WebGL + Node.js + socket.io 기반으로 개발한다.

최종 형태는 다음과 같다.

```text
친구 PC/모바일 브라우저
  ↓
Unity WebGL 클라이언트
  ↓ WebSocket / socket.io
Node.js 게임 서버
  ↓
Raspberry Pi 배포
```

## 핵심 방향

처음부터 로컬 싱글게임을 완성한 뒤 서버로 포팅하지 않는다.

대신 다음 원칙으로 개발한다.

```text
게임의 진짜 상태는 서버가 가진다.
Unity는 서버 상태를 화면에 보여주고 사용자 입력만 서버로 보낸다.
```

## 역할 분리

### Unity WebGL 클라이언트

- 로그인/닉네임 입력 UI
- 방 생성/입장 UI
- 카드 표시
- 카드 선택/제출 버튼
- 현재 턴 표시
- 서버에서 받은 game_state 렌더링
- 모바일/PC 반응형 UI

### Node.js 서버

- 접속 관리
- 방 생성/입장/퇴장
- 플레이어 준비 상태 관리
- 카드 셔플/배분
- 각 플레이어 손패 관리
- 턴 진행
- 카드 제출 검증
- 트릭 승자 판정
- 입찰/주공/프렌드/조커/마이티 룰
- 점수 계산
- 재접속 처리

## 추천 기술 스택

```text
Client: Unity WebGL, C# (NativeWebSocket)
Server: Node.js, 순수 WebSocket(ws)
Deploy: Raspberry Pi, Nginx, PM2
External Access: Cloudflare Tunnel 또는 Tailscale
Optional DB: SQLite
```

> [계획 수정 2026-07-18] 통신은 socket.io 대신 **순수 WebSocket(ws)** 사용.
> 이유: Unity 에디터 Play 모드와 WebGL 빌드 양쪽에서 동일 동작 → 개발/테스트가 빠름.
> 메시지는 `{ "type": ..., "data": ... }` JSON 규칙을 따른다. (socket.io의 이벤트 이름 = type)

## 전체 개발 단계

1. Node.js + WebSocket 서버 초기화
2. Unity와 서버 연결 테스트
3. 방 생성/입장/플레이어 목록 (+ reconnectToken 발급 조기 도입)
4. 준비 상태 및 5인 게임 시작 (+ Heartbeat 연결 감지 보강)
5. 서버 카드 덱/셔플/배분
6. Unity 손패 표시
7. 턴 기반 카드 제출
8. 트릭 승자 판정
9. 마이티 특수 룰 추가
10. 점수 계산 및 게임 종료
11. 재접속/방 비밀번호/기본 보안 (자동 재접속 + 상태 전체 재전송)
12. Unity WebGL 빌드 및 라즈베리파이 배포

> [계획 수정 2026-07-18] 재접속 관련 4기능 배치 조정:
> - 세션 토큰(reconnectToken) 발급 → 3단계로 앞당김 (나중에 끼워넣기 어려우므로)
> - Heartbeat(연결 살아있는지 확인) → 3~4단계에 보강 (WebSocket 전환으로 필요, 좀비 연결 감지)
> - 자동 재접속 + 게임 상태 전체 재전송 → 11단계 유지 (게임 로직 완성 후 붙이는 게 맞음)

## 개발 원칙

- 단계별로 작동하는 상태를 만든다.
- 각 단계마다 Cursor에게 작은 기능 단위로 요청한다.
- 서버와 클라이언트는 이벤트 이름과 데이터 구조를 명확히 맞춘다.
- Unity가 자체적으로 게임 상태를 바꾸지 않도록 한다.
- 서버가 내려준 game_state를 기준으로 화면을 갱신한다.

## 폴더 구조 제안

```text
mighty-project/
├─ client-unity/
│  └─ Unity project
│
└─ server-node/
   ├─ package.json
   ├─ server.js
   ├─ src/
   │  ├─ rooms/
   │  ├─ game/
   │  ├─ socket/
   │  └─ utils/
   └─ README.md
```

## 첫 번째 목표

처음 목표는 마이티 완성이 아니다.

첫 목표는 다음이다.

```text
Unity 버튼 클릭
→ 서버로 ping 이벤트 전송
→ 서버가 pong 이벤트 응답
→ Unity 화면에 응답 표시
```

이게 성공하면 온라인 게임의 가장 중요한 연결 뼈대가 잡힌다.
