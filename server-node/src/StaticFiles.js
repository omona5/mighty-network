const fs = require('node:fs');
const path = require('node:path');

const mime = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg',
  '.ico': 'image/x-icon', '.svg': 'image/svg+xml', '.wasm': 'application/wasm',
};

// Only content-addressed names are safe to keep without revalidation.
const immutableName = /^[a-f0-9]{32}\.(?:data|wasm|framework\.js|loader\.js|bundle)(?:\.(?:unityweb|br|gz))?$/i;

module.exports = function staticFiles(root) {
  root = path.resolve(root);
  return async (req, res) => {
    try {
      if (req.method !== 'GET' && req.method !== 'HEAD') {
        res.writeHead(405, { Allow: 'GET, HEAD', 'Cache-Control': 'no-store' });
        return res.end();
      }
      let route = decodeURIComponent((req.url || '/').split('?')[0]);
      if (route === '/') route = '/test.html';
      if (/^\/room\/[a-z0-9]{4}\/?$/i.test(route)
          || /^\/(multiplayer|singleplayer|tutorial)\/?$/.test(route)) route = '/webgl/index.html';
      let file = path.resolve(root, '.' + route);
      const relative = path.relative(root, file);
      if (relative.startsWith('..') || path.isAbsolute(relative)) {
        res.writeHead(403, { 'Cache-Control': 'no-store' });
        return res.end();
      }
      let stat = await fs.promises.stat(file);
      if (stat.isDirectory()) {
        file = path.join(file, 'index.html');
        stat = await fs.promises.stat(file);
      }
      if (!stat.isFile()) throw new Error('Not a file');
      const etag = `W/"${stat.size.toString(16)}-${stat.mtimeMs.toString(16)}-${stat.ctimeMs.toString(16)}"`;
      const headers = {
        'Cache-Control': immutableName.test(path.basename(file))
          ? 'public, max-age=31536000, immutable' : 'no-cache',
        ETag: etag,
        'Last-Modified': stat.mtime.toUTCString(),
      };
      const match = req.headers['if-none-match'];
      const modifiedSince = Date.parse(req.headers['if-modified-since']);
      if (match ? match.split(',').some(tag => tag.trim() === '*' || tag.trim().replace(/^W\//, '') === etag.replace(/^W\//, ''))
          : Number.isFinite(modifiedSince) && Math.floor(stat.mtimeMs / 1000) * 1000 <= modifiedSince) {
        res.writeHead(304, headers);
        return res.end();
      }
      let extension = path.extname(file).toLowerCase();
      if (extension === '.br' || extension === '.gz') {
        headers['Content-Encoding'] = extension === '.br' ? 'br' : 'gzip';
        extension = path.extname(file.slice(0, -extension.length)).toLowerCase();
      }
      // .unityweb is decompressed by Unity, so it must not receive Content-Encoding.
      headers['Content-Type'] = mime[extension] || 'application/octet-stream';
      headers['Content-Length'] = stat.size;
      res.writeHead(200, headers);
      if (req.method === 'HEAD') return res.end();
      fs.createReadStream(file).on('error', () => res.destroy()).pipe(res);
    } catch (error) {
      if (res.headersSent) return res.destroy();
      res.writeHead(error instanceof URIError ? 400 : 404, { 'Cache-Control': 'no-store' });
      res.end('File unavailable');
    }
  };
};
