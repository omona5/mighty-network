const assert = require('node:assert/strict');
const { acceptEmote } = require('../src/Emotes');
const ws = { clientId: 'one' };
const player = { clientId: 'one', ws };
const room = { status: 'playing', players: [player] };
for (const index of [-1, 8, 0.5, '1', null, undefined, {}, NaN])
  assert.equal(acceptEmote(room, ws, index, 0), null);
for (const status of ['waiting', 'bidding', 'exchanging_kitty', 'choosing_friend', 'finished'])
  assert.equal(acceptEmote({ ...room, status }, ws, 0, 0), null);
assert.equal(acceptEmote(null, ws, 0, 0), null);
assert.equal(acceptEmote(room, { clientId: 'one' }, 0, 0), null);
assert.deepEqual(acceptEmote(room, ws, 0, 0), { clientId: 'one', index: 0 });
assert.equal(acceptEmote(room, ws, 1, 999), null);
assert.deepEqual(acceptEmote(room, ws, 7, 1000), { clientId: 'one', index: 7 });
const reconnect = { clientId: 'one' };
player.ws = reconnect;
assert.equal(acceptEmote(room, reconnect, 2, 1500), null);
assert.deepEqual(acceptEmote(room, reconnect, 2, 2000), { clientId: 'one', index: 2 });
const second = { clientId: 'two' };
room.players.push({ clientId: 'two', ws: second });
assert.deepEqual(acceptEmote(room, second, 3, 2000), { clientId: 'two', index: 3 });
assert.equal(acceptEmote({ ...room, players: [] }, reconnect, 0, 4000), null);
console.log('emotes: phase, identity, range, per-player cooldown and reconnect tests passed');
