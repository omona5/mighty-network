# 07. 기본 트릭 승자 판정

## 목표

5명이 한 장씩 카드를 냈을 때 서버가 해당 트릭의 승자를 판정하고, 다음 트릭의 선 플레이어를 정한다.

## 구현 범위

- 한 트릭당 tableCards 5장 관리
- 기본 suit follow 구조 설계
- 트릭 승자 판정 함수
- 승자가 다음 턴 시작
- 획득 카드 pile 관리
- trickHistory 저장

## 단순화된 초기 룰

이 단계에서는 마이티 특수 룰을 모두 넣지 않는다.

초기 룰은 다음처럼 단순화한다.

```text
1. 첫 카드의 suit가 lead suit다.
2. 같은 suit 중 rank가 가장 높은 카드가 이긴다.
3. trump, joker, mighty는 아직 적용하지 않는다.
```

## 서버 함수 예시

```javascript
function determineTrickWinner(tableCards, leadSuit) {
  // tableCards: [{ playerId, card }]
  // return winnerPlayerId
}
```

## Cursor 서버 프롬프트

```text
서버에 기본 트릭 승자 판정 기능을 추가해줘.

요구사항:
1. tableCards가 5장이 되면 determineTrickWinner()를 호출한다.
2. leadSuit는 첫 번째로 낸 카드의 suit로 한다.
3. 같은 suit 카드 중 rank가 가장 높은 카드를 낸 플레이어를 승자로 한다.
4. 승자 플레이어의 wonCards 배열에 이번 tableCards의 카드들을 추가한다.
5. trickHistory에 이번 트릭 결과를 저장한다.
6. tableCards를 비운다.
7. currentTurnPlayerId를 승자로 설정한다.
8. 방 전체에 game_state를 전송한다.
9. 아직 trump, joker, mighty 특수 규칙은 적용하지 않는다.
```

## Cursor Unity 프롬프트

```text
Unity에서 트릭 종료 결과를 표시할 수 있게 game_state 렌더링을 수정해줘.

요구사항:
1. 현재 트릭 카드가 비워지면 화면에서도 tableCards를 초기화한다.
2. lastTrickWinnerNickname이 있으면 상태창에 표시한다.
3. 각 플레이어가 획득한 카드 수 또는 트릭 수를 표시한다.
```

## 완료 기준

- 5명이 한 장씩 내면 서버가 트릭 승자를 정한다.
- 트릭 승자가 다음 트릭의 첫 턴이 된다.
- tableCards가 초기화된다.
- Unity 화면에 마지막 트릭 승자가 표시된다.

## 다음 단계

마이티의 실제 카드 서열, 으뜸패, 마이티, 조커, 조커콜 룰을 추가한다.
