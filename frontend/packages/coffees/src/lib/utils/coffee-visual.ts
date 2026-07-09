/** Buckets a free-text roast level into the three coarse bands the UI keys off. */
export function roastBucket(roastLevel: string): 'Light' | 'Medium' | 'Dark' {
  const r = roastLevel.toLowerCase();
  if (r.includes('light')) return 'Light';
  if (r.includes('dark')) return 'Dark';
  return 'Medium';
}

/** Warm gradient used as a coffee-bag "label" when there's no photo. */
export function roastGradient(roastLevel: string): string {
  // Closed key union (not Record<string, …>): indexing by a bucket can't miss,
  // so no undefined creeps in under noUncheckedIndexedAccess.
  const map: Record<'Light' | 'Medium' | 'Dark', [string, string]> = {
    Light: ['#c98f4e', '#9a6536'],
    Medium: ['#7c4f30', '#4e3018'],
    Dark: ['#3a2415', '#160b05'],
  };
  const [a, b] = map[roastBucket(roastLevel)];
  return `radial-gradient(130% 120% at 25% 20%, ${a}, ${b})`;
}
