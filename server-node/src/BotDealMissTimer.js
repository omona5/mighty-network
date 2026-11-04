// Independent of bidding turn; server arms this only after deal animations finish.
class BotDealMissTimer {
  constructor(rooms, declare, { schedule = setTimeout, cancel = clearTimeout } = {}) {
    this.rooms = rooms;
    this.declare = declare;
    this.schedule = schedule;
    this.cancel = cancel;
    this.pending = new WeakMap();
  }

  eligible(room) {
    return room.players.find(p => this.rooms.isBotControlled(p)
      && this.rooms.canDeclareDealMiss(p.hand, this.rooms.hasActedInBidding(room, p.clientId)));
  }

  arm(room) {
    if (!room || room.tutorial || room.status !== 'bidding') return false;
    const previous = this.pending.get(room);
    if (previous && previous.dealId === room.dealId && this.eligible(room)) return true;
    if (previous) { this.cancel(previous.timer); this.pending.delete(room); }
    if (!this.eligible(room)) return false;
    const ticket = { dealId: room.dealId };
    this.pending.set(room, ticket);
    ticket.timer = this.schedule(() => {
      if (this.pending.get(room) !== ticket) return;
      this.pending.delete(room);
      if (this.rooms.getRoom(room.roomId) !== room || room.status !== 'bidding'
          || room.dealId !== ticket.dealId || room.tutorial) return;
      const player = this.eligible(room);
      if (player) this.declare(room, player);
    }, 3000);
    ticket.timer.unref?.();
    return true;
  }
}
module.exports = BotDealMissTimer;
