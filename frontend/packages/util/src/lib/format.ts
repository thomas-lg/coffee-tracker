/** "4.2" or "—" when there are no ratings yet. */
export function formatRating(value: number | null | undefined): string {
  return value == null ? '—' : value.toFixed(1);
}

/** Localized currency string for a coffee price (defaults to EUR). */
export function formatPrice(value: number, currency = 'EUR', locale = 'en-IE'): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(value);
}

/** "24 Jun 2026" from an ISO date / date-time string. */
export function formatDate(iso: string, locale = 'en-GB'): string {
  return new Date(iso).toLocaleDateString(locale, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}
