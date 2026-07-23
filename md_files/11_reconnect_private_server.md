# 11. 재접속 및 개인서버 안정화

> [계획 수정 2026-07-18] 재접속 4기능 배치:
> - reconnectToken 발급: 3단계에서 이미 도입 (여기서는 복구에 사용).
> - Heartbeat(연결 감지): 3~4단계에서 이미 보강 (순수 WebSocket이라 직접 구현).
> - 이 단계(11)에서 구현: **자동 재접속** + **게임 상태 전체 재전송**(your_hand + game_state).
> - socket.io가 아니므로 heartbeat/재연결을 라이브러리에 의존하지 않고 우리가 직접 만든다.

## 구현 방식 (2026-07-23)

**봇 대타 soft disconnect**

1. 비정상 끊김 → 좌석·손패·누적점수 유지, `connected=false`
2. 끊긴 동안 서버가 해당 손패로 봇처럼 입찰/버리기/프렌드/카드 제출
3. `reconnect { reconnectToken }` → 기존 `clientId` 유지로 복구, `your_hand` + `game_state` 재전송
4. 의도적 `leave_room` → 즉시 퇴장(토큰 무효)
5. 유예 5분 만료: 대기중이면 자리 회수, 게임중이면 영구 봇 전환
6. 플레이어 목록에 남은 시간 카운트다운 표시 (`mm:ss`)

이미 있는 것: reconnectToken 발급, heartbeat, 방 비밀번호.

## 목표

친구끼리 하는 개인서버로서 최소한의 안정성과 접근 제한을 추가한다.

## 구현 범위

- 방 비밀번호 ✅
- 닉네임 중복 처리 ✅ (자동 번호: 동건 → 동건2)
- 접속 끊김 감지 ✅
- 일정 시간 내 재접속 허용 ✅ (봇 대타 + 5분 유예)
- 플레이어 식별용 reconnectToken ✅
- 서버 로그 ✅
- ~~관전자 차단/허용~~ → **범위 제외** (5인 고정, 관전 없음)

## 닉네임 중복 (2026-07-23)

같은 방에 동일 닉네임이 있으면 입장 거부 대신 **뒤에 숫자**를 붙인다 (`동건` → `동건2`). 봇 닉네임(`봇1` 등)과도 충돌하지 않게 검사한다. 최대 12자 제한을 넘지 않도록 본문을 잘라 붙인다.

## 왜 필요한가

모바일 WebGL에서는 사용자가 카카오톡을 보거나 브라우저가 백그라운드로 가면 연결이 끊길 수 있다.

따라서 재접속 처리가 중요하다.

## 추천 구조

```javascript
player = {
  socketId: "...",
  nickname: "동건",
  reconnectToken: "random-token",
  connected: true,
  disconnectedAt: null,
  hand: []
}
```

## 이벤트 약속

### Unity → Server

```text
reconnect
```

### Server → Unity

```text
reconnected
your_hand
game_state
error_message
```

## Cursor 서버 프롬프트

```text
마이티 개인서버에 재접속과 방 비밀번호 기능을 추가해줘.

요구사항:
1. create_room 시 선택적으로 roomPassword를 받을 수 있게 한다.
2. join_room 시 비밀번호가 있는 방이면 검증한다.
3. 플레이어 입장 시 reconnectToken을 발급한다.
4. Unity에는 reconnectToken을 전달한다.
5. disconnect 이벤트 발생 시 플레이어를 즉시 삭제하지 않고 connected=false로 표시한다.
6. 일정 시간 내 같은 reconnectToken으로 reconnect 이벤트를 보내면 기존 플레이어로 복구한다.
7. 재접속 성공 시 your_hand와 game_state를 다시 전송한다.
8. 닉네임이 겹치면 자동으로 번호를 붙인다 (동건2).
9. 서버 주요 이벤트는 console.log로 남긴다.
(관전자 기능은 구현하지 않는다.)
```

## Cursor Unity 프롬프트

```text
Unity 클라이언트에 reconnectToken 저장과 재접속 요청 기능을 추가해줘.

요구사항:
1. 서버에서 받은 reconnectToken을 PlayerPrefs에 저장한다.
2. 서버 연결이 끊겼다가 다시 연결되면 reconnect 이벤트를 보낸다.
3. reconnect 성공 시 game_state와 your_hand를 받아 화면을 복구한다.
4. 재접속 실패 시 방 입장 화면으로 보낸다.
5. 방 비밀번호 입력 UI를 추가한다.
```

## 완료 기준

- 방 비밀번호가 있는 방은 비밀번호 없이는 입장할 수 없다.
- 모바일/브라우저 새로고침 후에도 일정 시간 내 재접속 가능하다.
- 재접속 시 손패와 현재 게임 상태가 복구된다.

## 다음 단계

Unity WebGL 빌드와 라즈베리파이 배포를 진행한다.
