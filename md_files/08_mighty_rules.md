# 08. 마이티 특수 룰 추가

## 목표

기본 트릭 시스템 위에 마이티 게임의 주요 룰을 단계적으로 추가한다.

## 구현 범위

- 으뜸패 suit
- 마이티 카드
- 조커
- 조커콜
- 기루/노기루 개념
- follow suit 제한
- 특수 카드 서열

## 주의사항

마이티는 지역/모임별 룰 차이가 크다.

현재 서버는 **표준 5마** 기준으로 다음을 반영한다.

- 조커 리드 시 `declaredSuit`로 따라낼 무늬 선언
- 조커콜 카드 리드 시 `activateJokerCall`로 선택 활성화 (활성 시 조커 강제, 마이티 예외)
- 딜미스: 마이티=0 / 10=0.5 / 조커=-1 / 그외 점수카드=1, 합계 ≤0.5이면 선언 가능 (입찰·패스 이후 불가)

아직 반영하지 않은 로컬 변형: 초구 기루다 금지, 바닥패 후 기루다 변경(+1/+2), 백런 기준 변형 등.

## 먼저 결정해야 할 룰

```text
1. 마이티 카드는 무엇인가?
   예: 스페이드 A 또는 다이아 A 등

2. 조커는 언제 가장 강한가?

3. 조커콜 카드는 무엇인가?

4. 으뜸패가 있을 때 카드 서열은 어떻게 되는가?

5. 노기루일 때 마이티/조커 처리는 어떻게 되는가?

6. 첫 턴에 조커를 낼 수 있는가?

7. 같은 무늬가 있으면 반드시 따라야 하는가?

8. 프렌드 카드를 낸 순간 프렌드가 공개되는가?
```

## 추천 RuleConfig 구조

```javascript
const ruleConfig = {
  mightyCardId: "S_A",
  jokerCardId: "JOKER",
  jokerCallCardId: "C_3",
  trumpSuit: "HEART",
  noTrump: false,
  mustFollowSuit: true,
  revealFriendWhenPlayed: true
};
```

## Cursor 서버 프롬프트

```text
마이티 게임의 카드 제출 검증과 트릭 승자 판정 로직을 RuleEngine으로 분리해줘.

요구사항:
1. src/game/RuleEngine.js를 만든다.
2. RuleConfig 객체를 받아 룰을 적용할 수 있게 한다.
3. canPlayCard({ playerHand, card, tableCards, ruleConfig }) 함수를 만든다.
4. determineTrickWinner({ tableCards, ruleConfig }) 함수를 만든다.
5. mustFollowSuit가 true이면 leadSuit 카드가 손패에 있을 때 다른 suit를 낼 수 없게 한다.
6. trumpSuit가 있으면 trumpSuit가 leadSuit보다 강하게 처리되도록 한다.
7. mightyCardId는 일반 카드보다 강하게 처리한다.
8. jokerCardId는 조커콜 상황을 고려할 수 있도록 구조만 만든다.
9. 아직 모든 세부 룰을 완벽히 구현하지 않아도 되지만, 테스트 케이스를 함께 작성해줘.
```

## 테스트 케이스 예시

```text
1. leadSuit가 HEART이고 HEART 카드가 손패에 있으면 SPADE를 낼 수 없어야 한다.
2. trumpSuit가 SPADE이면 HEART A보다 SPADE 2가 강해야 한다.
3. mightyCardId는 일반 trump보다 강해야 한다.
4. 5장의 tableCards에서 가장 강한 카드를 낸 플레이어가 승자여야 한다.
```

## 완료 기준

- 서버가 잘못된 카드 제출을 거부한다.
- 으뜸패가 트릭 승자 판정에 반영된다.
- 마이티 카드가 가장 강한 카드로 처리된다.
- RuleEngine 테스트 케이스가 통과한다.

## 다음 단계

입찰, 주공, 프렌드 선택 시스템을 구현한다.
