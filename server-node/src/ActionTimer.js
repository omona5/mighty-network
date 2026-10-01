// One deadline per authoritative decision. Repeated broadcasts/reconnects do not
// extend a human turn; stale callbacks cannot act on another turn or another deal.
class ActionTimer {
  constructor(getRoom, { humanDelay = 30000, schedule = setTimeout, cancel = clearTimeout } = {}) {
    this.getRoom = getRoom;
    this.humanDelay = humanDelay;
    this.schedule = schedule;
    this.cancel = cancel;
    this.pending = new WeakMap();
  }

  key(room, player) {
    return JSON.stringify([room.status, room.dealId, player.clientId,
      room.currentTurnIndex, room.currentBidderIndex,
      room.minBid, room.highestBid, room.passedClientIds, room.trickNumber,
      room.trickHistory?.length, room.tableCards?.map(t => t.card.id),
      room.players.map(p => p.hand?.length)]);
  }

  arm(room, player, botDelay, action) {
    if (!room || room.tutorial || !player) return;
    const key = this.key(room, player);
    const bot = !!(player.isBot || !player.connected);
    const previous = this.pending.get(room);
    if (previous?.key === key && previous.bot === bot) return;
    if (previous) this.cancel(previous.timer);
    const ticket = { key, bot };
    this.pending.set(room, ticket);
    ticket.timer = this.schedule(() => {
      if (this.pending.get(room) !== ticket) return;
      this.pending.delete(room);
      if (this.getRoom(room.roomId) !== room || this.key(room, player) !== key) return;
      action();
    }, bot ? botDelay : this.humanDelay);
    ticket.timer.unref?.();
  }
}

module.exports = ActionTimer;
