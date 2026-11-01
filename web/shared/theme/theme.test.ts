// @vitest-environment jsdom
import { beforeEach, expect, it, vi } from "vitest";
import { THEME_BOOTSTRAP } from "./theme-bootstrap";
import { getThemePreference, setThemePreference, subscribeTheme } from "./theme";

let systemDark = false;
let mediaChange: (() => void) | undefined;
const mediaRemove = vi.fn();

beforeEach(() => {
  localStorage.clear();
  delete document.documentElement.dataset.theme;
  delete document.documentElement.dataset.themePreference;
  systemDark = false;
  mediaChange = undefined;
  vi.stubGlobal("matchMedia", vi.fn(() => ({
    get matches() { return systemDark; },
    addEventListener: (_: string, fn: () => void) => { mediaChange = fn; },
    removeEventListener: mediaRemove,
  })));
});

function bootstrap() { new Function(THEME_BOOTSTRAP)(); }

it("initializes a dark first paint without a saved preference even on a light device", () => {
  bootstrap();
  expect(document.documentElement.dataset.theme).toBe("dark");
  expect(getThemePreference()).toBe("dark");
});

it.each(["light", "dark", "system"])("applies saved %s before the app hydrates", (preference) => {
  systemDark = true;
  localStorage.setItem("ratefreak.theme", preference);
  bootstrap();
  expect(document.documentElement.dataset.theme).toBe(preference === "light" ? "light" : "dark");
  expect(getThemePreference()).toBe(preference);
});

it("ignores invalid stored values", () => {
  localStorage.setItem("ratefreak.theme", "invalid");
  bootstrap();
  expect(document.documentElement.dataset.theme).toBe("dark");
  expect(getThemePreference()).toBe("dark");
});

it("switches immediately and persists the preference", () => {
  const changed = vi.fn();
  const unsubscribe = subscribeTheme(changed);
  setThemePreference("dark");
  expect(document.documentElement.dataset.theme).toBe("dark");
  expect(localStorage.getItem("ratefreak.theme")).toBe("dark");
  expect(changed).toHaveBeenCalled();
  unsubscribe();
  expect(mediaRemove).toHaveBeenCalled();
});

it("follows device changes only while System is selected", () => {
  const unsubscribe = subscribeTheme(vi.fn());
  setThemePreference("system");
  expect(document.documentElement.dataset.theme).toBe("light");
  systemDark = true;
  mediaChange?.();
  expect(document.documentElement.dataset.theme).toBe("dark");
  setThemePreference("light");
  mediaChange?.();
  expect(document.documentElement.dataset.theme).toBe("light");
  unsubscribe();
});

it("syncs preference changes and removal from other tabs", () => {
  const unsubscribe = subscribeTheme(vi.fn());
  window.dispatchEvent(new StorageEvent("storage", { key: "ratefreak.theme", newValue: "dark", storageArea: localStorage }));
  expect(getThemePreference()).toBe("dark");
  expect(document.documentElement.dataset.theme).toBe("dark");
  window.dispatchEvent(new StorageEvent("storage", { key: "unrelated", newValue: "light", storageArea: localStorage }));
  expect(getThemePreference()).toBe("dark");
  window.dispatchEvent(new StorageEvent("storage", { key: "ratefreak.theme", newValue: null, storageArea: localStorage }));
  expect(getThemePreference()).toBe("dark");
  unsubscribe();
});

it("keeps initialization and switching usable when storage is blocked", () => {
  vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
  vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
  expect(bootstrap).not.toThrow();
  setThemePreference("dark");
  expect(getThemePreference()).toBe("dark");
  expect(document.documentElement.dataset.theme).toBe("dark");
});
