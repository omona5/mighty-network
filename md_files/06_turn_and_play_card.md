# 06. 턴 기반 카드 제출 기능

## 목표

서버가 현재 턴을 관리하고, 현재 턴인 플레이어만 카드를 낼 수 있게 한다.

## 구현 범위

- currentTurnPlayerId 관리
- Unity 카드 클릭 시 play_card 이벤트 전송
- 서버에서 턴 검증
- 서버에서 손패 보유 여부 검증
- 카드 제출 성공 시 손패에서 제거
- 테이블에 낸 카드 기록
- 방 전체에 game_state 전송
- 해당 플레이어에게 your_hand 갱신 전송

## 이벤트 약속

### Unity → Server

```text
play_card
```

### Server → Unity

```text
game_state
your_hand
error_message
```

## play_card 예시

```json
{
  "cardId": "S_A"
}
```

## 공개 game_state 예시

```json
{
  "roomId": "ABCD",
  "status": "playing",
  "currentTurnNickname": "동건",
  "tableCards": [
    {
      "playerNickname": "동건",
      "card": { "id": "S_A", "suit": "SPADE", "rank": "A" }
    }
  ],
  "players": [
    { "nickname": "동건", "handCount": 9 },
    { "nickname": "친구1", "handCount": 10 }
  ]
}
```

## Cursor 서버 프롬프트

```text
마이티 서버에 턴 기반 카드 제출 기능을 추가해줘.

요구사항:
1. 게임 시작 시 첫 번째 플레이어를 currentTurnPlayerId로 설정한다.
2. play_card 이벤트를 받으면 현재 턴 플레이어인지 확인한다.
3. 제출한 cardId가 해당 플레이어 hand에 있는지 확인한다.
4. 유효하면 hand에서 카드를 제거하고 tableCards에 추가한다.
5. 다음 플레이어로 턴을 넘긴다.
6. 방 전체에 game_state를 전송한다.
7. 카드를 낸 플레이어에게 your_hand를 다시 전송한다.
8. 턴이 아니거나 손패에 없는 카드를 내면 error_message를 보낸다.
9. 아직 마이티 룰의 카드 제출 제한은 적용하지 않는다.
```

## Cursor Unity 프롬프트

```text
Unity 손패 UI에서 카드를 클릭하면 play_card 이벤트를 서버로 보내도록 수정해줘.

요구사항:
1. CardView는 cardId를 가지고 있어야 한다.
2. 카드 클릭 시 NetworkManager.PlayCard(cardId)를 호출한다.
3. game_state를 받을 때 현재 턴 플레이어를 화면에 표시한다.
4. tableCards를 화면 중앙에 표시한다.
5. error_message를 받으면 상태창에 표시한다.
```

## 완료 기준

- 현재 턴 플레이어만 카드를 낼 수 있다.
- 카드를 내면 모든 클라이언트의 테이블에 표시된다.
- 낸 사람의 손패에서 카드가 사라진다.
- 턴이 다음 플레이어로 넘어간다.

## 다음 단계

5명이 한 장씩 냈을 때 트릭 승자를 판정한다.
