"use client";

import { useSyncExternalStore } from "react";
import { IconDeviceDesktop, IconMoon, IconSun } from "@tabler/icons-react";
import { getThemePreference, setThemePreference, subscribeTheme } from "@/shared/theme/theme";
import styles from "./theme-control.module.css";

const serverPreference = () => "dark" as const;

export function ThemeControl() {
  const preference = useSyncExternalStore(subscribeTheme, getThemePreference, serverPreference);
  return <div className={styles.control} role="group" aria-label="Theme">
    <button type="button" aria-label="Light theme" title="Light theme" aria-pressed={preference === "light"} onClick={() => setThemePreference("light")}>
      <IconSun size={16} stroke={1.75} aria-hidden="true" />
    </button>
    <button type="button" aria-label="Dark theme" title="Dark theme" aria-pressed={preference === "dark"} onClick={() => setThemePreference("dark")}>
      <IconMoon size={16} stroke={1.75} aria-hidden="true" />
    </button>
    <button type="button" aria-label="Use system theme" title="Use system theme" aria-pressed={preference === "system"} onClick={() => setThemePreference("system")}>
      <IconDeviceDesktop size={16} stroke={1.75} aria-hidden="true" />
    </button>
  </div>;
}
