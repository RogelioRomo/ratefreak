// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { SessionProvider } from "./session-provider";
import { LoginScreen } from "./login-screen";
import { RegisterScreen } from "./register-screen";
import { AccountScreen } from "./account-screen";
const navigation = vi.hoisted(() => ({ replace: vi.fn(), assign: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => navigation }));
vi.mock("../api/external-navigation", () => ({ navigateToProvider: navigation.assign }));
let signedIn = false; let discoveryFails = false; let enabled = ["google"]; let linked = ["github"];
let starts = 0; let finishStart: ((value: Response) => void) | undefined;
const account = { id: "a", handle: "reader", displayName: "Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
beforeEach(() => {
  resetCsrfState(); signedIn = false; discoveryFails = false; enabled = ["google"]; linked = ["github"]; starts = 0; finishStart = undefined; navigation.replace.mockReset();
  vi.stubGlobal("fetch", vi.fn(async (url) => {
    const path = String(url);
    if (path.endsWith("auth/csrf")) return Response.json({ requestToken: "csrf" });
    if (path.endsWith("users/me")) return signedIn ? Response.json(account) : new Response(null, { status: 401 });
    if (path.endsWith("auth/providers")) return discoveryFails ? new Response(null, { status: 503 }) : Response.json(["google","github"].map(name => ({ name, enabled: enabled.includes(name) })));
    if (path.endsWith("users/me/logins")) return Response.json({ hasPassword: true, providers: linked });
    if (path.endsWith("auth/recovery-status")) return Response.json({ hasPassword: true, hasRecoveryCode: true });
    if (path.endsWith("/start")) { starts++; return new Promise<Response>(resolve => { finishStart = resolve; }); }
    throw new Error(`Unexpected test URL ${path}`);
  }));
});
function mount(children: ReactNode) {
  const client = createQueryClient();
  render(<QueryClientProvider client={client}><SessionProvider>{children}</SessionProvider></QueryClientProvider>);
  return client;
}
it.each(["google", "github"])("shows independently enabled %s sign-in alongside password controls", async provider => {
  enabled = [provider]; mount(<LoginScreen />);
  expect(await screen.findByRole("button", { name: `Continue with ${provider === "google" ? "Google" : "GitHub"}` })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: `Continue with ${provider === "google" ? "GitHub" : "Google"}` })).not.toBeInTheDocument();
  expect(screen.getByLabelText("Password")).toBeInTheDocument();
});
it("offers the same providers on account creation", async () => {
  mount(<RegisterScreen />); expect(await screen.findByRole("button", { name: "Continue with Google" })).toBeInTheDocument();
  expect(screen.getByLabelText("Display name")).toBeInTheDocument();
});
it("discovery failure keeps password login usable and permits retry", async () => {
  discoveryFails = true; mount(<LoginScreen />);
  expect(await screen.findByRole("button", { name: "Retry sign-in options" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Log in" })).toBeEnabled();
  discoveryFails = false; fireEvent.click(screen.getByRole("button", { name: "Retry sign-in options" }));
  expect(await screen.findByRole("button", { name: "Continue with Google" })).toBeInTheDocument();
});
it("starts once, disables duplicate clicks and never stores the authorization request in mutation caches", async () => {
  const client = mount(<LoginScreen />); const button = await screen.findByRole("button", { name: "Continue with Google" });
  fireEvent.click(button); fireEvent.click(button); await waitFor(() => expect(starts).toBe(1));
  expect(button).toBeDisabled(); expect(screen.getByRole("button", { name: "Log in" })).toBeDisabled();
  expect(client.getMutationCache().getAll()).toHaveLength(0);
  finishStart!(new Response(null, { status: 503 }));
  expect(await screen.findByRole("alert")).toHaveTextContent(/couldn’t start/);
  expect(button).toBeEnabled(); expect(screen.getByRole("button", { name: "Log in" })).toBeEnabled();
});
it("account settings distinguish linked and available providers", async () => {
  signedIn = true; enabled = ["google", "github"]; mount(<AccountScreen />);
  fireEvent.click(await screen.findByRole("tab", { name: "Sign-in options" }));
  expect(await screen.findByText("GitHub is linked")).toBeInTheDocument();
  expect(await screen.findByRole("button", { name: "Link Google" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Link GitHub" })).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /unlink/i })).not.toBeInTheDocument();
});
it("keeps a failed linking action visible in account settings", async () => {
  signedIn = true; mount(<AccountScreen />);
  fireEvent.click(await screen.findByRole("tab", { name: "Sign-in options" }));
  fireEvent.click(await screen.findByRole("button", { name: "Link Google" }));
  await waitFor(() => expect(starts).toBe(1)); finishStart!(new Response(null, { status: 503 }));
  expect(await screen.findByRole("alert")).toHaveTextContent(/couldn’t start/);
});
it("makes provider controls usable after browser Back restores the page", async () => {
  mount(<LoginScreen />); fireEvent.click(await screen.findByRole("button", { name: "Continue with Google" }));
  await waitFor(() => expect(starts).toBe(1));
  finishStart!(Response.json({ authorizationUrl: "https://accounts.google.com/o/oauth2/v2/auth?client_id=controlled" }));
  const connecting = await screen.findByRole("button", { name: "Connecting to Google…" });
  await waitFor(() => expect(screen.getByRole("button", { name: "Log in" })).toBeEnabled());
  expect(connecting).toBeDisabled();
  const restored = new Event("pageshow"); Object.defineProperty(restored, "persisted", { value: true });
  fireEvent(window, restored);
  const retry = await screen.findByRole("button", { name: "Continue with Google" }); expect(retry).toBeEnabled();
  fireEvent.click(retry); await waitFor(() => expect(starts).toBe(2));
  finishStart!(new Response(null, { status: 503 })); await screen.findByRole("alert");
});
