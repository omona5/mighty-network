// 마이티 점수 계산 / 승패 판정 (스텝10)
//
// 점수 카드: A, K, Q, J, 10 = 각 1점 (총 20점)
// 주공팀 점수 >= targetScore 이면 주공팀 승리

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
    noTrump: !!room.noTrump,
    declarerTeam,
    defenderTeam,
    players: playerScores,
  };
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
  liveTeamScores,
};
