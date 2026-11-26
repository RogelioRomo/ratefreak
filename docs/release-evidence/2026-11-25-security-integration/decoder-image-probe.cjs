const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const runtimeRequire = createRequire(`${process.cwd()}/package.json`);

(async () => {
  const sharp = runtimeRequire('sharp');
  const pixel = { create: { width: 2, height: 2, channels: 3, background: 'red' } };
  const avif = await sharp(pixel).avif().toBuffer();
  assert.equal((await sharp(avif).metadata()).format, 'heif');
  const formats = {};
  for (const format of ['jpeg', 'png', 'webp', 'gif']) formats[format] = await sharp(pixel).toFormat(format).toBuffer();
  process.env.NEXT_RUNTIME = 'nodejs';
  const { register } = require('/app/.next/server/instrumentation.js');
  await register();
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
  console.log(JSON.stringify({ compiled_register: true, next_optimizer_same_sharp: true,
    avif_metadata_rejected: true, avif_optimization_rejected: true, supported_inputs: supported,
    sharp_version: sharp.versions.sharp, heif_version: sharp.versions.heif,
    scope: 'Exact production compiled startup and optimizer module; safe synthetic inputs, no exploit payload or live-instance access.' }));
})().catch(error => { console.error(error); process.exitCode = 1; });
