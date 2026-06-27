/**
 * Curated public model surface for `@coffee-tracker/data`.
 *
 * Field names/shapes mirror the backend DTOs (see `api-types.ts`, generated from the
 * API's OpenAPI doc via `npm run gen:api`). Numbers and dates are tightened to clean
 * runtime types here: the .NET 10 OpenAPI doc types numbers as `number | string`
 * (string-tolerant), but ASP.NET always serialises real JSON numbers.
 *
 * The drift guards at the bottom assert these keep *field-parity* with the generated
 * schemas — if the backend adds/removes a DTO field, regenerating `api-types.ts` makes
 * this file fail to compile until it is reconciled.
 */
import type { components } from './api-types';

type Schemas = components['schemas'];

export interface FlavorTag {
  id: number;
  name: string;
}

export interface Coffee {
  id: number;
  name: string;
  roaster: string;
  origin: string;
  roastLevel: string;
  price: number;
  /** ISO date (yyyy-MM-dd) */
  dateBought: string;
  photoPath: string | null;
  shopName: string | null;
  purchaseUrl: string | null;
  /** ISO date-time */
  createdAt: string;
  averageRating: number | null;
  reviewCount: number;
}

export interface CoffeeCreate {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: string;
  price: number;
  /** ISO date (yyyy-MM-dd) */
  dateBought: string;
  shopName?: string | null;
  purchaseUrl?: string | null;
}
export type CoffeeUpdate = CoffeeCreate;

export interface Review {
  id: number;
  coffeeId: number;
  userId: string;
  rating: number;
  stage: string | null;
  tastingNotes: string | null;
  brewMethod: string | null;
  grind: string | null;
  ratio: string | null;
  /** ISO date-time */
  createdAt: string;
  updatedAt: string | null;
  tags: FlavorTag[];
}

export interface ReviewCreate {
  rating: number;
  stage?: string | null;
  tastingNotes?: string | null;
  brewMethod?: string | null;
  grind?: string | null;
  ratio?: string | null;
  tagIds?: number[];
}
export type ReviewUpdate = ReviewCreate;

export interface ScannedCoffee {
  name: string | null;
  roaster: string | null;
  origin: string | null;
  roastLevel: string | null;
  weight: string | null;
}

export interface ScanResult {
  rawText: string;
  parsed: ScannedCoffee;
  photoPath: string;
}

export interface AuthResponse {
  token: string;
  /** ISO date-time */
  expiresAt: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
}

export interface Login {
  email: string;
  password: string;
}

export interface Register {
  email: string;
  password: string;
  displayName: string;
}

export interface ClientConfig {
  registrationEnabled: boolean;
}

// --- Admin photo cleanup (M5-review follow-up). These endpoints post-date the last
// `gen:api` run, so they have no api-types.ts schema yet and thus no drift guard
// below — re-run `npm run gen:api` once the OpenAPI doc includes /api/admin/photos. ---
export interface PhotoListItem {
  path: string;
  used: boolean;
}

export interface PhotoDeleteResult {
  deleted: number;
  skipped: number;
}

/* ---- compile-time drift guards: curated types must keep field-parity with the
   generated OpenAPI schemas. Each alias must resolve to `true`; a backend field
   add/remove turns it into an error object and breaks the build until reconciled.
   NOTE: this checks KEY parity only — a field whose type/nullability changes
   (without an add/remove) is not caught, since the generated numerics are
   `number | string`. Re-run `npm run gen:api` and re-check this file when DTOs change. ---- */
type SameKeys<A, B> = [keyof A] extends [keyof B]
  ? [keyof B] extends [keyof A]
    ? true
    : { missing_from_model: Exclude<keyof B, keyof A> }
  : { extra_in_model: Exclude<keyof A, keyof B> };
type Assert<T extends true> = T;

type _GCoffee = Assert<SameKeys<Coffee, Schemas['CoffeeResponseDto']>>;
type _GCoffeeCreate = Assert<SameKeys<CoffeeCreate, Schemas['CoffeeCreateDto']>>;
type _GReview = Assert<SameKeys<Review, Schemas['ReviewResponseDto']>>;
type _GReviewCreate = Assert<SameKeys<ReviewCreate, Schemas['ReviewCreateDto']>>;
type _GFlavorTag = Assert<SameKeys<FlavorTag, Schemas['FlavorTagDto']>>;
type _GScanResult = Assert<SameKeys<ScanResult, Schemas['ScanResponseDto']>>;
type _GScannedCoffee = Assert<SameKeys<ScannedCoffee, Schemas['ScannedCoffeeDto']>>;
type _GAuthResponse = Assert<SameKeys<AuthResponse, Schemas['AuthResponseDto']>>;
type _GLogin = Assert<SameKeys<Login, Schemas['LoginDto']>>;
type _GRegister = Assert<SameKeys<Register, Schemas['RegisterDto']>>;
type _GClientConfig = Assert<SameKeys<ClientConfig, Schemas['ConfigDto']>>;
