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
 * A word is shouting when it has letters and none of them are lowercase. "PACIFIC" is;
 * "IX" is too, which is the acceptable cost of not keeping a list of exceptions.
 * "250g" and "-" are not, so they pass through untouched.
 */
function isShouting(word: string): boolean {
  return word !== word.toLowerCase() && word === word.toUpperCase();
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
