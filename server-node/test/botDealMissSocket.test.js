const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const net = require('node:net');
const { spawn } = require('node:child_process');
const WebSocket = require('ws');

(async () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'mighty-dealmiss-'));
  const preload = path.join(directory, 'deck.cjs');
  fs.writeFileSync(preload, `
    const D = require(${JSON.stringify(require.resolve('../src/game/Deck'))});
    let deals = 0;
    D.createShuffledDeal = () => {
      const deck = D.createDeck();
      const weakIds = ['S_2','S_3','S_4','S_5','H_2','H_3','H_4','D_2','D_3','C_2'];
      if (++deals === 1) {
        const weak = deck.filter(c => weakIds.includes(c.id));
        const rest = deck.filter(c => !weakIds.includes(c.id));
        return {hands: [0,1,2,3].map(i=>rest.slice(i*10,i*10+10)).concat([weak]), kitty:rest.slice(40)};
      }
      const hands = Array.from({length:5},()=>[]);
      deck.slice(0,50).forEach((c,i)=>hands[i%5].push(c));
      return {hands,kitty:deck.slice(50)};
    };
  `);
  const reservation = net.createServer();
  await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
  const port = reservation.address().port;
  await new Promise(resolve => reservation.close(resolve));
  const server = spawn(process.execPath, ['--require', preload, 'server.js'], {
    cwd: path.resolve(__dirname, '..'),
    env: {...process.env, PORT:String(port), GAME_LOG_DIR:path.join(directory,'logs')},
    stdio:['ignore','pipe','pipe'],
  });
  let socket;
  let errors='';
  server.stderr.on('data',d=>{errors+=d;});
  try {
    await new Promise((resolve,reject)=> {
      const timer=setTimeout(()=>reject(Error(errors || 'startup timeout')),10000);
      server.stdout.on('data',d=>{if(String(d).includes('WebSocket server running')) {clearTimeout(timer);resolve();}});
      server.on('error',reject);
    });
    socket=new WebSocket(`ws://127.0.0.1:${port}`);
    const messages=[];
    socket.on('message',raw=>messages.push({...JSON.parse(raw),received:Date.now()}));
    await new Promise(resolve=>socket.once('open',resolve));
    const send=(type,data={})=>socket.send(JSON.stringify({type,data}));
    async function wait(type) {
      const deadline=Date.now()+10000;
      while(Date.now()<deadline) {
        const i=messages.findIndex(m=>m.type===type);
        if(i>=0) return messages.splice(i,1)[0];
        await new Promise(resolve=>setTimeout(resolve,10));
      }
      throw Error(`missing ${type}: ${errors}`);
    }
    send('create_room',{nickname:'DealMissHost'}); await wait('room_created');
    send('ready');
    await new Promise(resolve=>setTimeout(resolve,50));
    send('start_game');
    const started=await wait('game_started');
    // More than 3s since the server dealt must not trigger a declaration during animation.
    send('deal_animation_complete', {dealId:started.data.dealId-1});
    await new Promise(resolve=>setTimeout(resolve,3300));
    assert.ok(!messages.some(m=>m.type==='deal_miss' || m.type==='redeal'));
    const completed=Date.now();
    send('deal_animation_complete', {dealId:started.data.dealId});
    await new Promise(resolve=>setTimeout(resolve,1000));
    assert.ok(!messages.some(m=>m.type==='deal_miss'));
    // Duplicate acknowledgements must not restart the three-second countdown.
    send('deal_animation_complete', {dealId:started.data.dealId});
    const missed=await wait('deal_miss');
    const elapsed=missed.received-completed;
    assert.ok(elapsed>=2900 && elapsed<3900, `3s after animation: ${elapsed}`);
    const redeal=await wait('redeal');
    assert.ok(redeal.data.dealId>started.data.dealId);
    const records=fs.readdirSync(path.join(directory,'logs'),{recursive:true})
      .filter(p=>p.endsWith('.jsonl')).flatMap(p=>fs.readFileSync(path.join(directory,'logs',p),'utf8').trim().split('\n').map(JSON.parse));
    assert.equal(records.filter(e=>e.type==='deal_miss').length,1);
    assert.ok(records.some(e=>e.type==='game_end' && e.data.reason==='redealt'));
    console.log('Bot deal miss WebSocket: waits for animation, ignores stale/duplicate acknowledgements, declares 3s later; redeal and logs passed.');
  } finally {
    socket?.terminate();
    server.kill();
    if(server.exitCode==null) await new Promise(resolve=>server.once('exit',resolve));
    fs.rmSync(directory,{recursive:true,force:true});
  }
})().catch(e=>{console.error(e);process.exitCode=1;});
