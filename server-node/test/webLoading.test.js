const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const http = require('node:http');
const vm = require('node:vm');
const staticFiles = require('../src/StaticFiles');

const source = fs.readFileSync(path.resolve(__dirname,
  '../../mighty-network-unity/Assets/WebGLTemplates/Responsive/loading.js'), 'utf8');
const context = { URL, document: { baseURI: 'https://example.com/game/' } };
vm.runInNewContext(source, context);
const { measure, cacheControl } = context.MightyLoading;
const hash = '0123456789abcdef0123456789abcdef';
const sizes = { dataUrl: 80, codeUrl: 15, frameworkUrl: 5 };
assert.equal(measure({ frameworkUrl: { finished: true } }, sizes).percent, 5);
assert.equal(measure({ dataUrl: { loaded: 40, total: 80, lengthComputable: true } }, sizes).percent, 40);
assert.equal(measure({ dataUrl: { loaded: 400, total: 800, lengthComputable: true } }, sizes).percent, 40,
  'decoded byte totals must retain compressed payload weights');
assert.equal(measure({ dataUrl: { loaded: 40 } }, sizes).percent, 40);
assert.equal(measure({}, {}), null, 'unknown size must not display a made-up percentage');
const finished = Object.fromEntries(Object.keys(sizes).map(key => [key, { finished: true }]));
assert.equal(measure(finished, sizes).percent, 100);
assert.equal(measure(finished, sizes).complete, true, 'cache hits must also finish');
assert.equal(cacheControl(`Build/${hash}.data.unityweb`), 'immutable');
assert.equal(cacheControl(`Build/${hash}.wasm.unityweb?v=1`), 'immutable');
assert.equal(cacheControl('Build/webgl.data'), 'must-revalidate');
assert.equal(cacheControl('Build/webgl.wasm'), 'must-revalidate');
assert.equal(cacheControl('StreamingAssets/aa/settings.json'), 'no-store');
for (const relative of ['../../docs/loading.js', '../public/webgl/loading.js']) {
  assert.equal(fs.readFileSync(path.resolve(__dirname, relative), 'utf8'), source,
    'deployed helper must match the template');
}

(async () => {
  // Exercise the real UI controller with the same progress callbacks as Unity.
  let stopped = false, focused = false;
  const status = { children: [], replaceChildren() { this.children = []; },
    append(...items) { this.children.push(...items); }, remove() { this.removed = true; } };
  const config = { dataUrl: `Build/${hash}.data.unityweb`, codeUrl: `Build/${hash}.wasm.unityweb`,
    frameworkUrl: `Build/${hash}.framework.js.unityweb` };
  context.document.createElement = () => ({ setAttribute() {}, removeAttribute(key) { delete this[key]; }, remove() {} });
  context.document.body = { appendChild(script) { script.onload(); } };
  context.AbortSignal = AbortSignal;
  context.fetch = async () => ({ ok: true, json: async () => ({
    files: Object.keys(sizes).map(key => ({ url: config[key], size: sizes[key] }))
  }) });
  context.setInterval = () => 1;
  context.clearInterval = () => { stopped = true; };
  context.createUnityInstance = async (_, options, onProgress) => {
    options.downloadProgress.dataUrl = { loaded: 40, total: 80, lengthComputable: true };
    onProgress(0.1); // Unity's aggregate is deliberately different from byte weighting.
    assert.equal(status.children[1].value, 40);
    assert.match(status.children[0].textContent, /40%/);
    Object.assign(options.downloadProgress, finished);
    onProgress(0.9);
    assert.match(status.children[0].textContent, /100%.*게임 시작 준비 중/);
    assert.equal(status.children[1].value, undefined, 'initialization must be indeterminate');
  };
  await context.MightyLoading.start({ focus() { focused = true; } }, status, config, 'Build/loader.js');
  assert.ok(stopped && focused && status.removed);

  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'mighty-cache-test-'));
  fs.mkdirSync(path.join(root, 'webgl', 'Build'), { recursive: true });
  fs.writeFileSync(path.join(root, 'webgl', 'index.html'), 'first build');
  fs.writeFileSync(path.join(root, 'webgl', 'Build', `${hash}.data.unityweb`), 'payload');
  fs.writeFileSync(path.join(root, 'webgl', 'Build', 'webgl.wasm'), 'wasm');
  const server = http.createServer(staticFiles(root));
  try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const base = `http://127.0.0.1:${server.address().port}`;
    const first = await fetch(base + '/room/ABCD');
    assert.equal(await first.text(), 'first build');
    assert.equal(first.headers.get('cache-control'), 'no-cache');
    const etag = first.headers.get('etag');
    const unchanged = await fetch(base + '/webgl/', { headers: { 'If-None-Match': etag } });
    assert.equal(unchanged.status, 304);
    assert.equal((await unchanged.arrayBuffer()).byteLength, 0);
    const since = await fetch(base + '/webgl/', { headers: { 'If-Modified-Since': first.headers.get('last-modified') } });
    assert.equal(since.status, 304);
    fs.writeFileSync(path.join(root, 'webgl', 'index.html'), 'updated build!');
    const changed = await fetch(base + '/webgl/', { headers: { 'If-None-Match': etag } });
    assert.equal(changed.status, 200);
    assert.equal(await changed.text(), 'updated build!');
    const head = await fetch(base + `/webgl/Build/${hash}.data.unityweb`, { method: 'HEAD' });
    assert.equal(head.headers.get('cache-control'), 'public, max-age=31536000, immutable');
    assert.equal(head.headers.get('content-length'), '7');
    assert.equal(head.headers.get('content-encoding'), null);
    assert.equal((await head.arrayBuffer()).byteLength, 0);
    const fixed = await fetch(base + '/webgl/Build/webgl.wasm');
    assert.equal(fixed.headers.get('cache-control'), 'no-cache');
    assert.equal(fixed.headers.get('content-type'), 'application/wasm');
    await fixed.arrayBuffer();
    const missing = await fetch(base + '/webgl/Build/missing.data');
    assert.equal(missing.status, 404);
    assert.equal(missing.headers.get('cache-control'), 'no-store');
    await missing.text();
  } finally {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
    fs.rmSync(root, { recursive: true, force: true });
  }
  console.log('Web loading: byte weights, cache policies, HEAD, 304, updates and deployment copies passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
