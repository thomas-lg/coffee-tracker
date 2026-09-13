// Renders the synthetic half of the OCR benchmark corpus: coffee-bag fronts, each
// photographed badly on purpose.
//
//   node scripts/generate-ocr-fixtures.mjs
//
// Why rendered rather than photographed: a benchmark has to be reproducible and has to
// ship in the repo, and neither is true of a folder of real photos of my own shelf. What
// these DO give is a fixed target that moves only when the pipeline moves, so a change to
// the adapter can be scored instead of argued about.
//
// What they do NOT give is real-world accuracy. Rendered text is too clean, so every
// fixture is degraded through CSS the way a phone camera degrades a bag: rotation,
// perspective, glare, blur, low contrast on dark packaging, and JPEG artefacts from the
// screenshot encoder. `backend/CoffeeTracker.Tests/Ocr/Fixtures/real/` is where real
// photos go, and the benchmark scores them alongside these; see its README.
//
// Playwright rather than an image library: the degradations that matter here are
// perspective, gradients and blur, which a browser already does well, and the repo
// already carries Playwright for the screenshots.

import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

const OUT = process.env.OUT_DIR ?? 'backend/CoffeeTracker.Tests/Ocr/Fixtures/synthetic';

/**
 * The labels, and what the pipeline is expected to make of each one. The manifest is
 * generated from the same object that renders the image, so the two cannot drift.
 *
 * `weight` is printed one way and parsed another (`CoffeeLabelParser` normalises the
 * unit), so both spellings are written out rather than one being re-derived here.
 *
 * `expectRoaster: false` marks a bag whose roaster the parser is *expected to leave
 * null*. It requires positive evidence — a line actually saying "Roasters", "Roastery"
 * or "Coffee Co" — and "Onyx Coffee Lab" carries none. That is the parser's stated
 * contract (a null the user fills in beats a confident wrong value), so the benchmark
 * scores it as a pass and would flag an invented roaster as a failure.
 *
 * `filler` is the small print a real bag carries, and it is what makes the corpus worth
 * scoring: several filler lines deliberately bury an origin or roast word in prose
 * ("notes of brazil nut and dark chocolate"), which is exactly the trap `FindKeyword`
 * has to score its way out of.
 */
const LABELS = [
  {
    id: 'kirinyaga',
    roaster: 'La Cabra Coffee Roasters',
    expectRoaster: true,
    name: 'Kirinyaga AA',
    origin: 'Kenya',
    roastLevel: 'Light',
    weight: { printed: '250g', parsed: '250g' },
    filler: ['Washed · SL28 · 1750 masl', 'Notes of blackcurrant and cane sugar', 'Roasted in Aarhus'],
  },
  {
    id: 'yirgacheffe',
    roaster: 'Tim Wendelboe Roastery',
    expectRoaster: true,
    name: 'Konga Natural',
    origin: 'Yirgacheffe',
    roastLevel: 'Medium',
    weight: { printed: '340g', parsed: '340g' },
    filler: ['Natural process', 'Harvest 2025', 'Notes of brazil nut and dark chocolate'],
  },
  {
    id: 'huila',
    roaster: 'Coffee Collective Roasters',
    expectRoaster: true,
    name: 'El Mirador',
    origin: 'Huila',
    roastLevel: 'Medium-Dark',
    weight: { printed: '1kg', parsed: '1kg' },
    filler: ['Caturra & Castillo', 'Fully washed, 1900 masl', 'Best before end of the month'],
  },
  {
    id: 'guji',
    roaster: 'Square Mile Coffee Roasters',
    expectRoaster: true,
    name: 'Hambela Estate',
    origin: 'Guji',
    roastLevel: 'Light-Medium',
    weight: { printed: '500 g', parsed: '500g' },
    filler: ['Anaerobic natural', 'Filter roast', 'Grind fresh, brew hot'],
  },
  {
    id: 'antigua',
    roaster: 'Gardelli Specialty',
    expectRoaster: false,
    name: 'Finca El Injerto',
    origin: 'Guatemala',
    roastLevel: 'Dark',
    weight: { printed: '200g', parsed: '200g' },
    filler: ['Bourbon · 1600 masl', 'Notes of plum, cocoa and orange peel', 'Specialty coffee'],
  },
  {
    id: 'sumatra',
    roaster: 'Onyx Coffee Lab',
    expectRoaster: false,
    name: 'Bies Penantan',
    origin: 'Sumatra',
    roastLevel: 'Espresso',
    weight: { printed: '12 oz', parsed: '12oz' },
    filler: ['Wet hulled', 'Ateng varietal, 1400 masl', 'Roast date on the seal'],
  },
];

/**
 * How each label is photographed. Every condition is one real way a phone picture of a
 * bag goes wrong, and each is applied to every label, so a regression shows up as a
 * condition that stops scoring rather than a single fixture flipping.
 */
const CONDITIONS = [
  {
    id: 'flat',
    // The control. If this one ever drops, the problem is the parser, not the optics.
    bag: { background: '#f4f1ea', ink: '#211d18' },
    transform: 'none',
    filter: 'none',
    overlay: 'none',
  },
  {
    id: 'angled',
    // A bag held at arm's length is never square to the lens.
    bag: { background: '#efe6d4', ink: '#2b241c' },
    transform: 'perspective(1400px) rotateY(-16deg) rotateZ(-5deg)',
    filter: 'none',
    overlay: 'none',
  },
  {
    id: 'dark',
    // Matte black packaging with foil type: the case where contrast, not resolution,
    // is what the engine runs out of.
    bag: { background: '#26231f', ink: '#b9b2a4' },
    transform: 'perspective(1600px) rotateX(7deg)',
    filter: 'contrast(0.82)',
    overlay: 'none',
  },
  {
    id: 'glare',
    // A window behind the photographer. The blown-out band crosses the label, so part
    // of the text is genuinely unrecoverable and the engine has to not invent it.
    bag: { background: '#f1ece0', ink: '#26211a' },
    transform: 'perspective(1500px) rotateZ(3deg)',
    filter: 'brightness(1.06)',
    overlay:
      'linear-gradient(102deg, rgba(255,255,255,0) 34%, rgba(255,255,255,0.92) 47%, rgba(255,255,255,0.15) 62%, rgba(0,0,0,0.14) 100%)',
  },
  {
    id: 'soft',
    // Handheld in a kitchen at night: missed focus plus sensor noise.
    bag: { background: '#e9e2d4', ink: '#312a22' },
    transform: 'perspective(1800px) rotateY(9deg) rotateZ(2deg)',
    filter: 'blur(1.15px) contrast(0.9) brightness(0.95)',
    overlay: 'none',
  },
];

/** Paper grain, as an inline SVG so the page needs nothing from the network. */
const GRAIN =
  "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='240' height='240'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='3'/%3E%3C/filter%3E%3Crect width='240' height='240' filter='url(%23n)' opacity='0.35'/%3E%3C/svg%3E\")";

function page(label, condition) {
  const { background, ink } = condition.bag;
  return `<!doctype html>
<meta charset="utf-8">
<style>
  * { margin: 0; box-sizing: border-box; }
  body {
    width: 900px; height: 1200px; display: grid; place-items: center;
    /* A table, not a void: the surface around the bag is where the junk lines the
       confidence gate has to reject actually come from. */
    background: #6b5844 ${GRAIN};
    background-blend-mode: multiply;
    font-family: Georgia, 'Times New Roman', serif;
  }
  .bag {
    width: 620px; height: 940px; padding: 72px 56px;
    display: flex; flex-direction: column; gap: 34px;
    background: ${background}; color: ${ink};
    border-radius: 10px 10px 4px 4px;
    box-shadow: 0 30px 60px rgba(0,0,0,0.45);
    transform: ${condition.transform};
    filter: ${condition.filter};
    position: relative; overflow: hidden;
  }
  .bag::after {
    content: ''; position: absolute; inset: 0; pointer-events: none;
    background: ${condition.overlay};
  }
  .roaster { font-size: 26px; letter-spacing: 0.32em; text-transform: uppercase; font-family: Helvetica, Arial, sans-serif; }
  .name { font-size: 76px; line-height: 1.04; font-weight: 700; }
  .origin { font-size: 40px; letter-spacing: 0.06em; }
  .roast { font-size: 30px; font-family: Helvetica, Arial, sans-serif; letter-spacing: 0.18em; text-transform: uppercase; }
  .filler { margin-top: auto; display: flex; flex-direction: column; gap: 14px; font-size: 22px; opacity: 0.85; }
  .weight { font-size: 34px; font-family: Helvetica, Arial, sans-serif; letter-spacing: 0.1em; }
</style>
<div class="bag">
  <div class="roaster">${label.roaster}</div>
  <div class="name">${label.name}</div>
  <div class="origin">${label.origin}</div>
  <div class="roast">${label.roastLevel} Roast</div>
  <div class="filler">
    ${label.filler.map((f) => `<div>${f}</div>`).join('\n    ')}
    <div class="weight">${label.weight.printed}</div>
  </div>
</div>`;
}

await mkdir(OUT, { recursive: true });
const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 900, height: 1200 } });
const tab = await context.newPage();

const fixtures = [];
for (const label of LABELS) {
  for (const condition of CONDITIONS) {
    const file = `${label.id}-${condition.id}.jpg`;
    await tab.setContent(page(label, condition));
    await tab.screenshot({
      path: join(OUT, file),
      type: 'jpeg',
      // Low enough to leave the ringing artefacts a phone's own encoder leaves around
      // high-contrast type, which is what the engine actually has to read through.
      quality: 62,
    });
    fixtures.push({
      file,
      condition: condition.id,
      expected: {
        name: label.name,
        roaster: label.expectRoaster ? label.roaster : null,
        origin: label.origin,
        roastLevel: label.roastLevel,
        weight: label.weight.parsed,
      },
    });
  }
}

await browser.close();
await writeFile(join(OUT, 'manifest.json'), `${JSON.stringify({ fixtures }, null, 2)}\n`, 'utf8');
console.log(`wrote ${fixtures.length} fixtures to ${OUT}`);
