export type ThemeMode = 'light' | 'dark';

const THEME_KEY = 'ct.theme';

/** The OS-preferred theme — used as the default when the user hasn't chosen one. */
export function prefersDark(): boolean {
  return typeof matchMedia !== 'undefined' && matchMedia('(prefers-color-scheme: dark)').matches;
}

/** The theme to start with: the user's persisted choice, else the OS preference. */
export function initialTheme(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    // Storage unavailable (private mode / SSR) — fall through to the OS preference.
  }
  return prefersDark() ? 'dark' : 'light';
}

/** Persist the user's explicit theme choice so it survives reloads. */
export function persistTheme(mode: ThemeMode): void {
  try {
    localStorage.setItem(THEME_KEY, mode);
  } catch {
    // Best-effort — the in-page theme still applies.
  }
}

/** Reflect the chosen theme onto the document so the CSS tokens flip. */
export function applyTheme(mode: ThemeMode): void {
  document.documentElement.setAttribute('data-theme', mode);
}
