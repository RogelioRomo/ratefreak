const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const runtimeRequire = createRequire(`${process.cwd()}/package.json`);

(async () => {
  const zlib = require('node:zlib');
  const text = 'provider response';
  const frames = { zstd: zlib.zstdCompressSync(text), gzip: zlib.gzipSync(text),
    deflate: zlib.deflateSync(text), br: zlib.brotliCompressSync(text) };
  const server = require('node:http').createServer((request, response) => {
    const encoding = request.url.slice(1);
    response.writeHead(200, { 'Content-Type': 'text/plain', 'Content-Encoding': encoding });
    response.end(frames[encoding]);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  assert.equal(await (await fetch(`${origin}/zstd`)).text(), text);
  const sharp = runtimeRequire('sharp');
  const pixel = { create: { width: 2, height: 2, channels: 3, background: 'red' } };
  const avif = await sharp(pixel).avif().toBuffer();
  assert.equal((await sharp(avif).metadata()).format, 'heif');
  const formats = {};
  for (const format of ['jpeg', 'png', 'webp', 'gif']) formats[format] = await sharp(pixel).toFormat(format).toBuffer();
  process.env.NEXT_RUNTIME = 'nodejs';
  const { register } = require('/app/.next/server/instrumentation.js');
  await register();
  await assert.rejects(() => fetch(`${origin}/zstd`).then(response => response.text()));
  const methods = ['createZstdDecompress', 'ZstdDecompress', 'zstdDecompress', 'zstdDecompressSync'];
  const esm = await import('node:zlib');
  for (const name of methods) {
    assert.throws(() => Reflect.apply(zlib[name], undefined, [frames.zstd]), /decompression is disabled/i);
    assert.throws(() => Reflect.apply(esm[name], undefined, [frames.zstd]), /decompression is disabled/i);
  }
  for (const encoding of ['gzip', 'deflate', 'br']) {
    assert.equal(await (await fetch(`${origin}/${encoding}`)).text(), text);
  }
  server.closeAllConnections();
  await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  // Use the exact lazily initialized Next module after real compiled startup.
  const { getSharp, optimizeImage } = runtimeRequire('next/dist/server/image-optimizer/transform');
  assert.equal(getSharp(undefined, undefined), sharp);
  await assert.rejects(() => optimizeImage({ buffer: avif, contentType: 'image/webp', width: 1, quality: 75 }), /unsupported image format/i);
  await assert.rejects(() => sharp(avif).metadata(), /unsupported image format/i);
  const supported = [];
  for (const [format, input] of Object.entries(formats)) {
    const output = await optimizeImage({ buffer: input, contentType: 'image/webp', width: 1, quality: 75 });
    const metadata = await sharp(output).metadata();
    assert.equal(metadata.format, 'webp');
    assert.equal(metadata.width, 1);
    supported.push(format);
  }
  console.log(JSON.stringify({ compiled_register: true, warm_zstd_fetch_rejected: true, commonjs_and_esm_decoders_rejected: methods, supported_response_encodings: ['gzip', 'deflate', 'br'], node_version: process.versions.node, embedded_zstd_version: process.versions.zstd, next_optimizer_same_sharp: true,
    avif_metadata_rejected: true, avif_optimization_rejected: true, supported_inputs: supported,
    sharp_version: sharp.versions.sharp, heif_version: sharp.versions.heif,
    scope: 'Local loopback HTTP only in a network-disabled container. Exact production compiled startup and optimizer module; safe synthetic inputs, no exploit payload or live-instance access.' }));
})().catch(error => { console.error(error); process.exitCode = 1; });
