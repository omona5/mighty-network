# 13. Cursor 작업 방식 가이드

## 기본 원칙

Cursor에는 한 번에 큰 요구사항을 던지지 않는다.

나쁜 요청:

```text
마이티 온라인 게임 전체를 만들어줘.
```

좋은 요청:

```text
현재 Node.js socket.io 서버에 join_room 이벤트를 추가해줘.
roomId가 존재하면 socket.join(roomId)를 호출하고, 방 전체에 game_state를 보내게 해줘.
```

## 추천 작업 단위

각 기능은 다음 단위로 쪼갠다.

```text
1. 서버 이벤트 하나 추가
2. Unity에서 해당 이벤트 보내기
3. 서버 응답 받기
4. UI에 표시하기
5. 에러 처리 추가
```

## Cursor에게 항상 알려줘야 할 정보

```text
- 현재 폴더 구조
- 현재 server.js 내용
- 현재 Unity NetworkManager 내용
- 추가하고 싶은 이벤트 이름
- 이벤트 데이터 형식
- 완료 기준
```

## 서버 기능 추가 프롬프트 템플릿

```text
현재 Node.js + socket.io 서버에 [기능명]을 추가하고 싶다.

현재 구조:
- server.js
- src/RoomManager.js
- src/game/Deck.js

추가할 이벤트:
- Unity → Server: [event_name]
- Server → Unity: [event_name]

요구사항:
1. ...
2. ...
3. ...

주의사항:
- game_state에는 비공개 손패를 넣지 않는다.
- 에러는 error_message로 해당 클라이언트에게만 보낸다.
- 서버가 authoritative하게 상태를 관리한다.
```

## Unity 기능 추가 프롬프트 템플릿

```text
Unity 클라이언트에 [기능명] UI와 서버 통신 코드를 추가하고 싶다.

현재 구조:
- NetworkManager.cs
- GameStateView.cs
- CardView.cs

서버 이벤트:
- 보낼 이벤트: [event_name]
- 받을 이벤트: [event_name]

요구사항:
1. ...
2. ...
3. ...

주의사항:
- Unity는 서버가 준 game_state를 기준으로 화면을 갱신한다.
- Unity가 임의로 게임 상태를 확정하지 않는다.
```

## 단계별 검증 방식

각 단계가 끝나면 반드시 다음을 확인한다.

```text
1. 서버 콘솔 로그가 정상인가?
2. Unity 콘솔 에러가 없는가?
3. 이벤트 이름이 양쪽에서 동일한가?
4. JSON 데이터 필드명이 동일한가?
5. 한 명이 아닌 여러 클라이언트에서 같은 상태가 보이는가?
```

## 디버깅 팁

문제가 생기면 먼저 이벤트 로그를 찍는다.

서버:

```javascript
console.log("play_card received", data);
```

Unity:

```csharp
Debug.Log("game_state received: " + json);
```

대부분의 초기 문제는 다음 중 하나다.

```text
- 서버가 안 켜져 있음
- Unity 서버 주소가 틀림
- 이벤트 이름 오타
- JSON 필드명 불일치
- CORS 문제
- WebGL에서 지원 안 되는 패키지 사용
```

## 최종 원칙

```text
서버는 심판이다.
Unity는 화면이다.
socket.io는 둘 사이의 무전기다.
```
