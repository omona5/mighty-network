# 04. 준비 상태 및 5인 게임 시작

## 목표

방에 들어온 플레이어가 준비 버튼을 누르고, 5명이 모두 준비되면 서버가 게임을 시작할 수 있게 한다.

## 구현 범위

- 플레이어 ready 상태 관리
- ready 이벤트 추가
- 5명 모두 ready일 때 게임 시작 가능
- 서버 room status 변경
- Unity 준비 버튼 UI
- 게임 시작 상태 표시

## 이벤트 약속

### Unity → Server

```text
ready
```

### Server → Unity

```text
game_state
game_started
error_message
```

## 상태값

```text
waiting: 방 대기 중
ready: 시작 조건 확인 중
playing: 게임 진행 중
finished: 게임 종료
```

## Cursor 서버 프롬프트

```text
RoomManager에 ready 기능과 게임 시작 조건을 추가해줘.

요구사항:
1. 클라이언트가 ready 이벤트를 보내면 해당 플레이어의 isReady를 true/false 토글한다.
2. 방 안 모든 플레이어에게 game_state를 전송한다.
3. 플레이어가 정확히 5명이고 모두 isReady=true이면 방 status를 playing으로 바꾼다.
4. status가 playing으로 바뀌면 game_started 이벤트를 방 전체에 보낸다.
5. 이미 playing 상태인 방에서는 ready 변경을 막는다.
6. 에러는 error_message로 보낸다.
```

## Cursor Unity 프롬프트

```text
Unity 클라이언트에 준비 버튼 기능을 추가해줘.

요구사항:
1. Ready 버튼을 누르면 ready 이벤트를 서버로 보낸다.
2. game_state를 받을 때 각 플레이어의 ready 상태를 표시한다.
3. game_started 이벤트를 받으면 대기 UI를 숨기고 게임 화면으로 전환한다.
4. 아직 실제 카드 UI는 없어도 되고, 게임 시작 메시지만 표시하면 된다.
```

## 완료 기준

- 플레이어가 준비/준비 취소를 할 수 있다.
- 모든 클라이언트에 준비 상태가 동일하게 표시된다.
- 5명이 모두 준비되면 서버 상태가 playing으로 바뀐다.
- Unity가 게임 화면으로 전환된다.

## 다음 단계

서버에서 카드 덱을 만들고 5명에게 나눠준다.
