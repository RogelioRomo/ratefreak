// Static first-party code only. Apply a saved preference before the page paints.
export const THEME_BOOTSTRAP = `(() => {
  let preference = "dark";
  try {
    const saved = localStorage.getItem("ratefreak.theme");
    if (saved === "light" || saved === "dark" || saved === "system") preference = saved;
  } catch {}
  const dark = preference === "dark" ||
    (preference === "system" && window.matchMedia?.("(prefers-color-scheme: dark)").matches);
  document.documentElement.dataset.themePreference = preference;
  document.documentElement.dataset.theme = dark ? "dark" : "light";
})();`;
