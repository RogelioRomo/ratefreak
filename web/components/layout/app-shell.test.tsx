// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { AppShell } from "./app-shell";
import { HomeIntro } from "@/features/home/components/home-intro";

it("provides one main landmark, skip navigation and working Home navigation", () => {
  render(<AppShell><HomeIntro /></AppShell>);
  const main = screen.getByRole("main");
  expect(main).toHaveAttribute("id", "main-content");
  expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#main-content");
  expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
  expect(within(main).getByRole("heading", { level: 1 })).toBeInTheDocument();
  expect(within(main).queryByRole("link")).not.toBeInTheDocument();
  expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
});
