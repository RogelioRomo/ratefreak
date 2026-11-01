// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ThemeControl } from "./theme-control";
import { THEME_BOOTSTRAP } from "@/shared/theme/theme-bootstrap";

beforeEach(() => {
  localStorage.clear();
  vi.stubGlobal("matchMedia", () => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
  new Function(THEME_BOOTSTRAP)();
});

it("offers labeled theme choices and switches without losing adjacent form input", () => {
  render(<><ThemeControl /><label>Email<input name="email" defaultValue="reader@example.test" /></label></>);
  const input = screen.getByLabelText("Email");
  expect(screen.getByRole("group", { name: "Theme" })).toBeInTheDocument();
  expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Dark theme" })).toHaveAttribute("aria-pressed", "true");
  fireEvent.click(screen.getByRole("button", { name: "Light theme" }));
  expect(screen.getByRole("button", { name: "Light theme" })).toHaveAttribute("aria-pressed", "true");
  fireEvent.click(screen.getByRole("button", { name: "Dark theme" }));
  expect(screen.getByRole("button", { name: "Dark theme" })).toHaveAttribute("aria-pressed", "true");
  expect(screen.getByRole("button", { name: "Light theme" })).toHaveAttribute("aria-pressed", "false");
  expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  expect(localStorage.getItem("ratefreak.theme")).toBe("dark");
  expect(screen.getByLabelText("Email")).toBe(input);
  expect(input).toHaveValue("reader@example.test");
  fireEvent.click(screen.getByRole("button", { name: "Use system theme" }));
  expect(localStorage.getItem("ratefreak.theme")).toBe("system");
  expect(screen.getByRole("button", { name: "Use system theme" })).toHaveAttribute("aria-pressed", "true");
});

it("reflects the saved preference after initialization", () => {
  localStorage.setItem("ratefreak.theme", "dark");
  new Function(THEME_BOOTSTRAP)();
  render(<ThemeControl />);
  expect(screen.getByRole("button", { name: "Dark theme" })).toHaveAttribute("aria-pressed", "true");
});
