import { describe, expect, it } from 'vitest';
import { formatDate, formatPrice, formatRating } from './format';

describe('format', () => {
  it('formatRating rounds to one decimal, dashes when null', () => {
    expect(formatRating(null)).toBe('—');
    expect(formatRating(undefined)).toBe('—');
    expect(formatRating(4.25)).toBe('4.3');
  });

  it('formatPrice renders a currency amount', () => {
    expect(formatPrice(18)).toContain('18');
  });

  it('formatDate includes the year', () => {
    expect(formatDate('2026-06-24')).toMatch(/2026/);
  });
});
