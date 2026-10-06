/* Shared by the Unity template and deployed pages. No extra game downloads. */
(function (root) {
  'use strict';
  const keys = ['dataUrl', 'codeUrl', 'frameworkUrl'];
  const hashed = /^[a-f0-9]{32}\.(?:data|wasm|framework\.js|loader\.js|bundle)(?:\.(?:unityweb|br|gz))?$/i;

  function cacheControl(url) {
    const name = new URL(url, document.baseURI).pathname.split('/').pop();
    if (hashed.test(name)) return 'immutable';
    return /\.(?:data|wasm|bundle)(?:\.(?:unityweb|br|gz))?$/i.test(name)
      ? 'must-revalidate' : 'no-store';
  }

  function measure(progress, sizes) {
    let loaded = 0, total = 0;
    let complete = true;
    for (const key of keys) {
      const item = progress[key];
      const size = sizes[key];
      if (!(size > 0)) return null;
      total += size;
      complete = complete && !!(item && item.finished);
      if (!item) continue;
      // Encoded responses can report decoded totals. Weight their fraction by
      // the on-disk payload size, never by the number of files.
      loaded += item.finished ? size : item.lengthComputable && item.total > 0
        ? size * Math.min(1, item.loaded / item.total) : Math.min(size, item.loaded || 0);
    }
    return { loaded, total, complete, percent: complete ? 100 : Math.min(99, Math.floor(loaded / total * 100)) };
  }

  async function resolveSizes(config) {
    let files = [];
    try {
      const response = await fetch('build-sizes.json', { cache: 'no-cache', signal: AbortSignal.timeout(5000) });
      if (response.ok) files = (await response.json()).files || [];
    } catch (_) { /* Older deployments can use HEAD instead. */ }
    const sizes = {};
    await Promise.all(keys.map(async key => {
      const target = new URL(config[key], document.baseURI).href;
      const file = files.find(entry => new URL(entry.url, document.baseURI).href === target);
      // Fixed-name builds can be replaced independently of an old manifest.
      if (file && hashed.test(new URL(target).pathname.split('/').pop()) && file.size > 0) {
        sizes[key] = file.size;
        return;
      }
      try {
        const response = await fetch(target, { method: 'HEAD', cache: 'no-cache', signal: AbortSignal.timeout(5000) });
        if (response.ok) sizes[key] = Number(response.headers.get('Content-Length'));
      } catch (_) { /* Fall back to Unity's per-file totals during download. */ }
    }));
    return sizes;
  }

  async function start(canvas, status, config, loaderUrl) {
    status.replaceChildren();
    const label = document.createElement('div');
    const bar = document.createElement('progress');
    bar.max = 100;
    bar.setAttribute('aria-label', '게임 파일 불러오기');
    status.append(label, bar);
    label.textContent = '게임 파일 크기 확인 중…';
    const sizes = await resolveSizes(config);
    const progress = {};
    config.downloadProgress = progress;
    config.cacheControl = cacheControl;
    let initializing = false;
    function render() {
      for (const key of keys) {
        const item = progress[key];
        if (!(sizes[key] > 0) && item && item.lengthComputable && item.total > 0) sizes[key] = item.total;
      }
      const value = measure(progress, sizes);
      if (value && value.complete) initializing = true;
      if (initializing) {
        label.textContent = '게임 파일 준비 완료 (100%) · 게임 시작 준비 중…';
        bar.removeAttribute('value');
      } else if (value) {
        label.textContent = `게임 파일 불러오는 중 ${value.percent}% · ${(value.loaded / 1e6).toFixed(1)} / ${(value.total / 1e6).toFixed(1)} MB`;
        bar.value = value.percent;
      } else {
        label.textContent = '게임 파일 불러오는 중…';
        bar.removeAttribute('value');
      }
    }
    render();
    const timer = setInterval(render, 100);
    try {
      await new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = loaderUrl;
        script.onload = resolve;
        script.onerror = () => reject(new Error('게임 로더를 불러오지 못했습니다. 새로고침해 주세요.'));
        document.body.appendChild(script);
      });
      await createUnityInstance(canvas, config, value => {
        if (value >= 0.9) initializing = true;
        render();
      });
      status.remove();
      canvas.focus();
    } catch (error) {
      label.textContent = '게임을 시작하지 못했습니다: ' + (error && error.message || String(error));
      bar.remove();
    } finally {
      clearInterval(timer);
    }
  }
  root.MightyLoading = { start, measure, cacheControl };
})(typeof window === 'undefined' ? globalThis : window);
