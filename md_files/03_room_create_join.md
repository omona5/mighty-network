# 03. 방 생성 및 입장 기능

## 목표

플레이어가 닉네임을 입력하고 방을 생성하거나 기존 방에 입장할 수 있게 한다.

## 구현 범위

- 서버 RoomManager 생성
- 방 ID 생성
- 방 생성 이벤트
- 방 입장 이벤트
- 플레이어 목록 관리
- 방 안 사람들에게 플레이어 목록 브로드캐스트
- Unity 방 생성/입장 UI

## 서버 데이터 구조 초안

```javascript
rooms = {
  "ABCD": {
    roomId: "ABCD",
    players: [
      {
        socketId: "...",
        nickname: "동건",
        isReady: false
      }
    ],
    status: "waiting"
  }
}
```

## 이벤트 약속

### Unity → Server

```text
create_room
join_room
```

### Server → Unity

```text
room_created
room_joined
game_state
error_message
```

## 이벤트 데이터 예시

### create_room

```json
{
  "nickname": "동건"
}
```

### room_created

```json
{
  "roomId": "ABCD"
}
```

### join_room

```json
{
  "roomId": "ABCD",
  "nickname": "친구1"
}
```

### game_state

```json
{
  "roomId": "ABCD",
  "status": "waiting",
  "players": [
    {
      "nickname": "동건",
      "isReady": false
    },
    {
      "nickname": "친구1",
      "isReady": false
    }
  ]
}
```

## Cursor 서버 프롬프트

```text
기존 Node.js socket.io 서버에 방 생성/입장 기능을 추가해줘.

요구사항:
1. RoomManager 클래스를 만들어 rooms를 메모리에서 관리한다.
2. create_room 이벤트를 받으면 4자리 대문자/숫자 roomId를 생성한다.
3. 방 생성자는 자동으로 해당 방에 입장한다.
4. join_room 이벤트를 받으면 roomId가 존재하는지 확인한다.
5. 방 최대 인원은 5명이다.
6. 입장 성공 시 socket.join(roomId)를 호출한다.
7. 방 상태는 game_state 이벤트로 방 안 사람들에게 전송한다.
8. 에러는 error_message 이벤트로 해당 클라이언트에게만 보낸다.
9. 서버 코드를 server.js, src/RoomManager.js로 분리해줘.
```

## Cursor Unity 프롬프트

```text
Unity에 방 생성/입장 UI와 NetworkManager 기능을 추가해줘.

요구사항:
1. 닉네임 입력 InputField를 만든다.
2. 방 ID 입력 InputField를 만든다.
3. 방 생성 버튼은 create_room 이벤트를 보낸다.
4. 방 입장 버튼은 join_room 이벤트를 보낸다.
5. room_created를 받으면 생성된 방 ID를 화면에 표시한다.
6. game_state를 받으면 플레이어 목록 UI를 갱신한다.
7. error_message를 받으면 화면에 에러 메시지를 표시한다.
```

## 완료 기준

- 한 Unity 클라이언트가 방을 만들 수 있다.
- 다른 Unity 클라이언트가 같은 방 ID로 입장할 수 있다.
- 방 안 플레이어 목록이 모든 클라이언트에서 동일하게 보인다.
- 방 최대 인원 5명 제한이 작동한다.

## 다음 단계

준비 버튼과 5인 게임 시작 조건을 구현한다.
