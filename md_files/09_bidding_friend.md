# 09. 입찰, 주공, 프렌드 선택

## 목표

마이티의 본게임 시작 전 단계인 입찰, 주공 결정, 프렌드 선택 기능을 구현한다.

## 구현 범위

- 게임 상태 세분화
- 입찰 단계
- 주공 결정
- 으뜸패 선택
- 목표 점수 설정
- 프렌드 카드 선택
- 프렌드 비공개 상태 유지

## 게임 상태 제안

```text
waiting
ready
bidding
choosing_friend
playing
finished
```

## 입찰 데이터 구조 예시

```javascript
{
  playerId: "...",
  targetScore: 14,
  trumpSuit: "HEART",
  noTrump: false,
  pass: false
}
```

## 이벤트 약속

### Unity → Server

```text
bid
choose_friend
pass_bid
```

### Server → Unity

```text
game_state
bid_result
friend_chosen
error_message
```

## Cursor 서버 프롬프트

```text
마이티 서버에 입찰, 주공 결정, 프렌드 선택 단계를 추가해줘.

요구사항:
1. 게임 시작 후 바로 playing으로 가지 말고 bidding 상태로 진입한다.
2. 각 플레이어는 bid 또는 pass_bid를 보낼 수 있다.
3. bid에는 targetScore와 trumpSuit가 포함된다.
4. 가장 높은 targetScore를 제시한 플레이어가 주공이 된다.
5. 모든 플레이어가 입찰/패스하면 choosing_friend 상태로 넘어간다.
6. 주공만 choose_friend 이벤트를 보낼 수 있다.
7. friendCardId를 서버에 저장한다.
8. 프렌드가 누구인지는 해당 카드가 나오기 전까지 공개하지 않는다.
9. choosing_friend 완료 후 playing 상태로 넘어간다.
10. game_state에는 주공, 목표 점수, 으뜸패를 공개한다.
```

## Cursor Unity 프롬프트

```text
Unity에 입찰 UI와 프렌드 선택 UI를 추가해줘.

요구사항:
1. bidding 상태일 때 입찰 UI를 보여준다.
2. targetScore 입력 또는 선택 UI를 만든다.
3. trumpSuit 선택 버튼을 만든다.
4. bid 버튼과 pass 버튼을 만든다.
5. choosing_friend 상태일 때 주공에게만 프렌드 선택 UI를 보여준다.
6. 프렌드 카드 선택 후 choose_friend 이벤트를 보낸다.
7. 현재 주공, 목표 점수, 으뜸패를 화면에 표시한다.
```

## 완료 기준

- 입찰 단계가 진행된다.
- 주공이 결정된다.
- 주공만 프렌드를 선택할 수 있다.
- 프렌드 선택 후 본게임으로 진입한다.

## 다음 단계

점수 계산과 승패 판정을 구현한다.
