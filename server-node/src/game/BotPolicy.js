// Play policies ported from the confirmed eight-bot training pool.
const Rules = require('./RuleEngine');
const { RANKS, SUITS } = require('./Card');
const NAMES = Object.freeze(['GreedyBot', 'ScoreBot', 'ConservativeBot', 'AggressiveBot',
  'PartnerBot', 'TrumpBot', 'SpecialCardBot', 'InformationBot']);
const PREFIX = { S: 'SPADE', H: 'HEART', D: 'DIAMOND', C: 'CLUB' };
const min = (cards, compare) => cards.reduce((a, b) => !a || compare(b, a) < 0 ? b : a, null);

// Deliberately never inspect another player's hand or a hidden friend identity.
function knownTeam(room, viewer, id) {
  if (!room.declarerClientId) return null;
  if (id === room.declarerClientId) return true;
  if (room.friendType === 'none') return false;
  if (room.friendType === 'player' || room.friendRevealed)
    return id === room.friendClientId;
  if (room.friendType === 'card' && viewer.hand.some(c => c.id === room.friendCardId))
    return id === viewer.clientId;
  return null;
}

function voidSuits(room, clientId) {
  const result = new Set();
  if (room.ruleConfig.mustFollowSuit === false) return result;
  for (const trick of room.trickHistory || []) {
    const table = trick.table;
    if (!table?.length) continue;
    const suit = Rules.leadSuitOf(table, room.ruleConfig);
    if (!suit) continue;
    for (const entry of table) {
      if (entry.clientId !== clientId) continue;
      // Special cards can legally ignore the lead suit, so prove no void.
      if (Rules.isJoker(entry.card, room.ruleConfig) || Rules.isMighty(entry.card, room.ruleConfig)) continue;
      if (entry.card.suit !== suit) result.add(suit);
    }
  }
  return result;
}

function pickPlay(room, player, policy = 'GreedyBot', random = Math.random) {
  if (!NAMES.includes(policy)) throw new Error(`Unknown bot policy: ${policy}`);
  const hand = player.hand || [];
  if (!hand.length) return null;
  const cfg = room.ruleConfig;
  const table = room.trickComplete ? [] : room.tableCards || [];
  let pool = hand.filter(card => Rules.canPlayCard({ playerHand: hand, card, tableCards: table, ruleConfig: cfg }));
  if (!pool.length) return null;
  // Shared priority overrides every personality, while preserving forced calls.
  if (room.trickNumber === 1 || room.trickNumber === 10) {
    const nonJokers = pool.filter(card => !Rules.isJoker(card, cfg));
    if (nonJokers.length) pool = nonJokers;
  } else if (room.trickNumber === 9) {
    const joker = pool.find(card => Rules.isJoker(card, cfg));
    if (joker) pool = [joker];
  }
  // Save mighty on leads and captures worth less than two points including itself.
  // Apply after joker rules so a forced/sole remaining candidate stays playable.
  const nonMighty = pool.filter(card => !Rules.isMighty(card, cfg));
  const mighty = pool.find(card => Rules.isMighty(card, cfg));
  if (mighty && nonMighty.length && (!table.length ||
      table.reduce((sum, t) => sum + t.card.point, 0) + mighty.point < 2)) {
    pool = nonMighty;
  }
  const special = c => Rules.isMighty(c, cfg) || Rules.isJoker(c, cfg);
  const strength = c => Rules.isMighty(c, cfg) ? 1000 : Rules.isJoker(c, cfg) ? 900
    : (c.suit === cfg.trumpSuit ? 500 : 0) + RANKS.indexOf(c.rank);
  const weak = cards => min(cards, (a, b) => strength(a) - strength(b));
  const strong = cards => min(cards, (a, b) => strength(b) - strength(a));
  const ordinary = cards => { const filtered = cards.filter(c => !special(c)); return filtered.length ? filtered : cards; };
  const remaining = Math.max(1, 11 - (room.trickNumber || 1));
  const cost = c => {
    const rank = RANKS.indexOf(c.rank);
    let value = Rules.isMighty(c, cfg) ? 80 : Rules.isJoker(c, cfg) ? 60
      : c.suit === cfg.trumpSuit ? (rank >= RANKS.indexOf('J') ? 30 : 20)
        : rank >= RANKS.indexOf('Q') ? 15 : rank >= RANKS.indexOf('10') ? 5 : 0;
    if (remaining <= 3 && special(c)) value *= 0.5;
    return value;
  };
  const cheapest = cards => min(cards, (a, b) => cost(a) - cost(b));
  const points = table.reduce((sum, t) => sum + (t.card.point ? 1 : 0), 0);
  const winner = cards => Rules.determineTrickWinner({ tableCards: cards, ruleConfig: cfg, trickNumber: room.trickNumber });
  const win = cards => weak(cards.filter(card => winner([...table, { clientId: player.clientId, card }]) === player.clientId));
  const feed = cards => { const pts = cards.filter(c => c.point); return pts.length ? pts[Math.floor(random() * pts.length)] : weak(cards); };
  let card;
  if (!table.length) {
    const trumps = pool.filter(c => c.suit === cfg.trumpSuit);
    switch (policy) {
      case 'ScoreBot': {
        const count = c => hand.filter(x => x.suit === c.suit && x.point).length;
        card = min(ordinary(pool), (a, b) => count(b) - count(a) || strength(b) - strength(a)); break;
      }
      case 'ConservativeBot': {
        const count = c => c.suit === 'JOKER' ? 99 : hand.filter(x => x.suit === c.suit).length;
        card = min(pool, (a, b) => cost(a) - cost(b) || count(a) - count(b)); break;
      }
      case 'AggressiveBot': card = strong(trumps.length ? trumps : ordinary(pool)); break;
      case 'PartnerBot': {
        const suit = room.friendType === 'card' ? PREFIX[room.friendCardId?.[0]] : null;
        card = weak(pool.filter(c => c.suit === suit)) || weak(pool); break;
      }
      case 'TrumpBot': card = trumps.length >= 3 ? strong(trumps)
        : weak(pool.filter(c => c.suit !== cfg.trumpSuit && !Rules.isJoker(c, cfg))) || weak(pool); break;
      case 'InformationBot': {
        const counts = Object.fromEntries(SUITS.map(s => [s, 0]));
        for (const other of room.players) if (other.clientId !== player.clientId)
          for (const suit of voidSuits(room, other.clientId)) counts[suit]++;
        card = min(pool.filter(c => !Rules.isJoker(c, cfg)), (a, b) => counts[a.suit] - counts[b.suit] || strength(b) - strength(a)) || weak(pool); break;
      }
      default: card = strong(ordinary(pool));
    }
  } else {
    const winnerId = winner(table);
    const own = knownTeam(room, player, player.clientId);
    const other = knownTeam(room, player, winnerId);
    const ours = winnerId === player.clientId ? true : own == null || other == null ? null : own === other;
    switch (policy) {
      case 'ConservativeBot': card = ours === true ? cheapest(pool.filter(c => c.point)) || cheapest(pool)
        : (points >= 2 && win(pool)) || cheapest(pool); break;
      case 'TrumpBot': card = (ours !== true && points >= 2 && win(pool)) || weak(pool); break;
      case 'PartnerBot': card = ours === true ? feed(pool) : (ours === false && points >= 1 && win(pool)) || weak(pool); break;
      case 'SpecialCardBot': {
        const safe = ordinary(pool);
        const mw = win(pool);
        card = ours === true ? feed(safe) : !mw ? weak(safe)
          : special(mw) && points < 3 && remaining > 3 ? win(safe) || weak(safe) : mw; break;
      }
      default: {
        const threshold = policy === 'ScoreBot' ? 2 : policy === 'InformationBot' ? 1 : 0;
        card = ours === true ? feed(pool) : (points >= threshold && win(pool)) || weak(pool);
      }
    }
  }
  const action = { cardId: card.id };
  if (!table.length && Rules.isJoker(card, cfg)) action.declaredSuit = cfg.trumpSuit || 'SPADE';
  if (!table.length && Rules.isJokerCall(card, cfg)) {
    const fid = room.friendCardId;
    const prefix = Object.keys(PREFIX).find(p => PREFIX[p] === cfg.trumpSuit);
    const strongFriend = room.friendType === 'card' && (fid === cfg.mightyCardId || fid === 'JOKER'
      || ['A', 'K', 'Q', 'J'].some(r => fid === `${prefix}_${r}`));
    action.activateJokerCall = !room.jokerPlayed && !!strongFriend && knownTeam(room, player, player.clientId) !== true;
  }
  return action;
}

module.exports = { NAMES, pickPlay, voidSuits, knownTeam };
