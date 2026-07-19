// 스텝9 자동 검증: 입찰 → (주공) 프렌드 지정 → 본게임 진행
const WebSocket = require("ws");
const ws = new WebSocket("ws://localhost:3000");
let started = false, myId = null, myHand = [];
let didBid = false, didFriend = false, pending = false;
let sawFive = false, trickSeen = 0, bidShown = false;

ws.on("open", () => {
  ws.send(JSON.stringify({ type: "create_room", data: { nickname: "테스터", password: "" } }));
});

function legalCard(state) {
  const table = state.tableCards || [];
  let leadSuit = null;
  if (table.length > 0 && table.length < 5) {
    const lead = table[0].card;
    if (lead.suit !== "JOKER") leadSuit = lead.suit;
  }
  if (leadSuit) {
    const follow = myHand.find((c) => c.suit === leadSuit);
    if (follow) return follow;
  }
  return myHand[0];
}

ws.on("message", (raw) => {
  const msg = JSON.parse(raw.toString());
  const d = msg.data;
  if (msg.type === "welcome") myId = d.clientId;
  else if (msg.type === "room_created") ws.send(JSON.stringify({ type: "ready", data: {} }));
  else if (msg.type === "your_hand") { myHand = d.cards; pending = false; didBid = false; /* 재배분 시 재입찰 허용 */ }
  else if (msg.type === "bid_result") console.log(`입찰마감: 주공=${d.declarerNickname} 공약=${d.targetScore} 기루다=${d.noTrump ? "노기루" : d.trumpSuit}`);
  else if (msg.type === "game_state") {
    const s = d;
    if (s.canStart && !started) { started = true; ws.send(JSON.stringify({ type: "start_game", data: {} })); return; }

    if (s.status === "bidding") {
      if (s.currentBidderClientId === myId && !didBid) {
        didBid = true;
        console.log("내 입찰: 14 SPADE (봇이 더 높이 부르면 봇이 주공)");
        ws.send(JSON.stringify({ type: "bid", data: { targetScore: 14, trumpSuit: "SPADE", noTrump: false } }));
      }
      return;
    }

    if (s.status === "choosing_friend") {
      if (s.declarerClientId === myId && !didFriend) {
        didFriend = true;
        // 마이티를 프렌드로 지정
        console.log("프렌드 지정:", s.mightyCardId);
        ws.send(JSON.stringify({ type: "choose_friend", data: { friendCardId: s.mightyCardId } }));
      }
      return;
    }

    if (s.status === "playing") {
      if (!bidShown) { bidShown = true; console.log(`본게임: 주공=${s.declarerNickname} 기루다=${s.trumpSuit} 마이티=${s.mightyCardId} 프렌드지정=${s.friendChosen}`); }
      if ((s.tableCards || []).length === 5) {
        if (!sawFive) {
          sawFive = true; trickSeen++;
          console.log(`트릭#${trickSeen} 승자=${s.lastTrickWinnerNickname}` + (s.friendRevealed ? ` (프렌드공개=${s.friendNickname})` : ""));
        }
      } else sawFive = false;
      if (s.currentTurnClientId === myId && !pending && myHand.length > 0) {
        const card = legalCard(s);
        pending = true;
        ws.send(JSON.stringify({ type: "play_card", data: { cardId: card.id } }));
      }
    }
  } else if (msg.type === "error_message") { console.log("에러:", d.message); pending = false; }
});

ws.on("error", (e) => { console.log("연결 오류:", e.message); process.exit(1); });
setTimeout(() => { console.log(`--- 종료: 트릭 ${trickSeen}개 ---`); ws.close(); process.exit(0); }, 45000);
