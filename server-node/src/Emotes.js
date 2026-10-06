// Cooldown belongs to the player, so reconnecting cannot bypass it.
function acceptEmote(room, ws, index, now = performance.now()) {
  if (!room || room.status !== 'playing' || !Number.isInteger(index) || index < 0 || index >= 8) return null;
  const player = room.players.find(p => p.clientId === ws.clientId && p.ws === ws && !p.isBot);
  if (!player || (player.lastEmoteAt !== undefined && now - player.lastEmoteAt < 1000)) return null;
  player.lastEmoteAt = now;
  return { clientId: player.clientId, index };
}
module.exports = { acceptEmote };
