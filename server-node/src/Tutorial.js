const { createDeck } = require('./game/Deck');
const { sortHand } = require('./game/Card');
const Rules = require('./game/RuleEngine');

// Lessons are barriers. Ordinary bot timers never run in a tutorial room.
// Text and scripted actions are separate so reconnects only replay the view.
const LESSONS = {
  intro: ["1 / 2 · 플레이 해보기","화면 아래의 앞면 카드 10장이 내 손패입니다. 다른 네 사람의 손패는 뒷면으로 보입니다.\n\n이번에는 카드를 직접 내면서 규칙을 배워봅시다. 설명을 읽고 확인을 누르면 연습이 진행됩니다."],
  follow: ["첫 카드의 무늬","이번 트릭은 선 플레이어가 ♣A로 시작했습니다. 첫 카드와 같은 무늬가 손에 있으면 일반 카드는 그 무늬를 따라 내야 합니다.\n\n바닥에 카드가 2장 이상 놓이면 현재 가장 강한 카드에 노란색 테두리가 표시됩니다.\n\n이번에는 ♣2를 내보세요.","♣2를 내서 클로버 무늬를 따라가세요.",["C_2"]],
  follow_result: ["같은 무늬에서는 A가 가장 강합니다","모두 클로버를 냈으므로 ♣A가 이겼습니다.\n\n같은 무늬에서는 A > K > Q > J > 10 > 9 > … > 2 순서로 강합니다. 트릭을 이긴 사람이 다음 트릭의 첫 카드를 냅니다."],
  trump: ["기루다 · 으뜸 무늬","기루다는 이번 판에서 가장 강한 힘을 가지는 무늬를 의미합니다. 이번 판의 기루다는 하트입니다. 기루다 정보는 상단의 판 정보에서 확인할 수 있습니다.\n\n이번 트릭은 ♦K로 시작했지만 내 손에는 다이아몬드가 없습니다. 첫 카드의 무늬가 없으면 다른 무늬를 낼 수 있습니다. ♥4를 내보세요.","다이아몬드가 없으므로 기루다 ♥4를 내보세요.",["H_4"]],
  trump_compare: ["♥4가 ♦K를 앞섰습니다","♥4는 ♦K보다 숫자는 낮지만, 하트가 기루다이므로 더 강합니다. 노란색 테두리도 ♥4로 옮겨졌습니다.\n\n아직 한 사람이 카드를 내지 않았습니다. 확인을 눌러 마지막 카드를 확인하세요."],
  trump_result: ["더 높은 기루다가 이깁니다","마지막 봇이 ♥6을 냈습니다. 같은 기루다끼리는 숫자가 높은 카드가 강하므로 ♥6이 이겼습니다.\n\n이 봇이 다음 트릭을 시작합니다."],
  special: ["마이티와 조커","마이티는 언제나 가장 강한 카드입니다. 보통 ♠A이며, 스페이드가 기루다이면 ♦A가 됩니다.\n\n조커는 보통 마이티 다음으로 강합니다. 마이티와 조커는 첫 카드의 무늬를 따르지 않아도 됩니다.\n\n이번 판은 하트 기루다이므로 ♠A가 마이티입니다."],
  mighty: ["마이티를 내보세요","봇이 클로버로 시작했습니다. 클로버가 손에 있어도 마이티는 예외로 낼 수 있습니다.\n\n♠A를 내서 가장 강한 카드의 힘을 확인해 보세요.","이번 판의 마이티 ♠A를 내보세요.",["S_A"]],
  mighty_result: ["마이티로 트릭 승리","♠A가 가장 강하므로 당신이 이 트릭을 이겼습니다."],
  reveal: ["프렌드 공개 · 마이티로 팀을 도왔습니다","마지막 봇이 ♠A, 마이티를 냈습니다! 마이티가 ♥K를 이기고 점수 카드 3장을 가져왔습니다.\n\n마이티 프렌드로 지정했던 바로 그 봇입니다. ♠A를 내면서 정체가 공개되고 보라색 프렌드 마크가 표시됩니다. 프렌드가 얻은 점수는 주공팀 점수로 합산됩니다.\n\n이처럼 프렌드는 정체를 숨긴 채 주공을 돕습니다. 이제 추가로 3트릭을 직접 판단해서 플레이해 봅시다."],
  points: ["A·K·Q·J·10은 점수 카드입니다","바닥의 카드 5장을 살펴보세요. A·K·Q·J·10은 각각 1점입니다. 따라서, 한 판의 전체 점수는 20점입니다.\n\n이번에는 ♠A·♣K·♣Q·♣J로 4점을 얻었습니다.\n\n확인을 누르면 점수 카드가 내 쪽으로 이동하고 나머지 카드는 정리됩니다."],
  call: ["내가 선이면 조커콜!","앞 트릭을 이겼으므로 이번에는 내가 먼저 냅니다. 이번 판의 조커콜 카드는 ♣3입니다.\n\n조커는 마이티 다음으로 강한 카드이지만 조커콜로 끌어내진 조커는 힘을 잃습니다. 선에서 조커콜을 사용하면 조커를 가진 상대가 반드시 조커를 내야 합니다.\n\n♣3을 선택한 뒤 「조커콜 사용」을 누르세요.","♣3 → 「조커콜 사용」",["C_3"]],
  call_result: ["조커를 끌어냈습니다","조커콜에 끌려 나온 조커는 힘을 잃었습니다. 이번 트릭은 가장 높은 클로버인 ♣10이 이겼습니다.\n\n조커는 첫 번째 트릭과 마지막 트릭에서도 힘을 잃습니다."],
  round_end: ["첫 번째 연습 정리","기본 세기: 마이티 > 조커 > 기루다 > 첫 카드의 무늬\n같은 무늬: A > K > Q > J > 10 > … > 2\n\n일반 카드는 첫 무늬를 따라 내고, 마이티와 조커는 예외입니다. 조커는 첫·마지막 트릭과 조커콜에서 힘을 잃습니다.\n\nA·K·Q·J·10을 모아 점수를 얻습니다. 이제 두 번째 연습으로 넘어갑니다."],
  bid: ["2 / 2 · 공약하고 주공 되기","게임 시작 전에는 기루다와 목표 점수를 제시해 주공 자리를 두고 경쟁합니다. 이것을 공약이라고 합니다.\n\n이번에는 하트가 많은 강한 손패입니다. 13점과 ♥하트를 선택하고 공약을 제출하세요. 봇들은 모두 패스합니다.","13점 · ♥하트 기루다로 공약을 제출하세요."],
  bid_result: ["당신이 주공이 되었습니다","다른 봇들이 모두 패스하여 당신이 주공으로 확정되었습니다.\n\n이번 판의 목표는 ♥하트를 기루다로 하고 우리 팀이 13점 이상 얻는 것입니다. 확인을 누르면 바닥패 3장을 가져옵니다."],
  discard: ["바닥패 3장 교환","바닥패 3장을 받아 손패가 13장이 되었습니다. 3장을 버려 다시 10장으로 만들어야 합니다.\n\n♠2·♦2·♣2를 선택하고 「3장 버리기」를 누르세요. 다시 누르면 선택이 해제됩니다.","♠2·♦2·♣2를 선택한 뒤 「3장 버리기」",["S_2","D_2","C_2"]],
  discard_result: ["손패를 정리했습니다","3장을 버려 손패가 다시 10장이 되었습니다. 주공은 바닥패를 이용해 자신의 패를 정리할 수 있습니다.\n\n버린 점수 카드는 주공팀 점수에 포함됩니다. 이번에 버린 세 장은 점수 카드가 아닙니다."],
  friend: ["함께할 프렌드 선언","주공은 특정 카드를 가진 사람을 프렌드로 지정할 수 있습니다.\n\n이번에는 내가 가지지 않은 마이티의 주인을 우리 팀으로 불러봅시다. 「마이티」를 누르세요.","「마이티」를 눌러 프렌드를 선언하세요."],
  friend_result: ["마이티의 주인이 같은 팀입니다","♠A를 가진 사람이 당신의 프렌드가 되었습니다. 그 사람이 ♠A를 내기 전까지는 주공도 정체를 알 수 없습니다.\n\n플레이어 지정은 특정 사람을 즉시 공개하며 프렌드로 선택합니다. 노프렌드는 혼자 주공팀으로 도전하는 선택입니다."],
  lead: ["주공의 첫 카드","첫 트릭은 주공이 시작합니다. 이번 판의 기루다는 ♥하트입니다.\n\n가장 높은 하트인 ♥A를 내서 첫 트릭을 시작해 보세요.","♥A를 내서 첫 트릭을 시작하세요.",["H_A"]],
  lead_result: ["♥A로 첫 트릭을 이겼습니다","모두 하트를 냈으므로 가장 높은 ♥A가 이겼습니다.\n\n손에는 ♥Q·♥J·♥10 등 강한 기루다가 남아 있지만 ♥K는 없습니다. 프렌드가 누구인지도 아직 모릅니다. 다음 트릭을 이어가 봅시다."],
  free: ["직접 판단해서 플레이하기","배운 규칙을 이용해 원하는 카드를 내보세요. 규칙상 가능한 카드라면 모두 선택할 수 있습니다.\n\n각 트릭이 끝나면 실제 결과를 함께 확인합니다.","낼 수 있는 카드 중 원하는 카드를 선택하세요."],
  complete: ["연습 완료!","축하합니다! 무늬 따라내기, 기루다, 마이티와 프렌드, 점수 카드, 조커콜을 직접 경험했습니다.\n\n주공의 공약·바닥패 교환·프렌드 선언과 자유 플레이 3트릭도 마쳤습니다.\n\n실전에서는 한 판에 10트릭을 진행합니다. 주공팀은 공약한 점수 이상을 얻으면 승리하고, 수비팀은 이를 막으면 승리합니다. 이제 싱글플레이에서 자유롭게 연습해 보세요."],
  roles: ["마이티에는 세 가지 역할이 있습니다","마이티에는 주공, 프렌드, 시민이라는 세 가지 역할이 있습니다.\n\n주공은 이번 게임을 이끌며, 특정 점수 이상 획득을 목표로 공약해 당선된 플레이어입니다. 강한 손패를 활용해 프렌드와 함께 목표 점수를 달성해야 승리합니다."],
  roles_friend: ["프렌드 · 숨겨진 같은 팀","프렌드는 주공이 선택한 숨겨진 주공팀의 일원입니다. 프렌드 카드(예: 마이티)를 내기 전까지는 주공을 포함한 다른 플레이어에게 정체가 공개되지 않습니다.\n\n프렌드는 자신의 역할을 알고 있으며, 시민들 사이에 숨어 주공을 도와야 합니다. 공개되면 이전에 모은 점수까지 주공팀 점수에 합산됩니다. 주공팀이 목표 점수를 달성하면 함께 승리합니다."],
  roles_citizens: ["시민 · 주공팀의 목표를 막으세요","시민은 주공과 프렌드가 아닌 나머지 세 명의 플레이어입니다.\n\n주공팀(주공 + 프렌드)이 목표 점수를 달성하지 못하도록 점수 카드를 가져와야 합니다. 화면에서는 시민 팀을 수비팀이라고도 부릅니다.\n\n이번 연습에서는 직접 주공이 되어, 숨겨진 프렌드와 함께 플레이해 봅시다."],
  rescue: ["♥Q로 다음 트릭 시작","앞 트릭을 이겼으므로 다시 내가 먼저 냅니다.\n\n♥Q를 내보세요. 다른 플레이어에게 있는 ♥K는 ♥Q보다 강합니다.","♥Q를 내서 다음 트릭을 시작하세요.",["H_Q"]],
  rescue_threat: ["시민의 ♥K가 앞서고 있습니다","시민이 ♥K를 내서 ♥Q를 앞섰습니다. 지금 트릭이 끝나면 점수를 시민 팀에게 빼앗깁니다.\n\n아직 마지막 플레이어가 카드를 내지 않았습니다. 확인을 눌러 마지막 카드를 살펴보세요."],
};

class Tutorial {
  constructor(rooms, send) { this.rooms = rooms; this.send = send; }

  start(ws) {
    const room = this.rooms.createRoom(require('crypto').randomBytes(16).toString('hex'));
    const { player } = this.rooms.addPlayer(room, '플레이어', ws);
    this.rooms.fillWithBots(room);
    ws.roomId = room.roomId;
    room.tutorial = { owner: player.clientId, revision: 0, round: 1 };
    this.send(ws, 'room_created', { roomId: room.roomId, reconnectToken: player.reconnectToken, nickname: player.nickname });
    this.setup(room, 1);
    return room;
  }

  emit(room, type, data) {
    const p = room.players.find(p => p.clientId === room.tutorial.owner);
    if (p && p.connected && p.ws) this.send(p.ws, type, data);
  }

  snapshot(room) {
    this.emit(room, 'game_state', this.rooms.publicState(room, room.tutorial.owner));
    this.emit(room, 'your_hand', { cards: room.players[0].hand, canDealMiss: false });
  }

  sync(room) { this.emit(room, 'tutorial_state', room.tutorial.view); }

  lesson(room, step, bodyOverride) {
    const [title, body, hint = '', cardIds = []] = LESSONS[step] || ['트릭 결과', bodyOverride];
    const t = room.tutorial;
    t.step = step;
    t.paused = true;
    t.view = { revision: ++t.revision, round: t.round, step, title,
      body: bodyOverride || body, hint, cardIds, paused: true, complete: step === 'complete',
      inspectTable: ['follow_result', 'trump_compare', 'trump_result', 'mighty_result', 'reveal', 'points', 'call_result', 'lead_result', 'rescue_threat', 'free_result'].includes(step) };
    this.sync(room);
  }

  setup(room, round) {
    this.emit(room, 'tutorial_reset', { round });
    this.rooms.returnToWaiting(room);
    Object.assign(room.tutorial, { round, holdTable: true, freeTricks: 0 });
    // Round one is a complete, unique 53-card deal. Seats 0 and 1 have no diamonds:
    // the learner can cut with H_4, then seat 1 overtrumps and leads the next trick.
    const specified = round === 1 ? [
      ['S_A','C_2','C_3','H_4','S_2','S_3','S_4','S_5','S_6','S_7'],
      ['C_4','H_6','C_K','C_5','S_8','S_9','S_10','S_J','S_Q','S_K'],
      ['C_A','D_K','C_7','C_6','D_7','D_8','D_9','D_10','D_J','D_Q'],
      ['C_8','D_3','C_Q','JOKER','H_2','H_3','H_5','H_7','H_8','H_9'],
      ['C_9','D_4','C_J','C_10','D_A','H_A','H_K','H_Q','H_J','H_10'],
    ] : [['H_A','H_Q','H_J','H_10','H_9','H_8','H_7','S_2','D_2','C_2'], ['H_2','H_K'], ['H_3','D_3'], ['H_4','D_4'], ['H_5','S_A']];
    const kittyIds = round === 1 ? ['D_2','D_5','D_6'] : ['H_6','C_A','C_3'];
    const deck = createDeck();
    const reserved = new Set(specified.flat().concat(kittyIds));
    const rest = deck.filter(c => !reserved.has(c.id));
    room.players.forEach((p, i) => {
      p.hand = specified[i].map(id => deck.find(c => c.id === id));
      while (p.hand.length < 10) p.hand.push(rest.shift());
      sortHand(p.hand);
    });
    room.kitty = kittyIds.map(id => deck.find(c => c.id === id));
    room.status = 'bidding';
    this.rooms.startBidding(room);
    if (round === 1) {
      this.rooms.passBid(room, room.players[0].clientId);
      this.rooms.passBid(room, room.players[1].clientId);
      this.rooms.placeBid(room, room.players[2].clientId, { targetScore: 13, trumpSuit: 'HEART' });
      for (let i = 3; i < 5; i++) this.rooms.passBid(room, room.players[i].clientId);
      this.rooms.resolveBidding(room);
      room.status = 'exchanging_kitty';
      this.rooms.startKittyExchange(room);
      this.rooms.discardKitty(room, room.declarerClientId, kittyIds);
      room.status = 'choosing_friend';
      this.rooms.chooseFriend(room, room.declarerClientId, { friendCardId: 'S_A' });
      room.status = 'playing';
      this.rooms.startPlay(room);
    }
    this.emit(room, 'game_started', { roomId: room.roomId });
    this.snapshot(room);
    this.lesson(room, round === 1 ? 'intro' : 'roles');
  }

  play(room, seat, cardId, options = {}) {
    const result = this.rooms.playCard(room, room.players[seat].clientId, cardId, options);
    if (result.error) throw new Error('Tutorial script: ' + result.error + ' / ' + cardId);
    room.tutorial.holdTable = true;
    this.snapshot(room);
  }

  // Release an inspected trick through the same animation queue as card plays.
  collect(room) {
    if (!room.trickComplete || !room.tutorial.holdTable) return;
    room.tutorial.holdTable = false;
    this.emit(room, 'game_state', { ...this.rooms.publicState(room, room.tutorial.owner), tutorialCollect: true });
  }

  acknowledge(room) {
    switch (room.tutorial.step) {
      case 'intro':
        this.play(room, 2, 'C_A'); this.play(room, 3, 'C_8'); this.play(room, 4, 'C_9');
        return this.lesson(room, 'follow');
      case 'follow_result':
        this.collect(room);
        this.play(room, 2, 'D_K'); this.play(room, 3, 'D_3'); this.play(room, 4, 'D_4');
        return this.lesson(room, 'trump');
      case 'trump_compare':
        this.play(room, 1, 'H_6'); return this.lesson(room, 'trump_result');
      case 'trump_result': return this.lesson(room, 'special');
      case 'special':
        this.collect(room);
        this.play(room, 1, 'C_K'); this.play(room, 2, 'C_7');
        this.play(room, 3, 'C_Q'); this.play(room, 4, 'C_J');
        return this.lesson(room, 'mighty');
      case 'mighty_result': return this.lesson(room, 'points');
      case 'points': this.collect(room); return this.lesson(room, 'call');
      case 'call_result': this.collect(room); return this.lesson(room, 'round_end');
      case 'round_end': return this.setup(room, 2);
      case 'roles': return this.lesson(room, 'roles_friend');
      case 'roles_friend': return this.lesson(room, 'roles_citizens');
      case 'roles_citizens': return this.lesson(room, 'bid');
      case 'bid_result':
        room.status = 'exchanging_kitty';
        this.emit(room, 'bid_result', room.tutorial.bidResult);
        this.rooms.startKittyExchange(room); this.snapshot(room);
        return this.lesson(room, 'discard');
      case 'discard_result':
        room.status = 'choosing_friend'; this.snapshot(room);
        return this.lesson(room, 'friend');
      case 'friend_result':
        room.status = 'playing'; this.rooms.startPlay(room); this.snapshot(room);
        return this.lesson(room, 'lead');
      case 'lead_result': this.collect(room); return this.lesson(room, 'rescue');
      case 'rescue_threat':
        this.play(room, 4, 'S_A'); return this.lesson(room, 'reveal');
      case 'reveal': return this.startFreeTrick(room);
      case 'free_result':
        if (room.tutorial.freeTricks < 3) return this.startFreeTrick(room);
        this.collect(room); return this.lesson(room, 'complete');
      default: return; // Action lesson: acknowledgement unlocks its input.
    }
  }

  startFreeTrick(room) {
    this.collect(room);
    // The previous winner may be a bot. It must lead before the learner acts.
    if (room.currentTurnIndex !== 0) {
      this.playBot(room);
      this.advanceBots(room);
    }
    this.lesson(room, 'free');
  }

  playBot(room) {
    const seat = room.currentTurnIndex;
    const pick = this.rooms.botPickPlay(room, room.players[seat]);
    this.play(room, seat, pick.cardId, pick);
  }

  advanceBots(room) {
    while (!room.trickComplete && room.currentTurnIndex !== 0) this.playBot(room);
  }

  freeResult(room) {
    const t = room.tutorial;
    t.freeTricks++;
    const trick = room.trickHistory.at(-1);
    const winnerCard = room.tableCards.find(entry => entry.clientId === trick.winnerClientId).card;
    const won = trick.winnerClientId === t.owner;
    const points = trick.cards.reduce((sum, card) => sum + card.point, 0);
    let reason;
    if (Rules.isMighty(winnerCard, room.ruleConfig)) reason = '마이티가 나와 다른 카드보다 강했습니다.';
    else if (Rules.isJoker(winnerCard, room.ruleConfig)) reason = '이번 트릭에서는 조커의 힘이 유효하여 조커가 이겼습니다.';
    else if (winnerCard.suit === room.ruleConfig.trumpSuit && Rules.leadSuitOf(room.tableCards, room.ruleConfig) !== winnerCard.suit)
      reason = '기루다가 첫 카드의 무늬보다 강해 이겼습니다.';
    else reason = '같은 무늬 중 가장 높은 카드가 이겼습니다.';
    this.lesson(room, 'free_result', `자유 플레이 ${t.freeTricks} / 3\n\n${won ? '당신이' : '봇이'} 이번 트릭을 이겨 점수 카드 ${points}장을 가져갔습니다.\n\n${reason}\n\n트릭을 이긴 사람이 다음 트릭을 시작합니다.`);
  }

  rejection(room, type, data) {
    const t = room.tutorial;
    if (t.paused) return '설명을 확인한 뒤 진행해 주세요.';
    if (t.step === 'follow' && type === 'play_card') {
      if (data.cardId === 'S_A') return '마이티는 무늬를 따르지 않아도 됩니다. 지금은 일반 카드의 무늬 따라내기를 연습하므로 ♣2를 내보세요.';
      if (data.cardId === 'C_3') return '♣3도 낼 수 있지만 뒤의 조커콜 연습에 남겨둡니다. 이번에는 ♣2를 내보세요.';
      return '첫 카드가 클로버입니다. 손에 클로버가 있으므로 일반 카드는 클로버를 내야 합니다. ♣2를 내보세요.';
    }
    const reasons = {
      trump: '이번에는 기루다의 힘을 확인합니다. 다이아몬드가 없으므로 ♥4를 낼 수 있습니다.',
      mighty: '이번에는 가장 강한 카드인 마이티를 연습합니다. ♠A를 내보세요.',
      call: '이번에는 조커콜을 연습합니다. ♣3을 선택하고 「조커콜 사용」을 눌러보세요.',
      bid: '하트가 많은 패로 주공이 되는 연습입니다. 13점 · 하트 기루다로 공약을 제출하세요.',
      discard: '손패를 10장으로 만들기 위해 3장을 버려야 합니다. 이번에는 ♠2·♦2·♣2를 선택하세요.',
      friend: '이번 연습에서는 마이티를 가진 사람을 프렌드로 지정합니다. 「마이티」를 선택하세요.',
      lead: '주공은 첫 트릭을 시작합니다. 이번에는 가장 높은 하트인 ♥A를 내보세요.',
      rescue: '이번에는 ♥Q를 내서 숨겨진 프렌드가 어떻게 도와주는지 살펴봅시다.',
    };
    return reasons[t.step] || '현재 안내에 따라 진행해 주세요.';
  }

  handle(ws, type, data = {}) {
    const room = this.rooms.getRoom(ws.roomId);
    if (!room || !room.tutorial) return false;
    const t = room.tutorial;
    if (ws.clientId !== t.owner) return true;
    if (type === 'leave_room') { delete this.rooms.rooms[room.roomId]; ws.roomId = null; return true; }
    if (type === 'tutorial_restart') { this.setup(room, 1); return true; }
    if (type === 'ping_from_client') { this.send(ws, 'pong_from_server', { message: 'pong' }); return true; }
    if (type === 'deal_animation_complete') return true;
    if (type === 'tutorial_card_hint') {
      this.emit(room, 'tutorial_feedback', { message: t.step === 'free'
        ? '첫 카드와 같은 무늬를 가지고 있으므로 다른 일반 무늬는 낼 수 없습니다.'
        : this.rejection(room, 'play_card', data) });
      return true;
    }
    if (type === 'tutorial_ack') {
      if (!t.paused || data.revision !== t.revision || t.step === 'complete') return true;
      t.paused = false; t.view.paused = false; this.sync(room);
      this.acknowledge(room);
      return true;
    }
    const reject = message => {
      this.emit(room, 'tutorial_feedback', { message: message || this.rejection(room, type, data) });
      this.snapshot(room); return true;
    };
    if (t.paused) return reject();
    const id = t.owner;
    if (t.step === 'follow' && type === 'play_card' && data.cardId === 'C_2') {
      this.play(room, 0, 'C_2'); this.play(room, 1, 'C_4'); this.lesson(room, 'follow_result');
    } else if (t.step === 'trump' && type === 'play_card' && data.cardId === 'H_4') {
      this.play(room, 0, 'H_4'); this.lesson(room, 'trump_compare');
    } else if (t.step === 'mighty' && type === 'play_card' && data.cardId === 'S_A') {
      this.play(room, 0, 'S_A'); this.lesson(room, 'mighty_result');
    } else if (t.step === 'call' && type === 'play_card' && data.cardId === 'C_3' && data.activateJokerCall === true) {
      this.play(room, 0, 'C_3', { activateJokerCall: true });
      ['C_5','C_6','JOKER','C_10'].forEach((card, i) => this.play(room, i + 1, card));
      this.lesson(room, 'call_result');
    } else if (t.step === 'bid' && type === 'bid' && Number(data.targetScore) === 13 && data.trumpSuit === 'HEART' && !data.noTrump) {
      const placed = this.rooms.placeBid(room, id, data);
      if (placed.error) return reject(placed.error);
      for (let i = 1; i < 5; i++) this.rooms.passBid(room, room.players[i].clientId);
      const result = this.rooms.resolveBidding(room);
      t.bidResult = result; this.snapshot(room); this.lesson(room, 'bid_result');
    } else if (t.step === 'discard' && type === 'discard_kitty' && Array.isArray(data.cardIds) && data.cardIds.length === 3 && new Set(data.cardIds).size === 3 && data.cardIds.every(c => ['S_2','D_2','C_2'].includes(c))) {
      this.rooms.discardKitty(room, id, data.cardIds);
      this.emit(room, 'kitty_discarded', { ok: true }); this.snapshot(room); this.lesson(room, 'discard_result');
    } else if (t.step === 'friend' && type === 'choose_friend' && data.friendCardId === 'S_A' && !data.friendClientId) {
      this.rooms.chooseFriend(room, id, data); this.snapshot(room); this.lesson(room, 'friend_result');
    } else if (t.step === 'lead' && type === 'play_card' && data.cardId === 'H_A') {
      this.play(room, 0, 'H_A');
      ['H_2','H_3','H_4','H_5'].forEach((card, i) => this.play(room, i + 1, card));
      this.lesson(room, 'lead_result');
    } else if (t.step === 'rescue' && type === 'play_card' && data.cardId === 'H_Q') {
      this.play(room, 0, 'H_Q'); this.play(room, 1, 'H_K');
      this.play(room, 2, 'D_3'); this.play(room, 3, 'D_4');
      this.lesson(room, 'rescue_threat');
    } else if (t.step === 'free' && type === 'play_card') {
      const result = this.rooms.playCard(room, id, data.cardId, data);
      if (result.error) {
        // Keep feedback faithful to the same rules that validated the play.
        const lead = room.trickComplete ? null : room.tableCards[0];
        if (lead && Rules.isJokerCallActivated(lead, room.ruleConfig) && room.players[0].hand.some(c => Rules.isJoker(c, room.ruleConfig)))
          return reject('조커콜이 활성화되어 조커를 내야 합니다.');
        return reject(result.error.startsWith('리드 무늬') ? '첫 카드와 같은 무늬를 가지고 있으므로 다른 일반 무늬는 낼 수 없습니다.' : result.error);
      }
      t.holdTable = true; this.snapshot(room); this.advanceBots(room); this.freeResult(room);
    } else return reject();
    return true;
  }
}

module.exports = Tutorial;
