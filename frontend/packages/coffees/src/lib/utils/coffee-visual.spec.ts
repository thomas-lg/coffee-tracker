import { describe, expect, it } from 'vitest';
import { roastBucket, roastGradient } from './coffee-visual';

describe('coffee-visual', () => {
  it('buckets free-text roast levels', () => {
    expect(roastBucket('Light')).toBe('Light');
    expect(roastBucket('Medium-Dark')).toBe('Dark');
    expect(roastBucket('Medium')).toBe('Medium');
    expect(roastBucket('whatever')).toBe('Medium');
  });

  it('produces a radial-gradient for the bag label', () => {
    expect(roastGradient('Light')).toContain('radial-gradient');
    expect(roastGradient('Dark')).toContain('radial-gradient');
  });
});
