// @vitest-environment jsdom
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { focusManager, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { createQueryClient } from "@/shared/query/create-query-client";
import { CollectionSession } from "@/features/collections/components/collection-session";
import { getCurrentAccount } from "../api/accounts";
import type { AccountProfile } from "../interfaces/account-profile";
import { SessionProvider } from "./session-provider";
import { AuthNavigation } from "./auth-navigation";

vi.mock("../api/accounts", () => ({ getCurrentAccount: vi.fn(), logout: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => "/catalog/mine", useRouter: () => ({ replace: vi.fn() }) }));
const account: AccountProfile = { id: "owner", handle: "reader", displayName: "Reader", email: "reader@example.test", createdAt: "2026-11-01" };
const getAccount = vi.mocked(getCurrentAccount);
beforeEach(() => { vi.resetAllMocks(); getAccount.mockResolvedValue(account); });

it.each(["focus", "pageshow", "visibility"])("keeps navigation and private drafts visible and usable during a %s check", async event => {
  const client = createQueryClient();
  const view = render(<QueryClientProvider client={client}><SessionProvider>
    <AuthNavigation />
    <CollectionSession requireAccount returnTo="/catalog/mine">{() => <textarea aria-label="Item draft" defaultValue="Unsaved item" />}</CollectionSession>
  </SessionProvider></QueryClientProvider>);
  try {
    const draft = await screen.findByRole("textbox", { name: "Item draft" });
    await waitFor(() => expect(draft).toBeVisible());
    let resolve!: (value: AccountProfile | null) => void;
    getAccount.mockImplementation(() => new Promise(done => { resolve = done; }));
    act(() => {
      if (event === "visibility") { focusManager.setFocused(false); focusManager.setFocused(true); }
      else fireEvent(window, new Event(event));
    });
    await waitFor(() => expect(resolve).toBeTypeOf("function"));
    expect(screen.getByRole("link", { name: "Account" })).toBeVisible();
    expect(screen.queryByText(/Checking.*session/)).not.toBeInTheDocument();
    expect(draft).toBeVisible();
    expect(draft).toBeEnabled();
    expect(draft.closest("[hidden], [inert]")).toBeNull();
    fireEvent.change(draft, { target: { value: "Continued editing" } });
    await act(async () => { resolve(account); });
    expect(screen.getByRole("textbox", { name: "Item draft" })).toBe(draft);
    expect(draft).toHaveValue("Continued editing");
    getAccount.mockResolvedValue(null);
    fireEvent(window, new Event("focus"));
    await screen.findByRole("link", { name: "Log in to continue" });
    expect(screen.queryByRole("textbox", { name: "Item draft" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Log in" })).toBeVisible();
  } finally {
    view.unmount(); client.clear(); focusManager.setFocused(undefined);
  }
});
