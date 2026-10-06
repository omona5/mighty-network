# 마이티 딥러닝 봇 개발 지시서

## 0. 개발 목적

현재 구현되어 있는 5인 마이티 게임을 기반으로, **사람 플레이 데이터 없이 Python self-play를 통해 학습하는 딥러닝 봇**을 개발한다.

학습은 Python 환경에서 대량으로 수행하고, 최종 학습된 추론 모델만 실제 마이티 게임 서버에서 사용한다.

최종 구조는 다음을 목표로 한다.

```text
현재 Node.js 마이티 서버
        │
        │ 규칙 기준
        ▼
Python MightyEnv
        │
        │ self-play
        ▼
RL Training
        │
        ▼
Policy Model
        │
        │ ONNX 등 추론용 모델로 export
        ▼
실제 게임 서버에서 Bot inference
```

## 1. 반드시 참조할 기준 문서

프로젝트 내 다음 문서를 **봇 학습 환경의 최상위 게임 명세**로 사용한다.

```text
17_bot_learning_game_spec.md
```

이 문서에 정의된 다음 내용을 임의로 변경하지 않는다.

- 카드 및 카드 인덱스
- 입찰 규칙
- 딜미스
- 바닥패 교환
- 프렌드 선택
- 조커 / 마이티 / 조커콜
- 리드 및 따라내기
- 트릭 승자 판정
- 점수 계산
- 정산 방식
- observation에서 공개 가능한 정보
- actor에게 공개하면 안 되는 정보
- action mask
- terminal reward
- 학습 curriculum
- 테스트 invariant

게임 규칙에 애매한 부분이 있으면 임의로 일반적인 마이티 규칙을 적용하지 말고 다음 실제 서버 구현을 확인한다.

```text
server-node/src/RoomManager.js
server-node/src/game/RuleEngine.js
server-node/src/game/Scoring.js
server-node/src/game/Card.js
server-node/src/game/Deck.js
```

우선순위는 다음과 같다.

```text
현재 서버 실제 동작
    >
17_bot_learning_game_spec.md
    >
일반적으로 알려진 마이티 규칙
```

서버와 명세가 다르면 반드시 사용자에게 알려야 하며, 임의로 어느 한쪽을 수정하지 않는다.

## 2. 가장 중요한 개발 원칙

### 2.1 서버 게임 로직과 학습 로직을 분리한다

Node.js 서버를 직접 반복 호출하여 학습하지 않는다.

학습용으로 별도의 순수 Python 환경을 구현한다.

예:

```python
env = MightyEnv(seed=1234)

obs, infos = env.reset(seed=1234)

while not terminated:
    action = policy(obs[current_agent], infos[current_agent]["action_mask"])
    obs, rewards, terminated, truncated, infos = env.step(action)
```

네트워크, socket, UI, 인간 플레이 타이머 등의 요소는 학습 환경에서 제거한다.

### 2.2 Python 환경은 서버 규칙을 정확히 재현해야 한다

학습 속도를 위해 Python으로 재구현할 수 있지만, Python 구현은 현재 서버의 RuleEngine / Scoring 결과와 동일해야 한다.

다음 항목에 대해 deterministic regression test를 작성한다.

```text
deal
bidding
deal miss
kitty exchange
friend selection
legal card
joker lead
joker call
trick winner
team score
final settlement
```

동일한:

```text
seed
초기 카드
행동 sequence
```

를 입력했을 때 Node.js 서버와 Python 환경의 결과가 일치해야 한다.

## 3. 프로젝트 구조

가능하면 기존 게임 서버와 분리된 다음 형태로 구성한다.

```text
bot-training/
│
├─ mighty_env/
│   ├─ __init__.py
│   ├─ env.py
│   ├─ cards.py
│   ├─ deck.py
│   ├─ rules.py
│   ├─ scoring.py
│   ├─ observation.py
│   ├─ action_space.py
│   └─ constants.py
│
├─ agents/
│   ├─ random_agent.py
│   ├─ heuristic_agent.py
│   └─ neural_agent.py
│
├─ models/
│   ├─ policy_network.py
│   ├─ critic_network.py
│   └─ encoders.py
│
├─ training/
│   ├─ train_play.py
│   ├─ train_full_game.py
│   ├─ self_play.py
│   ├─ rollout.py
│   └─ league.py
│
├─ evaluation/
│   ├─ evaluate.py
│   ├─ baselines.py
│   └─ tournament.py
│
├─ export/
│   └─ export_onnx.py
│
├─ tests/
│   ├─ test_rules.py
│   ├─ test_scoring.py
│   ├─ test_action_mask.py
│   ├─ test_observation.py
│   └─ test_invariants.py
│
└─ configs/
    ├─ env.yaml
    ├─ model.yaml
    └─ training.yaml
```

현재 repository 구조와 충돌한다면 기존 구조에 맞춰 조정할 수 있다.

단, 환경 / 모델 / 학습 / 평가 / export 코드는 논리적으로 분리한다.

## 4. MightyEnv 구현

### 4.1 목표 인터페이스

가능하면 Gymnasium 또는 유사한 구조를 사용한다.

다만 마이티는 turn-based multi-agent 환경이므로 Gymnasium API를 억지로 따를 필요는 없다.

다음 기능은 반드시 제공한다.

```python
env.reset(seed=...)
env.step(action)
env.current_agent
env.get_observation(agent)
env.get_action_mask(agent)
env.get_global_state()
env.clone()
```

`clone()`은 추후 rollout / search 용으로 빠르게 현재 게임 상태를 복제할 수 있도록 설계한다.

## 5. Observation 설계

`17_bot_learning_game_spec.md`의 공개/비공개 정보 구분을 반드시 따른다.

### 5.1 Actor가 사용할 수 있는 정보

Actor에는 해당 플레이어가 실제 게임에서 알 수 있는 정보만 제공한다.

예:

```text
자기 손패
현재 phase
현재/과거 입찰 정보
주공
target score
trump
Mighty
Joker Call card
friend 지정 방식
friend 공개 여부
현재 trick
이전 trick history
각 플레이어 남은 카드 수
각 플레이어 획득 trick 수
공개된 카드
joker 사용 여부
```

상대 좌석은 acting player 자신을 항상 0번으로 두는 방식으로 canonicalization한다.

예:

```text
acting player = 실제 seat 3

policy 입력:
self = 0
실제 seat 4 = 1
실제 seat 0 = 2
실제 seat 1 = 3
실제 seat 2 = 4
```

모든 플레이어가 동일한 shared policy를 사용할 수 있도록 observation을 구성한다.

## 6. 절대 Actor에 제공하면 안 되는 정보

다음 정보는 training 중에도 actor input으로 들어가면 안 된다.

```text
상대방의 실제 손패
비공개 kitty
비공개 friend의 실제 seat
아직 공개되지 않은 역할
미래 카드
deck shuffle 순서
다른 player만 알고 있는 private information
```

이 정보는 필요하면 다음 용도로만 사용한다.

```text
centralized critic
debug
environment validation
training analysis
```

Actor observation과 global state는 코드에서도 명확하게 분리한다.

예:

```python
actor_obs = env.get_observation(agent)
global_state = env.get_global_state()
```

두 데이터를 동일 dictionary에 섞지 않는 것을 권장한다.

## 7. Bidding history 추가

단순히 현재 최고 공약만 저장하지 말고 전체 공개 bidding history를 observation에 포함할 수 있도록 한다.

예:

```python
[
    {
        "seat": 0,
        "action": "PASS",
    },
    {
        "seat": 1,
        "action": "BID",
        "score": 13,
        "suit": "HEART",
    },
    {
        "seat": 2,
        "action": "BID",
        "score": 14,
        "suit": "HEART",
    }
]
```

입찰 행동 자체가 해당 플레이어의 손패에 대한 정보이기 때문이다.

단, 모델 입력에서는 고정 길이 encoding 또는 sequence encoding으로 변환한다.

## 8. Action Space

phase별 action head를 분리한다.

하나의 거대한 action space로 모든 행동을 표현하지 않아도 된다.

예:

```text
Bid Head

PASS
DEAL_MISS
11~20 × suit
11~20 × no-trump


Kitty Head

53 card logits
3회 sequential selection


Friend Head

53 card friend
4 relative player friend
NONE


Play Head

53 card logits


Joker Lead Suit Head

SPADE
HEART
DIAMOND
CLUB


Joker Call Head

ON
OFF
```

현재 phase에서 사용하지 않는 head는 무시한다.

## 9. Action Mask

action mask는 매우 중요하다.

**신경망이 게임 규칙을 이용하여 합법 행동을 직접 판단하도록 만들지 않는다.**

합법 행동은 환경이 계산한다.

예:

```python
logits = model(obs)
logits[action_mask == 0] = -inf
action = Categorical(logits=logits).sample()
```

카드 플레이는 반드시:

```text
자기 손패
AND
RuleEngine에 따른 합법 카드
```

조건을 만족하는 카드만 선택할 수 있어야 한다.

불법 행동에 penalty를 주어 학습시키는 방식보다 애초에 mask를 적용하는 방식을 사용한다.

## 10. Policy parameter sharing

5명의 플레이어에 대해 5개의 서로 다른 actor network를 만들지 않는다.

기본 구조는 하나의 shared policy network를 사용한다.

```text
             Shared Policy
                  │
     ┌────────────┼────────────┐
     P0           P1          ...
```

모든 seat에서 동일한 parameter를 공유한다.

position 정보는 observation의 상대좌석 encoding으로 표현한다.

## 11. 최초 Network 구조

초기 버전에서는 지나치게 복잡한 Transformer를 사용하지 않는다.

먼저 MLP 기반 policy/value model을 만든다.

초기 후보:

```text
Observation
    │
    ▼
Encoder
    │
   512
    │
   512
    │
   256
    │
    ├─ Bid policy head
    ├─ Kitty policy head
    ├─ Friend policy head
    ├─ Card policy head
    ├─ Joker suit head
    ├─ Joker call head
    │
    └─ Value head
```

hidden dimension과 layer 수는 config에서 변경 가능하게 한다.

예:

```yaml
model:
  hidden_dims:
    - 512
    - 512
    - 256
```

처음부터 architecture를 과도하게 크게 만들지 않는다.

목표는 우선:

```text
환경 정확성
→ 학습 가능 여부
→ 실제 전략 향상
```

을 확인하는 것이다.

## 12. History 처리

게임이 최대 10 trick이므로 첫 버전에서는 공개된 전체 history를 observation에 명시적으로 넣는다.

따라서 첫 버전에는 반드시 LSTM/GRU/Transformer memory가 필요한 것은 아니다.

먼저 fixed-size encoding + MLP를 사용한다.

성능이 부족한 경우 이후에:

```text
GRU
LSTM
Transformer
card/token encoder
```

를 비교한다.

## 13. Critic

초기 RL 알고리즘은 다음 구조를 목표로 한다.

```text
Shared Actor
+
Centralized Critic
```

Actor:

```text
실제 플레이어가 알고 있는 정보만 사용
```

Critic:

```text
훈련 중에만 전체 state 사용 가능
```

Centralized critic에는 필요하면 다음 정보가 포함될 수 있다.

```text
모든 player hand
kitty
실제 friend identity
role/team
전체 game state
```

단 이 정보가 actor network로 전달되어서는 안 된다.

## 14. RL 알고리즘

첫 번째 구현 후보는:

```text
PPO / MAPPO 계열
```

로 한다.

특히 다음 조건을 지원해야 한다.

```text
multi-agent
shared actor
centralized critic
partial observation
action masking
self-play
```

직접 PPO를 완전히 새로 구현하기 전에 현재 사용할 library가 이 조건을 제대로 지원하는지 검토한다.

Library 때문에 게임 구조를 왜곡하지 않는다.

## 15. Reward

초기 reward는 반드시 **최종 정산 delta 기반 terminal reward**로 시작한다.

예:

```python
reward[i] = final_delta[i] / reward_scale
```

중간 trick을 먹었다고 단순히 +1 reward를 주는 등의 arbitrary reward shaping은 초기 버전에는 사용하지 않는다.

그 이유는:

```text
공약 성공
백런
노프렌드
노기루
점수 카드 전략
프렌드 전략
```

등 실제 최종 목표와 충돌할 수 있기 때문이다.

Reward scaling은 config로 조절한다.

## 16. Training curriculum

처음부터 전체 마이티를 end-to-end로 학습시키지 않는다.

다음 순서로 진행한다.

### Phase 0. Environment validation

학습을 하지 않는다.

Random action + action mask로 수십만 game을 실행하여 환경 invariant가 깨지지 않는지 검증한다.

### Phase 1. Trick play only

다음 요소를 고정한다.

```text
계약
trump
declarer
friend/team
```

10 trick 플레이만 self-play한다.

목표:

```text
카드를 어떻게 내야 하는가
```

를 먼저 학습한다.

### Phase 2. Friend mechanics 추가

card friend와 hidden team structure를 포함한다.

프렌드 공개 전/후 observation이 정확하게 처리되는지 검증한다.

### Phase 3. Kitty exchange 추가

주공의 13장 → 3장 discard를 학습한다.

3장을 동시에 선택하는 복잡한 action보다 처음에는:

```text
1장 선택
→ mask
→ 1장 선택
→ mask
→ 1장 선택
```

방식을 사용한다.

### Phase 4. Bidding 추가

입찰과 deal miss까지 포함한 전체 게임 self-play를 수행한다.

### Phase 5. Full game training

```text
Deal
→ Bidding
→ Kitty
→ Friend
→ 10 Tricks
→ Settlement
```

전체 게임을 하나의 episode로 학습한다.

## 17. Self-play opponent 관리

항상 최신 policy끼리만 플레이시키지 않는다.

다음 opponent pool을 유지한다.

```text
Random bot
현재 heuristic bot
과거 policy checkpoint
최근 policy checkpoint
현재 latest policy
```

과거 policy와도 일정 확률로 대전시켜 전략 망각이나 특정 상대에 대한 과적합을 줄인다.

## 18. 평가 시스템

Training loss만으로 성능을 판단하지 않는다.

별도의 tournament evaluation을 구현한다.

Baseline:

```text
Random legal bot
현재 서버 heuristic bot
이전 RL checkpoint
latest RL policy
```

최소 다음 metric을 기록한다.

```text
전체 평균 reward
평균 settlement delta
주공일 때 평균 delta
수비일 때 평균 delta
주공 승률
수비 승률
계약 성공률
target score별 성공률
trump별 성공률
no-trump 성공률
no-friend 성공률
friend type별 성능
평균 공약
```

가능하면 최소 수천~수만 판 단위로 평가한다.

## 19. 동일 deal 기반 paired evaluation

카드 운 때문에 policy 성능 비교가 왜곡되지 않도록 동일 seed를 이용한 비교 기능을 구현한다.

가능하면 seat rotation을 수행한다.

```text
A가 모든 seat를 경험
B가 모든 seat를 경험
```

동일한 deal condition에서 여러 policy를 비교할 수 있도록 evaluation runner를 구현한다.

## 20. Model checkpoint

training checkpoint에는 최소 다음 정보를 저장한다.

```text
model weights
optimizer state
training step
games played
config
rules version
git commit
evaluation score
```

게임 규칙이 변경된 모델을 같은 league에서 무분별하게 섞지 않는다.

## 21. Episode log

`17_bot_learning_game_spec.md`의 episode log 형태를 따른다.

최소 다음 정보를 기록한다.

```text
episode_id
seed
step
acting_seat
phase
observation
action_mask
action
reward
terminated
public_event
```

debug global state는 actor observation과 분리한다.

## 22. 성능 최적화

환경 정확성 검증이 완료되기 전에는 성능 최적화를 우선하지 않는다.

정확성이 확보된 뒤 다음 순서로 최적화한다.

```text
1. Python object allocation 감소
2. card state numpy array화
3. vectorized rollout
4. multiprocessing
5. 여러 environment 병렬 실행
6. batched neural inference
7. GPU training
```

## 23. 추후 Search 확장

초기 버전에는 MCTS를 넣지 않는다.

마이티는 상대 패가 보이지 않는 불완전정보 게임이므로 완전정보 게임용 AlphaZero 방식의 MCTS를 그대로 적용하지 않는다.

추후 필요하면 다음을 검토한다.

```text
Information Set MCTS
belief-state sampling
determinization + rollout
policy/value guided rollout
```

search가 hidden hand를 직접 참조하여 cheating하는 구조가 되면 안 된다.

## 24. ONNX export

학습 완료 후 실제 게임 서버에서 전체 training framework를 실행할 필요가 없도록 추론 모델만 export한다.

목표:

```text
PyTorch training model
        ↓
ONNX
        ↓
server inference
```

Export 시 입력과 출력 schema를 명확히 정의한다.

## 25. 실제 게임 서버 integration

서버 integration 전에는 반드시 Python 단독 평가를 완료한다.

실제 서버에서 봇 행동 요청 시 개념적으로 다음 과정만 수행한다.

```text
Server GameState
      ↓
Bot Observation Builder
      ↓
Inference Model
      ↓
Policy logits
      ↓
Legal action mask
      ↓
Action selection
      ↓
기존 RoomManager / RuleEngine
```

AI가 게임 상태 자체를 변경하게 하지 않는다.

최종 행동만 기존 서버의 정상 action API로 전달한다.

게임 규칙의 최종 권위는 계속 서버가 가진다.

## 26. 테스트

다음 테스트를 반드시 작성한다.

### Rule tests

```text
Mighty 판정
Joker 판정
Joker Call
follow suit
trump
first trick joker
last trick joker
trick winner
```

### State tests

```text
53장 중복 없음
50장 hand + 3 kitty
kitty exchange 후 각 player 10장
10 trick 완료
모든 카드 소진
```

### Score tests

```text
점수 카드 총 20점
공약 성공/실패
run
back-run
no-trump multiplier
no-friend multiplier
zero-sum settlement
```

### Observation tests

특히 정보 누설을 테스트한다.

```text
상대 hand가 actor observation에 없는가
kitty가 수비 observation에 없는가
friend 공개 전에 friend seat가 없는가
```

### Action mask tests

모든 random state에 대해 mask가 legal action과 정확히 일치해야 한다.

## 27. Random simulation 검증

신경망 학습 전에 random legal agent 5명으로 최소 대량 episode simulation을 실행한다.

검증 항목:

```text
exception 없음
illegal action 없음
deadlock 없음
무한 episode 없음
카드 중복 없음
score invariant 유지
settlement zero-sum
```

이 검증을 통과하지 못하면 RL training을 시작하지 않는다.

## 28. 개발 단계

다음 순서로 개발한다.

```text
[1] 17_bot_learning_game_spec.md 검토
[2] 기존 Node.js RuleEngine / Scoring 확인
[3] Python MightyEnv 구현
[4] Python unit test 구현
[5] Node ↔ Python rule regression test
[6] RandomAgent 구현
[7] 대량 random simulation
[8] Observation encoder 구현
[9] Shared Policy network 구현
[10] PPO/MAPPO training pipeline 구현
[11] Trick-play curriculum
[12] Full-game self-play
[13] League / checkpoint self-play
[14] Evaluation tournament
[15] ONNX export
[16] 실제 게임 서버 inference integration
```

앞 단계가 정상 동작하지 않는 상태에서 뒤 단계를 억지로 진행하지 않는다.

## 29. 구현 중 판단 기준

구현 선택지가 여러 개라면 다음 순서로 우선한다.

```text
1. 현재 서버 게임 규칙과 정확히 일치하는가
2. information leakage가 없는가
3. deterministic test가 가능한가
4. 학습이 안정적인가
5. 구현이 단순한가
6. 계산 속도가 빠른가
```

속도를 위해 규칙 정확성을 희생하지 않는다.

## 30. Cursor 작업 방식

한 번에 전체 RL 시스템을 만들려고 하지 않는다.

현재 repository를 먼저 분석하고 단계별로 구현한다.

각 단계에서:

```text
1. 기존 관련 코드 확인
2. 구현 계획 제시
3. 코드 작성
4. 테스트 작성
5. 테스트 실행
6. 오류 수정
7. 다음 단계 진행
```

순서를 따른다.

## 31. 첫 번째 개발 목표

### Milestone 1

```text
Python MightyEnv
+
RandomLegalAgent
+
게임 1판 완전 진행
+
Rule/Scoring unit test
```

### Milestone 2

```text
Random agent 5명
100,000판 이상 자동 simulation
게임 invariant 위반 0
illegal action 0
```

### Milestone 3

```text
play phase만 사용하는
shared policy PPO/MAPPO prototype
```

이 세 단계가 안정적으로 동작한 후 전체 게임 학습으로 확장한다.

## 32. 첫 작업

먼저 다음을 수행한다.

1. `17_bot_learning_game_spec.md` 전체를 읽는다.
2. 현재 `server-node`의 관련 게임 소스를 확인한다.
3. 명세와 실제 코드가 서로 다른 부분을 찾는다.
4. 현재 repository 구조를 분석한다.
5. `bot-training`을 어디에 구성할지 제안한다.
6. Python MightyEnv 구현 계획을 작성한다.
7. 아직 RL 모델 구현은 시작하지 않는다.
8. 먼저 environment와 rule regression test를 완성한다.

명세와 실제 구현 사이에 충돌이 발견되면 임의로 결정하지 말고 차이를 명확하게 보고한다.

이후 Python MightyEnv의 최소 동작 버전부터 구현을 시작한다.
