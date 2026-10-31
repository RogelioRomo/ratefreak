// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { Providers } from "@/app/providers";
import { SessionProvider } from "@/features/auth/components/session-provider";
import { AppShell } from "./app-shell";
import { HomeIntro } from "@/features/home/components/home-intro";

vi.mock("next/navigation", () => ({ usePathname: () => "/", useRouter: () => ({ replace: vi.fn() }) }));
vi.mock("@/features/auth/api/accounts", () => ({ getCurrentAccount: vi.fn().mockResolvedValue(null), logout: vi.fn() }));

it("provides one main landmark, skip navigation and working Home navigation", () => {
  render(<Providers><SessionProvider><AppShell><HomeIntro /></AppShell></SessionProvider></Providers>);
  const main = screen.getByRole("main");
  expect(main).toHaveAttribute("id", "main-content");
  expect(screen.getByRole("link", { name: "Skip to content" })).toHaveAttribute("href", "#main-content");
  expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
  expect(within(main).getByRole("heading", { level: 1 })).toBeInTheDocument();
  expect(within(main).queryByRole("link")).not.toBeInTheDocument();
  expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
});
