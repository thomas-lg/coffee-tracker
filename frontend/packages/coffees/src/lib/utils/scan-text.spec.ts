import { describe, expect, it } from 'vitest';
import { titleCaseScan } from './scan-text';

describe('titleCaseScan', () => {
  it('stops a bag printed in capitals from shouting in the catalog', () => {
    expect(titleCaseScan('INTENSO BLEND')).toBe('Intenso Blend');
    expect(titleCaseScan('GREEN LION COFFEE')).toBe('Green Lion Coffee');
    expect(titleCaseScan('LA LIBERTAD')).toBe('La Libertad');
  });

  it('leaves casing a roaster chose deliberately alone', () => {
    // Recasing everything would turn this into "Café De Colombia", which is worse than
    // what arrived.
    expect(titleCaseScan('Café de Colombia')).toBe('Café de Colombia');
    expect(titleCaseScan('mokxa')).toBe('mokxa');
  });

  it('recases only the words that are shouting', () => {
    expect(titleCaseScan('Kekchi DARK ROAST')).toBe('Kekchi Dark Roast');
  });

  it('leaves a bean grade alone, because it is capitals on purpose', () => {
    // Caught by the e2e before this list existed: "Kirinyaga AA" is a Kenyan screen
    // size, and "Kirinyaga Aa" is nothing at all.
    expect(titleCaseScan('KIRINYAGA AA')).toBe('Kirinyaga AA');
    expect(titleCaseScan('Kirinyaga AA')).toBe('Kirinyaga AA');
    expect(titleCaseScan('HUEHUETENANGO SHB')).toBe('Huehuetenango SHB');
  });

  it('keeps accents, which is most of what these labels are', () => {
    expect(titleCaseScan('TORRÉFACTEUR')).toBe('Torréfacteur');
    expect(titleCaseScan('BRÉSIL')).toBe('Brésil');
  });

  it('starts at the first letter, not the first character', () => {
    expect(titleCaseScan('(BLEND)')).toBe('(Blend)');
    expect(titleCaseScan('"ESPRESSO"')).toBe('"Espresso"');
  });

  it('passes through a value with nothing to change', () => {
    expect(titleCaseScan('250g')).toBe('250g');
    expect(titleCaseScan('')).toBe('');
  });

  it('preserves the spacing it was given', () => {
    // The form shows this verbatim, so collapsing runs would silently edit the value.
    expect(titleCaseScan('  LEADING AND  DOUBLE ')).toBe('  Leading And  Double ');
  });
});
