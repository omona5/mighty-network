// 마이티 점수 계산 / 승패 판정 (스텝10 + 런/백런)
//
// 점수 카드: A, K, Q, J, 10 = 각 1점 (총 20점)
// 주공팀 점수 >= targetScore 이면 주공팀 승리
//
// 런:   주공팀이 20점 전부 → 배수 ×2
// 백런: 주공팀이 10점 이하 → 배수 ×2
// (노기루/노프렌드로 이긴 경우도 각각 ×2, 중첩 가능)

function scoreOfCards(cards) {
  if (!cards || cards.length === 0) return 0;
  return cards.reduce((sum, c) => sum + (c && c.point ? c.point : 0), 0);
}

/**
 * 한 판 결과 계산.
 * @param {object} room
 * @returns {object} game_finished 페이로드
 */
function calculateResult(room) {
  const players = room.players || [];
  const declarerId = room.declarerClientId;
  const targetScore = room.targetScore || 13;

  // 프렌드 확정: 공개됐으면 사용. 카드 프렌드가 끝까지 안 나왔으면 주공 단독.
  let friendId = null;
  if (room.friendType === "player" && room.friendClientId) {
    friendId = room.friendClientId;
  } else if (room.friendType === "card" && room.friendRevealed && room.friendClientId) {
    friendId = room.friendClientId;
  } else if (room.friendType === "none") {
    friendId = null;
  }
  // 주공 자신 = 프렌드면 단독으로 취급
  if (friendId === declarerId) friendId = null;

  const playerScores = players.map((p) => ({
    clientId: p.clientId,
    nickname: p.nickname,
    isBot: !!p.isBot,
    score: scoreOfCards(p.wonCards),
    trickCount: p.wonCards ? Math.floor(p.wonCards.length / players.length) : 0,
  }));

  const kittyCards = room.discardedKitty != null ? room.discardedKitty : (room.kitty || []);
  const kittyScore = scoreOfCards(kittyCards);
  const isDeclarerTeam = (clientId) =>
    clientId === declarerId || (friendId && clientId === friendId);

  let declarerTeamScore = 0;
  let defenderTeamScore = 0;
  const declarerTeam = [];
  const defenderTeam = [];

  for (const ps of playerScores) {
    if (isDeclarerTeam(ps.clientId)) {
      declarerTeamScore += ps.score;
      declarerTeam.push(ps);
    } else {
      defenderTeamScore += ps.score;
      defenderTeam.push(ps);
    }
  }
  // 바닥패(버린 카드) 점수 → 주공팀
  declarerTeamScore += kittyScore;

  const declarerWins = declarerTeamScore >= targetScore;
  const declarer = players.find((p) => p.clientId === declarerId);
  const friend = friendId ? players.find((p) => p.clientId === friendId) : null;

  // ---- 런 / 백런 / 배수 ----
  const isRun = declarerTeamScore >= 20; // 점수카드 전부
  const isBackrun = declarerTeamScore <= 10; // 주공팀 10점 이하
  const noFriend = room.friendType === "none";
  const noTrump = !!room.noTrump;

  const multipliers = [];
  let multiplier = 1;
  if (isRun) {
    multipliers.push("런");
    multiplier *= 2;
  }
  if (isBackrun) {
    multipliers.push("백런");
    multiplier *= 2;
  }
  // 승리 시에만 노기루/노프렌드 배수 적용 (패배 시에도 2배인 로컬룰도 있으나 일단 승리에만)
  if (declarerWins && noTrump) {
    multipliers.push("노기루");
    multiplier *= 2;
  }
  if (declarerWins && noFriend) {
    multipliers.push("노프렌드");
    multiplier *= 2;
  }

  // 정산용 기본 단위 (칩 시스템 전 UI 표시용)
  // 승: (주공팀점수 - 10), 패: (공약 - 주공팀점수), 최소 1
  const stakeBase = declarerWins
    ? Math.max(1, declarerTeamScore - 10)
    : Math.max(1, targetScore - declarerTeamScore);
  const stakeTotal = stakeBase * multiplier;

  // 개인 정산 델타 (영합). 주공 2배, 프렌드 1배, 야당 각 1배.
  // 노프렌드면 야당 4명이 각 1배, 주공이 4배.
  const unit = stakeTotal;
  const deltas = {};
  players.forEach((p) => { deltas[p.clientId] = 0; });
  const defenders = players.filter((p) => !isDeclarerTeam(p.clientId));
  const sign = declarerWins ? 1 : -1;

  if (friendId) {
    deltas[declarerId] = sign * 2 * unit;
    deltas[friendId] = sign * unit;
    defenders.forEach((p) => { deltas[p.clientId] = -sign * unit; });
  } else {
    deltas[declarerId] = sign * defenders.length * unit;
    defenders.forEach((p) => { deltas[p.clientId] = -sign * unit; });
  }

  return {
    winner: declarerWins ? "declarer" : "defender",
    winnerLabel: declarerWins ? "주공팀" : "수비팀",
    targetScore,
    declarerTeamScore,
    defenderTeamScore,
    kittyScore,
    declarerNickname: declarer ? declarer.nickname : null,
    friendNickname: friend ? friend.nickname : null,
    friendType: room.friendType || null,
    friendCardId: room.friendType === "card" ? room.friendCardId : null,
    friendRevealed: !!room.friendRevealed,
    trumpSuit: room.declaredTrump != null ? room.declaredTrump : (room.ruleConfig && room.ruleConfig.trumpSuit),
    noTrump,
    isRun,
    isBackrun,
    multiplier,
    multipliers,
    stakeBase,
    stakeTotal,
    deltas, // { clientId: delta }
    declarerTeam,
    defenderTeam,
    players: playerScores,
  };
}

/**
 * 결과에 따라 각 플레이어 sessionScore에 델타를 누적하고,
 * result에 scoreboard(누적 현황)를 붙인다.
 */
function applySessionScores(room, result) {
  const board = [];
  for (const p of room.players || []) {
    if (p.sessionScore == null) p.sessionScore = 0;
    const delta = (result.deltas && result.deltas[p.clientId]) || 0;
    p.sessionScore += delta;
    board.push({
      clientId: p.clientId,
      nickname: p.nickname,
      isBot: !!p.isBot,
      delta,
      sessionScore: p.sessionScore,
    });
  }
  board.sort((a, b) => b.sessionScore - a.sessionScore);
  result.scoreboard = board;
  return result;
}

/**
 * 진행 중 점수(공개용). 프렌드 미공개면 주공 단독으로 계산.
 */
function liveTeamScores(room) {
  if (!room || !room.declarerClientId) {
    return { declarerTeamScore: 0, defenderTeamScore: 0, kittyScore: 0, pointsNeeded: null };
  }
  const result = calculateResult(room);
  const pointsNeeded = Math.max(0, result.targetScore - result.declarerTeamScore);
  return {
    declarerTeamScore: result.declarerTeamScore,
    defenderTeamScore: result.defenderTeamScore,
    kittyScore: result.kittyScore,
    pointsNeeded,
    targetScore: result.targetScore,
  };
}

module.exports = {
  scoreOfCards,
  calculateResult,
  applySessionScores,
  liveTeamScores,
};
