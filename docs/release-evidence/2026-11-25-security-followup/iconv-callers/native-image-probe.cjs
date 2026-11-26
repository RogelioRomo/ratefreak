const { createRequire } = require('node:module');
const r = createRequire('/app/package.json');
(async () => {
  const sharp = r('sharp');
  const pixel = { create: { width: 2, height: 2, channels: 3, background: 'red' } };
  const fixtures = {};
  for (const format of ['jpeg', 'png', 'webp', 'gif']) fixtures[format] = await sharp(pixel).toFormat(format).toBuffer();
  process.env.NEXT_RUNTIME = 'nodejs';
  await require('/app/.next/server/instrumentation.js').register();
  const { imageOptimizerTransform } = r('next/dist/server/image-optimizer/transform');
  const config = { images: { minimumCacheTTL: 86400, dangerouslyAllowSVG: false }, experimental: {} };
  const params = { href: 'https://images.igdb.com/igdb/image/upload/t_cover_big/fixture.jpg', width: 64, quality: 75, mimeType: 'image/webp' };
  const results = [];
  for (const encoding of ['UTF-8', 'CP949', 'ISO-2022-CN-EXT']) {
    // Entire content is benign ASCII, valid in each declared ASCII-compatible charset.
    const buffer = Buffer.from(`<?xml version="1.0" encoding="${encoding}"?><svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>`);
    console.error(`CASE optimizer-svg ${encoding}`);
    let optimizerStatus;
    try { await imageOptimizerTransform({ buffer, etag: 'fixture', contentType: 'image/jpeg', cacheControl: null }, params, config); }
    catch (e) { optimizerStatus = e.statusCode; }
    console.error(`CASE direct-native-svg ${encoding}`);
    let directNativeResult;
    try { directNativeResult = (await sharp(buffer).metadata()).format; }
    catch (e) { directNativeResult = 'rejected:' + e.message; }
    results.push({ encoding, optimizerStatus, directNativeResult });
    if (optimizerStatus !== 400) throw Error('SVG reached optimizer');
  }
  for (const [format, buffer] of Object.entries(fixtures)) {
    console.error(`CASE optimizer-raster ${format}`);
    await imageOptimizerTransform({ buffer, etag: 'fixture', contentType: `image/${format}`, cacheControl: null }, params, config);
  }
  console.log(JSON.stringify({ imageProbe: true, node: process.versions.node, libvips: sharp.versions.vips, results, rasterFormats: Object.keys(fixtures), scope: 'Benign ASCII-compatible XML and generated raster images only; direct Sharp native capability is distinguished from supported optimizer gate. Trace records charset names, never data. No live instance or network.' }, null, 2));
})().catch(e => { console.error(e); process.exitCode = 1; });
