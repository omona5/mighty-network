// 스텝10+바닥패: 입찰 → 바닥패 버리기 → 프렌드 → 본게임 → game_finished
const WebSocket = require("ws");
const ws = new WebSocket("ws://localhost:3000");
let started = false, myId = null, myHand = [];
let didBid = false, didFriend = false, didDiscard = false, pending = false;
let sawFive = false, trickSeen = 0, bidShown = false, finished = false;

ws.on("open", () => {
  ws.send(JSON.stringify({ type: "create_room", data: { nickname: "테스터", password: "" } }));
});

function pickDiscardIds(hand) {
  // 점수카드/조커를 피하고 약한 카드 3장
  const scored = hand.map((c) => {
    let s = 0;
    if (c.suit === "JOKER") s += 90;
    if (c.point) s += 20;
    const r = { "2": 1, "3": 2, "4": 3, "5": 4, "6": 5, "7": 6, "8": 7, "9": 8, "10": 9, J: 10, Q: 11, K: 12, A: 13, JOKER: 14 };
    s += (r[c.rank] || 0) * 0.1;
    return { id: c.id, s };
  });
  scored.sort((a, b) => a.s - b.s);
  return scored.slice(0, 3).map((x) => x.id);
}

function legalPlay(state) {
  const tableRaw = state.tableCards || [];
  const isLead = tableRaw.length === 0 || tableRaw.length >= 5;
  const table = isLead ? [] : tableRaw;
  let leadSuit = null;
  let jokerCall = false;
  if (table.length > 0) {
    const lead = table[0];
    jokerCall = !!lead.jokerCallActivated;
    if (lead.card && lead.card.suit === "JOKER") leadSuit = lead.declaredSuit || null;
    else if (lead.card) leadSuit = lead.card.suit;
  }
  if (jokerCall) {
    const joker = myHand.find((c) => c.id === "JOKER" || c.suit === "JOKER");
    if (joker) return { cardId: joker.id };
    const mighty = myHand.find((c) => c.id === state.mightyCardId);
    if (mighty) return { cardId: mighty.id };
  }
  if (leadSuit) {
    const follow = myHand.find((c) => c.suit === leadSuit);
    if (follow) return { cardId: follow.id };
  }
  const card = myHand[0];
  const opts = { cardId: card.id };
  if (isLead && card.id === "JOKER") opts.declaredSuit = "SPADE";
  if (isLead && state.jokerCallCardId && card.id === state.jokerCallCardId) {
    opts.activateJokerCall = false;
  }
  return opts;
}

ws.on("message", (raw) => {
  const msg = JSON.parse(raw.toString());
  const d = msg.data;
  if (msg.type === "welcome") myId = d.clientId;
  else if (msg.type === "room_created") ws.send(JSON.stringify({ type: "ready", data: {} }));
  else if (msg.type === "your_hand") { myHand = d.cards; pending = false; didBid = false; }
  else if (msg.type === "bid_result") console.log(`입찰마감: 주공=${d.declarerNickname} 공약=${d.targetScore}`);
  else if (msg.type === "kitty_discarded") console.log("바닥패 버리기 완료");
  else if (msg.type === "game_finished") {
    finished = true;
    console.log(`판종료: 승=${d.winnerLabel} 주공팀=${d.declarerTeamScore}/${d.targetScore} 수비=${d.defenderTeamScore} 바닥패=${d.kittyScore}`);
    setTimeout(() => { ws.close(); process.exit(0); }, 400);
  }
  else if (msg.type === "game_state") {
    const s = d;
    if (s.canStart && !started) { started = true; ws.send(JSON.stringify({ type: "start_game", data: {} })); return; }

    if (s.status === "bidding") {
      if (s.currentBidderClientId === myId && !didBid) {
        didBid = true;
        console.log("내 입찰: 14 SPADE");
        ws.send(JSON.stringify({ type: "bid", data: { targetScore: 14, trumpSuit: "SPADE", noTrump: false } }));
      }
      return;
    }

    if (s.status === "exchanging_kitty") {
      if (s.declarerClientId === myId && !didDiscard && myHand.length === 13) {
        didDiscard = true;
        const ids = pickDiscardIds(myHand);
        console.log("바닥패 버림:", ids.join(","));
        ws.send(JSON.stringify({ type: "discard_kitty", data: { cardIds: ids } }));
      }
      return;
    }

    if (s.status === "choosing_friend") {
      if (s.declarerClientId === myId && !didFriend) {
        didFriend = true;
        console.log("프렌드 지정:", s.mightyCardId);
        ws.send(JSON.stringify({ type: "choose_friend", data: { friendCardId: s.mightyCardId } }));
      }
      return;
    }

    if (s.status === "playing") {
      if (!bidShown) { bidShown = true; console.log(`본게임: 주공=${s.declarerNickname} 목표=${s.targetScore}`); }
      if ((s.tableCards || []).length === 5) {
        if (!sawFive) { sawFive = true; trickSeen++; console.log(`트릭#${trickSeen} 승자=${s.lastTrickWinnerNickname}`); }
      } else sawFive = false;
      if (s.currentTurnClientId === myId && !pending && myHand.length > 0) {
        const play = legalPlay(s);
        pending = true;
        ws.send(JSON.stringify({ type: "play_card", data: play }));
      }
    }
  } else if (msg.type === "error_message") { console.log("에러:", d.message); pending = false; didDiscard = false; }
});

ws.on("error", (e) => { console.log("연결 오류:", e.message); process.exit(1); });
setTimeout(() => {
  console.log(`--- 타임아웃: 트릭 ${trickSeen} finished=${finished} ---`);
  ws.close();
  process.exit(finished ? 0 : 1);
}, 65000);
