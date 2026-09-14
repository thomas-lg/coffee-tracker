/**
 * Curated public model surface for `@coffee-tracker/data`.
 *
 * Field names/shapes mirror the backend DTOs (see `api-types.ts`, generated from the
 * API's OpenAPI doc via `npm run gen:api`). Numbers and dates are tightened to clean
 * runtime types here: the .NET 10 OpenAPI doc types numbers as `number | string`
 * (string-tolerant), but ASP.NET always serialises real JSON numbers.
 *
 * The drift guards at the bottom assert these keep *field-parity* with the generated
 * schemas, if the backend adds/removes a DTO field, regenerating `api-types.ts` makes
 * this file fail to compile until it is reconciled.
 */
import type { components } from './api-types';

type Schemas = components['schemas'];

export interface FlavorTag {
  id: number;
  name: string;
}

/** Roast band, a closed set mirrored from the backend enum. */
export type RoastLevel = 'Light' | 'Medium' | 'Dark';

/** The roast bands in display order, single source for selects/filters. */
export const ROAST_LEVELS: readonly RoastLevel[] = ['Light', 'Medium', 'Dark'];

export interface Coffee {
  id: number;
  name: string;
  roaster: string;
  origin: string;
  roastLevel: RoastLevel;
  price: number;
  /** ISO date (yyyy-MM-dd) */
  dateBought: string;
  /** Signed, ready-to-use image URL (already rooted, e.g. `/photos/x.jpg?exp=…&sig=…`). */
  photoUrl: string | null;
  shopName: string | null;
  purchaseUrl: string | null;
  /** ISO date-time */
  createdAt: string;
  averageRating: number | null;
  reviewCount: number;
  /** Distinct flavour descriptors aggregated across this coffee's reviews. */
  flavorTags: string[];
}

export interface CoffeeCreate {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: RoastLevel;
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
}

export interface AuthResponse {
  token: string;
  /** ISO date-time, when the short-lived access token expires. */
  expiresAt: string;
  /** Opaque refresh token used to obtain the next access/refresh pair. */
  refreshToken: string;
  /** ISO date-time, when the refresh token expires. */
  refreshExpiresAt: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
}

/** Body for POST /api/auth/refresh and /api/auth/logout. */
export interface RefreshRequest {
  refreshToken: string;
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
  /** Whether accounts created in the app may sign in. */
  localLoginEnabled: boolean;
  /** Whether new app accounts may be registered. */
  registrationEnabled: boolean;
  /** Whether an external identity provider is configured and reachable. */
  oidcAvailable: boolean;
  /** How to reach that provider. Present only when `oidcAvailable`. */
  oidc: OidcClientConfig | null;
}

/** Provider coordinates, served by the API so the client configures itself. */
export interface OidcClientConfig {
  authority: string;
  clientId: string;
  scopes: string;
  /** What to call the provider on the button. Null falls back to a generic label. */
  displayName: string | null;
}

/** The provider ID token, presented for an app session. */
export interface OidcSignIn {
  idToken: string;
}

// --- Admin account policy ---
export interface AccountSettings {
  localLoginEnabled: boolean;
  localRegistrationEnabled: boolean;
}

/**
 * Which OCR engine snap-to-fill uses. The names are the wire format, pinned server-side
 * by a string-enum converter, so this union cannot drift into ordinals.
 */
export type OcrEngine = 'RapidOcr' | 'Tesseract' | 'Disabled';

export interface ScanEngineOption {
  engine: OcrEngine;
  /** Whether the engine is installed on this host; a build may ship without one. */
  available: boolean;
}

export interface ScanSettings {
  engine: OcrEngine;
  options: ScanEngineOption[];
}

// --- Admin photo cleanup ---
export interface PhotoListItem {
  /** Raw relative path, what the delete endpoint expects back. */
  path: string;
  /** Signed, ready-to-use display URL. */
  url: string;
  used: boolean;
}

export interface PhotoDeleteResult {
  deleted: number;
  skipped: number;
}

/* ---- compile-time drift guards: curated types must keep field-parity with the
   generated OpenAPI schemas. Each alias must resolve to `true`; a backend field
   add/remove turns it into an error object and breaks the build until reconciled.
   NOTE: this checks KEY parity only, a field whose type/nullability changes
   (without an add/remove) is not caught, since the generated numerics are
   `number | string`. Re-run `npm run gen:api` and re-check this file when DTOs change. ---- */
type SameKeys<A, B> = [keyof A] extends [keyof B]
  ? [keyof B] extends [keyof A]
    ? true
    : { missing_from_model: Exclude<keyof B, keyof A> }
  : { extra_in_model: Exclude<keyof A, keyof B> };
type Assert<T extends true> = T;

/**
 * A whole catalog, as an administrator downloads and restores it. The client only ever
 * reads it back out of a file and hands it straight to the API, so the shapes exist to
 * keep that round trip type-checked rather than to be built by hand here.
 */
export interface Backup {
  formatVersion: number;
  /** ISO date-time */
  exportedAt: string;
  coffees: BackupCoffee[];
}

export interface BackupCoffee {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: RoastLevel;
  price: number;
  /** ISO date */
  dateBought: string;
  photoPath: string | null;
  shopName: string | null;
  purchaseUrl: string | null;
  createdByUserId: string | null;
  createdAt: string;
  reviews: BackupReview[];
}

export interface BackupReview {
  userId: string;
  rating: number;
  stage: string | null;
  tastingNotes: string | null;
  brewMethod: string | null;
  grind: string | null;
  ratio: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** Flavour tags by name, since ids are per-instance. */
  tags: string[];
}

/** What a restore wrote, and anything it skipped on the way. */
export interface ImportResult {
  coffees: number;
  reviews: number;
  warnings: string[];
}


type _GCoffee = Assert<SameKeys<Coffee, Schemas['CoffeeResponseDto']>>;
type _GCoffeeCreate = Assert<SameKeys<CoffeeCreate, Schemas['CoffeeCreateDto']>>;
type _GReview = Assert<SameKeys<Review, Schemas['ReviewResponseDto']>>;
type _GReviewCreate = Assert<SameKeys<ReviewCreate, Schemas['ReviewCreateDto']>>;
type _GFlavorTag = Assert<SameKeys<FlavorTag, Schemas['FlavorTagDto']>>;
type _GScanResult = Assert<SameKeys<ScanResult, Schemas['ScanResponseDto']>>;
type _GScannedCoffee = Assert<SameKeys<ScannedCoffee, Schemas['ScannedCoffeeDto']>>;
type _GAuthResponse = Assert<SameKeys<AuthResponse, Schemas['AuthResponseDto']>>;
type _GRefreshRequest = Assert<SameKeys<RefreshRequest, Schemas['RefreshRequestDto']>>;
type _GLogin = Assert<SameKeys<Login, Schemas['LoginDto']>>;
type _GRegister = Assert<SameKeys<Register, Schemas['RegisterDto']>>;
type _GClientConfig = Assert<SameKeys<ClientConfig, Schemas['ConfigDto']>>;
type _GOidcClientConfig = Assert<SameKeys<OidcClientConfig, Schemas['OidcClientConfigDto']>>;
type _GOidcSignIn = Assert<SameKeys<OidcSignIn, Schemas['OidcSignInDto']>>;
type _GAccountSettings = Assert<SameKeys<AccountSettings, Schemas['AccountSettingsDto']>>;
type _GScanSettings = Assert<SameKeys<ScanSettings, Schemas['ScanSettingsDto']>>;
type _GScanEngineOption = Assert<SameKeys<ScanEngineOption, Schemas['ScanEngineOptionDto']>>;
type _GPhotoListItem = Assert<SameKeys<PhotoListItem, Schemas['PhotoListItemDto']>>;
type _GPhotoDeleteResult = Assert<SameKeys<PhotoDeleteResult, Schemas['PhotoDeleteResultDto']>>;
type _GBackup = Assert<SameKeys<Backup, Schemas['BackupDto']>>;
type _GBackupCoffee = Assert<SameKeys<BackupCoffee, Schemas['BackupCoffeeDto']>>;
type _GBackupReview = Assert<SameKeys<BackupReview, Schemas['BackupReviewDto']>>;
type _GImportResult = Assert<SameKeys<ImportResult, Schemas['ImportResultDto']>>;
