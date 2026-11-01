import type { ThemePreference } from "./theme-preference";

const storageKey = "ratefreak.theme";
const changeEvent = "ratefreak-theme-change";
const mediaQuery = "(prefers-color-scheme: dark)";

export function isThemePreference(value: unknown): value is ThemePreference {
  return value === "light" || value === "dark" || value === "system";
}

export function getThemePreference(): ThemePreference {
  const value = typeof document === "undefined" ? undefined : document.documentElement.dataset.themePreference;
  return isThemePreference(value) ? value : "dark";
}

function applyPreference(preference: ThemePreference): void {
  const dark = preference === "dark" || (preference === "system" && window.matchMedia?.(mediaQuery).matches);
  document.documentElement.dataset.themePreference = preference;
  document.documentElement.dataset.theme = dark ? "dark" : "light";
}

export function setThemePreference(preference: ThemePreference): void {
  applyPreference(preference);
  try { window.localStorage.setItem(storageKey, preference); } catch { /* Keep the current tab usable without storage. */ }
  window.dispatchEvent(new Event(changeEvent));
}

export function subscribeTheme(changed: () => void): () => void {
  const media = window.matchMedia?.(mediaQuery);
  const onSystemChange = () => { applyPreference(getThemePreference()); changed(); };
  const onStorage = (event: StorageEvent) => {
    if (event.key !== storageKey && event.key !== null) return;
    try { if (event.storageArea && event.storageArea !== window.localStorage) return; } catch { return; }
    applyPreference(isThemePreference(event.newValue) ? event.newValue : "dark");
    changed();
  };
  media?.addEventListener("change", onSystemChange);
  window.addEventListener("storage", onStorage);
  window.addEventListener(changeEvent, changed);
  onSystemChange();
  return () => {
    media?.removeEventListener("change", onSystemChange);
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(changeEvent, changed);
  };
}
