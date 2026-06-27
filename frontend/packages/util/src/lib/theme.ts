export type ThemeMode = 'light' | 'dark';

/** The OS-preferred theme — used as the initial default before any user choice. */
export function prefersDark(): boolean {
  return typeof matchMedia !== 'undefined' && matchMedia('(prefers-color-scheme: dark)').matches;
}

/** Reflect the chosen theme onto the document so the CSS tokens flip. */
export function applyTheme(mode: ThemeMode): void {
  document.documentElement.setAttribute('data-theme', mode);
}
