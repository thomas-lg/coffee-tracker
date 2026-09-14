import { describe, expect, it } from 'vitest';
import { sentenceCaseScan } from './scan-text';

describe('sentenceCaseScan', () => {
  it('stops a bag printed in capitals from shouting in the catalog', () => {
    expect(sentenceCaseScan('LA LIBERTAD')).toBe('La Libertad');
    expect(sentenceCaseScan('INTENSO BLEND')).toBe('Intenso Blend');
  });

  it('leaves casing a roaster chose deliberately alone', () => {
    // Recasing everything would turn this into "Café De Colombia", which is worse than
    // what arrived.
    expect(sentenceCaseScan('Café de Colombia')).toBe('Café de Colombia');
    expect(sentenceCaseScan('mokxa')).toBe('mokxa');
  });

  it('recases only the words that are shouting', () => {
    expect(sentenceCaseScan('Kekchi DARK ROAST')).toBe('Kekchi Dark Roast');
  });

  it('keeps accents, which is most of what these labels are', () => {
    expect(sentenceCaseScan('TORRÉFACTEUR')).toBe('Torréfacteur');
    expect(sentenceCaseScan('BRÉSIL')).toBe('Brésil');
  });

  it('starts at the first letter, not the first character', () => {
    expect(sentenceCaseScan('(BLEND)')).toBe('(Blend)');
    expect(sentenceCaseScan('"ESPRESSO"')).toBe('"Espresso"');
  });

  it('capitalises from the first letter, wherever in the word that is', () => {
    // "250G" keeps its capital because G is its first letter. Odd in isolation, but the
    // alternative is a rule that treats a word differently depending on what precedes
    // its letters, and weight is a parsed field of its own anyway.
    expect(sentenceCaseScan('250G ·  ARABICA')).toBe('250G ·  Arabica');
    expect(sentenceCaseScan('')).toBe('');
  });

  it('leaves a bean grade alone, because it is capitals on purpose', () => {
    // Caught by the e2e before this list existed: "Kirinyaga AA" is a Kenyan screen
    // size and "Kirinyaga Aa" is nothing at all.
    expect(sentenceCaseScan('KIRINYAGA AA')).toBe('Kirinyaga AA');
    expect(sentenceCaseScan('Kirinyaga AA')).toBe('Kirinyaga AA');
    expect(sentenceCaseScan('HUEHUETENANGO SHB')).toBe('Huehuetenango SHB');
    // Still recased, because it is a word rather than a grade.
    expect(sentenceCaseScan('LA LIBERTAD')).toBe('La Libertad');
  });

  it('preserves the spacing it was given', () => {
    // The form shows this verbatim, so collapsing runs would silently edit the value.
    expect(sentenceCaseScan('  LEADING AND  DOUBLE ')).toBe('  Leading And  Double ');
  });
});
