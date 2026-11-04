// One deadline per authoritative decision. Repeated broadcasts/reconnects do not
// extend a human turn; stale callbacks cannot act on another turn or another deal.
class ActionTimer {
  constructor(getRoom, { humanDelay = 30000, bidDelay = 45000, onArmed = () => {}, schedule = setTimeout, cancel = clearTimeout } = {}) {
    this.getRoom = getRoom;
    this.humanDelay = humanDelay;
    this.bidDelay = bidDelay;
    this.onArmed = onArmed;
    this.schedule = schedule;
    this.cancel = cancel;
    this.pending = new WeakMap();
  }

  snapshot(room) {
    const ticket = this.pending.get(room);
    const valid = ticket && !room.tutorial && !ticket.bot
      && this.key(room, ticket.player) === ticket.key;
    return {
      roomId: room.roomId,
      serverTime: Date.now(),
      actionDeadline: valid ? ticket.deadline : 0,
      actionClientId: valid ? ticket.player.clientId : null,
    };
  }

  key(room, player) {
    return JSON.stringify([room.status, room.dealId, player.clientId,
      room.currentTurnIndex, room.currentBidderIndex,
      room.minBid, room.highestBid, room.passedClientIds, room.trickNumber,
      room.trickHistory?.length, room.tableCards?.map(t => t.card.id),
      room.status === 'playing' ? room.playIntroUntil : room.status === 'exchanging_kitty' ? room.electionUntil : null,
      room.players.map(p => p.hand?.length)]);
  }

  arm(room, player, botDelay, action) {
    if (!room || room.tutorial || !player) return;
    const key = this.key(room, player);
    const bot = !!(player.isBot || !player.connected);
    const previous = this.pending.get(room);
    if (previous?.key === key && previous.bot === bot) return;
    if (previous) this.cancel(previous.timer);
    const ticket = { key, bot, player };
    this.pending.set(room, ticket);
    // Start the decision clock only after the announcement, for humans and bots.
    const notBefore = room.status === 'playing' ? room.playIntroUntil
      : room.status === 'exchanging_kitty' ? room.electionUntil : 0;
    const introDelay = Math.max(0, (notBefore || 0) - Date.now());
    const delay = introDelay + (bot ? botDelay : room.status === 'bidding' ? this.bidDelay : this.humanDelay);
    ticket.deadline = Date.now() + delay;
    ticket.timer = this.schedule(() => {
      if (this.pending.get(room) !== ticket) return;
      this.pending.delete(room);
      if (this.getRoom(room.roomId) !== room || this.key(room, player) !== key) return;
      action();
    }, delay);
    ticket.timer.unref?.();
    this.onArmed(room, this.snapshot(room));
  }
}

module.exports = ActionTimer;
