/**
 * Tidies a value read off a bag before it lands in the form.
 *
 * Labels are printed in capitals, so OCR returns "LA LIBERTAD" and "TORREFACTEUR", and
 * pre-filling those verbatim means the catalog shouts wherever they are shown. Only
 * fully uppercase words are touched: a bag that prints "Café de Colombia" already has
 * the casing its roaster chose, and forcing every word would make that worse, not
 * better.
 *
 * Applies to the free-text fields alone. Origin and roast level come back from closed
 * vocabularies the parser has already canonicalised.
 */
export function sentenceCaseScan(value: string): string {
  return value.replace(/\S+/g, (word) => (isShouting(word) ? capitalise(word) : word));
}

/**
 * Grades and certifications that are capitals on purpose, so recasing them is wrong:
 * "Kirinyaga AA" is a Kenyan screen size, and "Kirinyaga Aa" is nothing.
 *
 * A list rather than a rule, for the same reason the parser keeps one for origins: no
 * general test separates these from ordinary shouted words. "AA" is a grade and "LA" is
 * an article, and both are two capital letters.
 */
const PRESERVED = new Set([
  // Screen sizes and bean grades
  'AA',
  'AAA',
  'AB',
  'PB',
  'TT',
  // Hardness and altitude grades
  'SHB',
  'SHG',
  'HB',
  'HG',
  'EP',
  'MG',
  // Certification marks
  'COE',
  'FTO',
  'RFA',
  'UTZ',
]);

/**
 * A word is shouting when it has letters, none of them lowercase, and it is not one of
 * the marks above. "PACIFIC" is; "250g" and "-" have nothing to change and pass through.
 */
function isShouting(word: string): boolean {
  if (word === word.toLowerCase() || word !== word.toUpperCase()) return false;
  return !PRESERVED.has(word.replace(/[^\p{L}]/gu, ''));
}

/**
 * Capitalises from the first letter rather than the first character, so a leading quote
 * or bracket does not absorb it: "(BLEND)" has to become "(Blend)", not "(blend)".
 */
function capitalise(word: string): string {
  const first = word.search(/\p{L}/u);
  if (first === -1) return word;
  return word.slice(0, first) + word[first]!.toUpperCase() + word.slice(first + 1).toLowerCase();
}
