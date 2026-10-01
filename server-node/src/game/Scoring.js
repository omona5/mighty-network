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

// One public team identity for scoring and client role badges.
function resolvedFriendId(room) {
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
  if (friendId === room.declarerClientId) friendId = null;
  return (room.players || []).some(p => p.clientId === friendId) ? friendId : null;
}

/** Calculate the game_finished payload, including each player's scoring role. */
function calculateResult(room) {
  const players = room.players || [];
  const declarerId = room.declarerClientId;
  const targetScore = room.targetScore || 13;
  const friendId = resolvedFriendId(room);

  const playerScores = players.map((p) => ({
    clientId: p.clientId,
    nickname: p.nickname,
    isBot: !!p.isBot,
    role: p.clientId === declarerId ? "declarer" : p.clientId === friendId ? "friend" : "defender",
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
    friendClientId: friendId,
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
 * 진행 중에는 버린패 점수를 주공에게만 공개한다. 종료 시 모두에게 합산 공개.
 * viewerClientId 생략은 관전자와 동일하며 비공개 정보를 포함하지 않는다.
 */
function liveTeamScores(room, viewerClientId) {
  if (!room || !room.declarerClientId) {
    return { declarerTeamScore: 0, defenderTeamScore: 0, pointsNeeded: null };
  }
  const result = calculateResult(room);
  const finished = room.status === "finished";
  const canSeeKitty = finished || viewerClientId === room.declarerClientId;
  const kittyScore = finished ? result.kittyScore : scoreOfCards(room.discardedKitty || []);
  const declarerTeamScore = result.declarerTeamScore - result.kittyScore
    + (canSeeKitty ? kittyScore : 0);
  const pointsNeeded = Math.max(0, result.targetScore - declarerTeamScore);
  return {
    declarerTeamScore,
    defenderTeamScore: result.defenderTeamScore,
    ...(canSeeKitty ? { kittyScore } : {}),
    pointsNeeded,
    targetScore: result.targetScore,
  };
}

module.exports = {
  resolvedFriendId,
  scoreOfCards,
  calculateResult,
  applySessionScores,
  liveTeamScores,
};
