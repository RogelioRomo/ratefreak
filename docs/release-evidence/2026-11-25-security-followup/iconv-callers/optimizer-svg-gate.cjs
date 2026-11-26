const { createRequire } = require('node:module');
const requireWeb = createRequire(process.cwd() + '/web/package.json');
const { imageOptimizerTransform } = requireWeb('next/dist/server/image-optimizer/transform');
const { detectContentType } = requireWeb('next/dist/server/image-optimizer/detect-content-type');
const Module = require('node:module');
const original = Module._load;
let sharpLoads = 0;
Module._load = function(request, ...args) {
  if (request === 'sharp') { sharpLoads++; throw Error('Unexpected Sharp import'); }
  return original.call(this, request, ...args);
};
(async () => {
  const results = [];
  for (const encoding of ['UTF-8', 'CP949', 'ISO-2022-CN-EXT', 'UCS-4', 'IBM1390', 'IBM1399']) {
    // Benign ASCII XML declaration: exercises type detection/gating, not iconv.
    const buffer = Buffer.from(`<?xml version="1.0" encoding="${encoding}"?><svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>`);
    let status;
    try { await imageOptimizerTransform({ buffer, etag: 'fixture', contentType: 'image/jpeg', cacheControl: null }, { href: 'https://images.igdb.com/igdb/image/upload/t_cover_big/fixture.jpg', width: 64, quality: 75, mimeType: 'image/webp' }, { images: { minimumCacheTTL: 86400, dangerouslyAllowSVG: false }, experimental: {} }); }
    catch (e) { status = e.statusCode; }
    if (status !== 400) throw new Error(`Expected rejection for ${encoding}, got ${status}`);
    results.push({ declaration: encoding, declaredHttpType: 'image/jpeg', detectedType: await detectContentType(buffer), optimizerStatus: status });
  }
  if (sharpLoads !== 0) throw Error('Sharp was loaded');
  console.log(JSON.stringify({ hostNode: process.version, results, sharpLoads, scope: 'Installed Next16.4.6 modules; no native parse or exploit inputs. XML bytes have ASCII declarations, not actual UCS4 encoding. No claim of exhaustive format confusion proof.' }, null, 2));
})().catch(e => { console.error(e); process.exitCode = 1; });
