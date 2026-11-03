// @vitest-environment jsdom
import Link from "next/link";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { MobileNavigation } from "./mobile-navigation";

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute("open", ""); };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute("open"); this.dispatchEvent(new Event("close")); };
  vi.stubGlobal("matchMedia", () => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
});

it("opens the navigation and restores scrolling and trigger focus after closing", () => {
  render(<MobileNavigation><Link href="/catalog">Catalog</Link></MobileNavigation>);
  const trigger = screen.getByRole("button", { name: "Open menu" });
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  fireEvent.click(trigger);
  const dialog = screen.getByRole("dialog", { name: "Navigation menu" });
  expect(within(dialog).getByRole("link", { name: "Catalog" })).toBeVisible();
  expect(trigger).toHaveAttribute("aria-expanded", "true");
  expect(document.body.style.overflow).toBe("hidden");
  fireEvent.click(within(dialog).getByRole("button", { name: "Close menu" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(document.body.style.overflow).toBe("");
  expect(trigger).toHaveFocus();
});

it("closes on link selection and native Escape cancellation", () => {
  render(<MobileNavigation><a href="#catalog">Catalog</a></MobileNavigation>);
  const trigger = screen.getByRole("button", { name: "Open menu" });
  fireEvent.click(trigger);
  fireEvent.click(screen.getByRole("link", { name: "Catalog" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  fireEvent.click(trigger);
  fireEvent(screen.getByRole("dialog"), new Event("cancel", { cancelable: true }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(document.body.style.overflow).toBe("");
});
