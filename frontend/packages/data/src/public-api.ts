/*
 * Public API surface of @coffee-tracker/data
 *
 * Curated DTO models + thin injectable HTTP services. The generated `api-types.ts`
 * stays internal; consumers use the friendly `models` types.
 */

export * from './lib/models';
export * from './lib/coffees.api';
export * from './lib/reviews.api';
export * from './lib/flavor-tags.api';
export * from './lib/scan.api';
export * from './lib/config.api';
export * from './lib/auth.api';
