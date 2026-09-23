const { createDeck } = require('./game/Deck');
const { sortHand } = require('./game/Card');

// These sessions use the real rules, but never the ordinary automatic bot timers.
// Every lesson is a server-side barrier; only its matching acknowledgement opens it.
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
    this.emit(room, 'game_state', this.rooms.publicState(room));
    this.emit(room, 'your_hand', { cards: room.players[0].hand, canDealMiss: false });
  }

  sync(room) {
    this.emit(room, 'tutorial_state', room.tutorial.view);
  }

  lesson(room, step, title, body, hint = '', cardIds = []) {
    const t = room.tutorial;
    t.step = step;
    t.paused = true;
    t.view = { revision: ++t.revision, round: t.round, step, title, body, hint, cardIds, paused: true, complete: step === 'complete' };
    this.sync(room);
  }

  setup(room, round) {
    this.emit(room, 'tutorial_reset', {});
    this.rooms.returnToWaiting(room);
    room.tutorial.round = round;
    // Reserve teaching cards first, then fill every remaining slot in deck order.
    const specified = round === 1
      ? [['S_A','C_3'], ['C_A','C_K'], ['C_4','JOKER'], ['C_5','C_7'], ['C_6','C_8']]
      : [['H_A','H_K','H_Q','H_J','H_10','H_9','H_8','S_2','D_2','C_2'], ['S_A','H_2'], ['H_3'], ['H_4'], ['H_5']];
    const kittyIds = round === 1 ? ['D_2','D_3','D_4'] : ['H_7','H_6','C_A'];
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
      this.rooms.placeBid(room, room.players[1].clientId, { targetScore: 13, trumpSuit: 'HEART' });
      for (let i = 2; i < 5; i++) this.rooms.passBid(room, room.players[i].clientId);
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
    if (round === 1) this.lesson(room, 'intro', '첫 번째 연습 · 내 손패 보기',
      '화면 아래의 앞면 카드 10장이 내 손패입니다. 다른 네 사람의 손패는 뒷면으로 보입니다.\n\n이번 연습에서는 ♠A와 ♣3을 사용합니다. 설명 상자의 확인을 누르기 전에는 아무도 다음 행동을 하지 않습니다.');
    else this.lesson(room, 'bid', '2 / 2 · 이번에는 내가 주공',
      '하트가 많이 들어온 강한 패입니다. 공약은 우리 팀이 모으겠다고 약속하는 점수이고, 기루다는 다른 일반 무늬보다 강한 무늬입니다.\n\n13점과 하트를 선택한 뒤 공약을 제출하세요. 이 연습에서는 봇들이 모두 패스합니다.', '13점 · 하트 기루다로 공약을 제출하세요.');
  }

  play(room, seat, cardId, options = {}) {
    const result = this.rooms.playCard(room, room.players[seat].clientId, cardId, options);
    if (result.error) throw new Error('Tutorial script: ' + result.error + ' / ' + cardId);
    this.snapshot(room);
  }

  handle(ws, type, data = {}) {
    const room = this.rooms.getRoom(ws.roomId);
    if (!room || !room.tutorial) return false;
    const t = room.tutorial;
    if (ws.clientId !== t.owner) return true;
    if (type === 'leave_room') {
      delete this.rooms.rooms[room.roomId];
      ws.roomId = null;
      return true;
    }
    if (type === 'tutorial_restart') { this.setup(room, 1); return true; }
    if (type === 'ping_from_client') {
      this.send(ws, 'pong_from_server', { message: 'pong' });
      return true;
    }
    if (type === 'deal_animation_complete') return true;
    if (type === 'tutorial_ack') {
      if (!t.paused || data.revision !== t.revision || t.step === 'complete') return true;
      t.paused = false;
      t.view.paused = false;
      this.sync(room);
      if (t.step === 'intro') {
        this.lesson(room, 'team', '주공과 프렌드는 같은 팀',
          '이번 판은 봇이 주공이 되어 하트를 기루다로 정하고 13점을 약속한 상태입니다. 기루다는 다른 일반 무늬보다 강합니다.\n\n주공은 마이티 프렌드를 선언했습니다. 마이티인 ♠A를 가진 당신이 같은 팀입니다. 다른 사람들은 아직 프렌드가 누구인지 모릅니다.');
      } else if (t.step === 'team') {
        this.play(room, 1, 'C_A');
        this.lesson(room, 'follow', '첫 카드의 무늬를 따라냅니다',
          '주공이 ♣A를 냈습니다. 이 트릭은 클로버로 시작했습니다.\n\n클로버를 가진 사람은 클로버를 내야 합니다. 클로버가 없으면 다른 무늬도 낼 수 있습니다. 확인을 누르면 나머지 봇들이 카드를 냅니다.');
      } else if (t.step === 'follow') {
        ['C_4','C_5','C_6'].forEach((id, i) => this.play(room, i + 2, id));
        this.lesson(room, 'mighty', '마이티로 이겨 보세요',
          '첫 카드의 무늬가 클로버이므로 보통은 클로버를 따라 내야 합니다. 하지만 마이티는 무늬를 따르지 않아도 되고 언제나 가장 강합니다.\n\n손패의 ♠A를 내세요. 이번 판의 마이티는 왼쪽 판 정보에서도 확인할 수 있습니다.', '♠A를 내서 프렌드를 공개하세요.', ['S_A']);
      } else if (t.step === 'reveal') {
        this.lesson(room, 'call', '내가 선이면 조커콜!',
          '트릭을 이긴 사람이 다음 트릭의 첫 카드를 냅니다. 이번 판의 조커콜 카드는 ♣3입니다.\n\n♣3을 누르고 「조커콜 사용」을 선택하세요. 이 게임에서는 조커를 가진 상대가 마이티를 함께 가지고 있어도 반드시 조커를 내야 합니다.', '♣3 → 조커콜 사용', ['C_3']);
      } else if (t.step === 'round_end') this.setup(room, 2);
      return true;
    }
    const reject = () => { this.emit(room, 'tutorial_feedback', { message: t.view.hint || '설명을 확인한 뒤 진행해 주세요.' }); this.snapshot(room); return true; };
    if (t.paused) return reject();
    const id = t.owner;
    if (t.step === 'mighty' && type === 'play_card' && data.cardId === 'S_A') {
      this.play(room, 0, 'S_A');
      this.lesson(room, 'reveal', '프렌드 공개 · 첫 트릭 승리',
        '♠A를 내면서 당신이 프렌드라는 사실이 공개되었습니다. 마이티가 가장 강하므로 이 트릭의 카드 5장은 당신이 가져갑니다.\n\n이 중 A·K·Q·J·10만 점수가 됩니다. 당신과 주공이 얻은 점수는 같은 팀 점수로 합쳐집니다.');
    } else if (t.step === 'call' && type === 'play_card' && data.cardId === 'C_3' && data.activateJokerCall === true) {
      this.play(room, 0, 'C_3', { activateJokerCall: true });
      ['C_K','JOKER','C_7','C_8'].forEach((card, i) => this.play(room, i + 1, card));
      this.lesson(room, 'round_end', '조커를 끌어냈습니다',
        '조커콜에 끌려 나온 조커는 이 트릭에서 힘을 잃습니다. 이번에는 주공의 ♣K가 이겼습니다.\n\n보통 조커는 마이티 다음으로 강하지만, 첫·마지막 트릭과 조커콜에는 예외가 있습니다. 이제 새 패로 주공 역할을 연습합니다.');
    } else if (t.step === 'bid' && type === 'bid' && Number(data.targetScore) === 13 && data.trumpSuit === 'HEART' && !data.noTrump) {
      this.rooms.placeBid(room, id, data);
      for (let i = 1; i < 5; i++) this.rooms.passBid(room, room.players[i].clientId);
      const result = this.rooms.resolveBidding(room);
      room.status = 'exchanging_kitty';
      this.emit(room, 'bid_result', result);
      this.rooms.startKittyExchange(room);
      this.snapshot(room);
      this.lesson(room, 'discard', '바닥패 3장 교환',
        '주공은 바닥패 3장을 받아 손패가 13장이 됩니다. 여기서 3장을 버려 다시 10장을 만듭니다.\n\n이번에는 ♠2·♦2·♣2를 선택하고 「3장 버리기」를 누르세요. 다시 누르면 선택이 해제됩니다. 버린 점수 카드는 주공팀 점수에 포함됩니다.', '♠2·♦2·♣2를 선택한 뒤 버리세요.', ['S_2','D_2','C_2']);
    } else if (t.step === 'discard' && type === 'discard_kitty' && Array.isArray(data.cardIds) && data.cardIds.length === 3 && new Set(data.cardIds).size === 3 && data.cardIds.every(c => ['S_2','D_2','C_2'].includes(c))) {
      this.rooms.discardKitty(room, id, data.cardIds);
      room.status = 'choosing_friend';
      this.emit(room, 'kitty_discarded', { ok: true });
      this.snapshot(room);
      this.lesson(room, 'friend', '함께할 프렌드 선언',
        '내가 가지지 않은 마이티의 주인을 우리 팀으로 불러 봅시다. 「마이티」 버튼을 누르세요.\n\n그 사람은 ♠A를 내기 전까지 다른 사람들에게 공개되지 않습니다. 노프렌드는 혼자 도전하는 선택이고, 플레이어 프렌드는 특정 사람을 즉시 공개하며 지정하는 선택입니다.', '「마이티」를 눌러 프렌드를 선언하세요.');
    } else if (t.step === 'friend' && type === 'choose_friend' && data.friendCardId === 'S_A' && !data.friendClientId) {
      this.rooms.chooseFriend(room, id, data);
      room.status = 'playing';
      this.rooms.startPlay(room);
      this.snapshot(room);
      this.lesson(room, 'lead', '주공의 첫 카드',
        '첫 트릭은 주공이 시작합니다. 가장 높은 하트인 ♥A를 내세요. 다른 사람은 하트가 있으면 따라 내야 합니다.\n\n이번에는 모두 하트를 내므로 ♥A가 이깁니다. 다른 무늬로 시작한 트릭에서는 기루다 하트가 일반 카드보다 강합니다.', '♥A를 내서 첫 트릭을 시작하세요.', ['H_A']);
    } else if (t.step === 'lead' && type === 'play_card' && data.cardId === 'H_A') {
      this.play(room, 0, 'H_A');
      ['H_2','H_3','H_4','H_5'].forEach((card, i) => this.play(room, i + 1, card));
      this.lesson(room, 'complete', '연습 완료!',
        '마이티 프렌드 공개와 조커콜, 주공의 공약·바닥패 교환·프렌드 선언까지 직접 해 보았습니다.\n\n실전에서는 10트릭을 끝까지 진행합니다. 주공팀은 공약한 점수 이상을 얻으면 승리하고, 수비팀은 이를 막으면 승리합니다. 이제 싱글플레이에서 자유롭게 연습해 보세요.');
    } else return reject();
    return true;
  }
}

module.exports = Tutorial;
