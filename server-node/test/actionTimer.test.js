const assert = require('node:assert/strict');
const ActionTimer = require('../src/ActionTimer');
const RoomManager = require('../src/RoomManager');
const rooms = new RoomManager();
const room = rooms.createRoom(null);
const p = rooms.addPlayer(room, 'Human', { clientId: 'human' }).player;
rooms.fillWithBots(room);
room.status = 'playing';
room.currentTurnIndex = 0;
let calls = 0;
const jobs = [];
const timer = new ActionTimer(id => rooms.getRoom(id), {
  schedule: (fn, ms) => { const job = { fn, ms }; jobs.push(job); return job; },
  cancel: job => { job.cancelled = true; },
});
const arm = () => timer.arm(room, p, 700, () => calls++);
arm(); arm();
assert.equal(jobs.length, 1);
assert.equal(jobs[0].ms, 30000);
jobs[0].fn(); jobs[0].fn();
assert.equal(calls, 1);
arm(); room.currentTurnIndex = 1; jobs.at(-1).fn();
assert.equal(calls, 1, 'stale turn must not act');
arm(); const humanJob = jobs.at(-1);
p.connected = false; arm();
assert.equal(humanJob.cancelled, true);
assert.equal(jobs.at(-1).ms, 700);
const botJob = jobs.at(-1);
p.connected = true; arm();
assert.equal(botJob.cancelled, true);
botJob.fn(); assert.equal(calls, 1);
jobs.at(-1).fn(); assert.equal(calls, 2);
arm(); room.dealId = 2; jobs.at(-1).fn(); assert.equal(calls, 2);
arm(); delete rooms.rooms[room.roomId]; jobs.at(-1).fn(); assert.equal(calls, 2);
room.tutorial = {}; const count = jobs.length; arm(); assert.equal(jobs.length, count);
console.log('ActionTimer: deadlines, duplicate scheduling, stale turns, reconnect, redeal, deletion and tutorial passed.');

const intro = rooms.createRoom(null);
const actor = rooms.addPlayer(intro, 'Intro', { clientId: 'intro' }).player;
intro.status = 'playing';
const realNow = Date.now;
try {
  Date.now = () => 10000;
  intro.playIntroUntil = 14200;
  timer.arm(intro, actor, 700, () => {});
  assert.equal(jobs.at(-1).ms, 34200, 'human timeout starts after the intro');
  const human = jobs.at(-1);
  Date.now = () => 11000;
  actor.connected = false;
  timer.arm(intro, actor, 700, () => {});
  assert.equal(human.cancelled, true);
  assert.equal(jobs.at(-1).ms, 3900, 'disconnect bot retains the remaining intro wait');
  intro.status = 'exchanging_kitty';
  intro.electionUntil = 13000;
  timer.arm(intro, actor, 0, () => {});
  assert.equal(jobs.at(-1).ms, 2000);
} finally { Date.now = realNow; }
console.log('Announcement timers: human, bot replacement and election wait passed.');
