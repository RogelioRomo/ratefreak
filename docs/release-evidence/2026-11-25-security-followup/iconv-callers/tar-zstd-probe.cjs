const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const r = createRequire('/app/package.json');
const tar = r('next/dist/compiled/tar');
const zlib = require('node:zlib');
const fixture = Buffer.alloc(1024);
const frame = zlib.zstdCompressSync(fixture);
function parse(bytes) {
  return new Promise((resolve, reject) => {
    const parser = new tar.Parser();
    parser.on('error', reject); parser.on('end', resolve);
    try { parser.end(bytes); } catch (e) { reject(e); }
  });
}
(async () => {
  await parse(frame);
  process.env.NEXT_RUNTIME = 'nodejs';
  await require('/app/.next/server/instrumentation.js').register();
  await assert.rejects(parse(frame), /decompression is disabled/i);
  await parse(zlib.gzipSync(fixture));
  await parse(fixture);
  console.log(JSON.stringify({ node: process.versions.node, tarLoadedBeforeHook: true, zstdTarDecodesBeforeHook: true, zstdTarRejectedAfterHook: true, gzipTarAfterHook: true, plainTarAfterHook: true, scope: 'Empty 1024-byte tar fixture; real Next bundled tar and compiled instrumentation in exact candidate image. No extraction or filesystem writes.' }));
})().catch(e => { console.error(e); process.exitCode = 1; });
