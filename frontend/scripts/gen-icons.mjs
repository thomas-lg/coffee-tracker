// Regenerate the PWA icon set + favicon from the master SVG.
//   node scripts/gen-icons.mjs   (or: npm run gen:icons)
import sharp from 'sharp';
import pngToIco from 'png-to-ico';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const root = new URL('../packages/app/public/', import.meta.url);
const svg = readFileSync(fileURLToPath(new URL('icon.svg', root)));

const SIZES = [72, 96, 128, 144, 152, 192, 384, 512];

const render = (size) =>
  sharp(svg, { density: 384 }).resize(size, size, { fit: 'contain' }).png();

for (const size of SIZES) {
  const out = fileURLToPath(new URL(`icons/icon-${size}x${size}.png`, root));
  await render(size).toFile(out);
  console.log(`wrote icons/icon-${size}x${size}.png`);
}

// favicon.ico bundles 16/32/48 px.
const faviconBuffers = await Promise.all([16, 32, 48].map((s) => render(s).toBuffer()));
writeFileSync(fileURLToPath(new URL('favicon.ico', root)), await pngToIco(faviconBuffers));
console.log('wrote favicon.ico');
