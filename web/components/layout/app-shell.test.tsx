// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { Providers } from "@/app/providers";
import { SessionProvider } from "@/features/auth/components/session-provider";
import { AppShell } from "./app-shell";
import { HomeIntro } from "@/features/home/components/home-intro";

const route = vi.hoisted(() => ({ pathname: "/" }));
vi.mock("next/navigation", () => ({ usePathname: () => route.pathname, useRouter: () => ({ replace: vi.fn() }) }));
vi.mock("@/features/auth/api/accounts", () => ({ getCurrentAccount: vi.fn().mockResolvedValue(null), logout: vi.fn() }));

it("provides one main landmark, skip navigation and working Home navigation", () => {
  const tree = <Providers><SessionProvider><AppShell><HomeIntro /></AppShell></SessionProvider></Providers>;
  const { rerender } = render(tree);
  const main = screen.getByRole("main");
  expect(main).toHaveAttribute("id", "main-content");
  expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#main-content");
  expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
  expect(within(main).getByRole("heading", { level: 1 })).toBeInTheDocument();
  expect(within(main).getByRole("link", { name: "Create your account" })).toHaveAttribute("href", "/register");
  expect(within(main).getByRole("heading", { name: "Good taste deserves a home.", level: 1 })).toBeInTheDocument();
  expect(within(main).getByText("A place for your interests")).toBeInTheDocument();
  expect(within(main).getByRole("link", { name: /browse the catalog/i })).toHaveAttribute("href", "/catalog");
  expect(screen.getByRole("navigation", { name: "Main navigation" })).toHaveTextContent("Catalog");
  expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
  expect(screen.getByRole("group", { name: "Theme" })).toBeInTheDocument();
  // The same theme control remains available when the active route changes.
  route.pathname = "/register";
  rerender(<Providers><SessionProvider><AppShell><HomeIntro /></AppShell></SessionProvider></Providers>);
  expect(document.querySelector("[data-homepage]")).toBeInTheDocument();
  expect(screen.getByRole("group", { name: "Theme" })).toBeInTheDocument();
  route.pathname = "/";
});
