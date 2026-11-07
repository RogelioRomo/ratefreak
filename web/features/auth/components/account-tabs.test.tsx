// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { SessionProvider } from "./session-provider";
import { AccountScreen } from "./account-screen";
const navigation = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => navigation }));
const account = { id: "a", handle: "reader", displayName: "Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
const code = "12345678-90ABCDEF-12345678-90ABCDEF";
let signedIn = true; let sessionStatus = 200;
beforeEach(() => {
  resetCsrfState(); signedIn = true; sessionStatus = 200; navigation.replace.mockReset();
  vi.stubGlobal("fetch", vi.fn(async url => {
    const path = String(url);
    if (path.endsWith("users/me") && sessionStatus !== 200) return new Response(null, { status: sessionStatus });
    if (path.endsWith("users/me")) return signedIn ? Response.json(account) : new Response(null, { status: 401 });
    if (path.endsWith("auth/csrf")) return Response.json({ requestToken: "token" });
    if (path.endsWith("auth/recovery-status")) return Response.json({ hasPassword: true, hasRecoveryCode: true });
    if (path.endsWith("auth/recovery-code")) return Response.json({ recoveryCode: code });
    if (path.endsWith("users/me/logins")) return Response.json({ hasPassword: true, providers: ["github"] });
    if (path.endsWith("auth/providers")) return Response.json([{ name: "google", enabled: true }, { name: "github", enabled: true }]);
    throw new Error(`Unexpected URL ${path}`);
  }));
});
function mount() { const client = createQueryClient(); render(<QueryClientProvider client={client}><SessionProvider><AccountScreen /></SessionProvider></QueryClientProvider>); return client; }
it("shows an overview and exposes only the selected section", async () => {
  mount(); const overview = await screen.findByRole("tab", { name: "Overview" });
  expect(overview).toHaveAttribute("aria-selected", "true"); expect(screen.getByText(account.email)).toBeVisible();
  expect(screen.getAllByRole("tabpanel")).toHaveLength(1); expect(screen.queryByRole("form", { name: "Edit public profile" })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("tab", { name: "Profile" }));
  expect(screen.getByRole("form", { name: "Edit public profile" })).toBeVisible(); expect(screen.getByText(account.email)).not.toBeVisible();
  expect(screen.getAllByRole("tabpanel")).toHaveLength(1); expect(screen.getByRole("button", { name: "Log out" })).toBeVisible();
});
it("supports vertical arrow navigation, Home, End and one tab stop", async () => {
  mount(); const overview = await screen.findByRole("tab", { name: "Overview" }); overview.focus();
  fireEvent.keyDown(overview, { key: "ArrowDown" }); const profile = screen.getByRole("tab", { name: "Profile" });
  expect(profile).toHaveFocus(); expect(profile).toHaveAttribute("aria-selected", "true"); expect(overview).toHaveAttribute("tabindex", "-1");
  fireEvent.keyDown(profile, { key: "End" }); const recovery = screen.getByRole("tab", { name: "Password recovery" }); expect(recovery).toHaveFocus();
  fireEvent.keyDown(recovery, { key: "ArrowDown" }); expect(overview).toHaveFocus();
  fireEvent.keyDown(overview, { key: "ArrowUp" }); expect(recovery).toHaveFocus();
  fireEvent.keyDown(recovery, { key: "Home" }); expect(overview).toHaveFocus();
});
it("preserves an unsaved profile while switching sections", async () => {
  mount(); fireEvent.click(await screen.findByRole("tab", { name: "Profile" }));
  fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "My draft name" } });
  fireEvent.change(screen.getByLabelText("Bio"), { target: { value: "My unsaved bio" } });
  fireEvent.click(screen.getByRole("tab", { name: "Sign-in options" }));
  expect(await screen.findByRole("button", { name: "Link Google" })).toBeVisible();
  fireEvent.click(screen.getByRole("tab", { name: "Profile" }));
  expect(screen.getByLabelText("Display name")).toHaveValue("My draft name"); expect(screen.getByLabelText("Bio")).toHaveValue("My unsaved bio");
});
it("preserves a newly issued recovery code until acknowledgement across tabs", async () => {
  const client = mount(); fireEvent.click(await screen.findByRole("tab", { name: "Password recovery" }));
  fireEvent.change(await screen.findByLabelText("Current password"), { target: { value: "Original-password-72" } });
  fireEvent.submit(screen.getByRole("form", { name: "Recovery code settings" }));
  await waitFor(() => expect(screen.getByText(code)).toBeVisible());
  fireEvent.click(screen.getByRole("tab", { name: "Overview" })); expect(screen.getByText(code)).not.toBeVisible();
  fireEvent.click(screen.getByRole("tab", { name: "Password recovery" })); expect(screen.getByText(code)).toBeVisible();
  expect(screen.getByRole("button", { name: "Done" })).toBeDisabled();
  expect(JSON.stringify(client.getMutationCache().getAll().map(m => m.state))).not.toMatch(/12345678|Original-password/);
  fireEvent.click(screen.getByRole("checkbox", { name: "I saved my recovery code" })); fireEvent.click(screen.getByRole("button", { name: "Done" }));
  expect(screen.queryByText(code)).not.toBeInTheDocument();
});
it("does not expose settings or navigation to anonymous visitors", async () => {
  signedIn = false; mount(); await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
  expect(screen.queryByRole("tab")).not.toBeInTheDocument(); expect(screen.queryByText(account.email)).not.toBeInTheDocument();
});

it("conceals the active private panel on session failure and restores its draft after retry", async () => {
  mount(); fireEvent.click(await screen.findByRole("tab", { name: "Profile" }));
  fireEvent.change(screen.getByLabelText("Bio"), { target: { value: "Keep my draft" } });
  sessionStatus = 503; fireEvent(window, new Event("focus"));
  expect(await screen.findByRole("alert")).toHaveTextContent("We couldn’t load your account");
  expect(screen.queryByRole("tabpanel", { name: "Profile" })).not.toBeInTheDocument();
  sessionStatus = 200; fireEvent.click(screen.getByRole("button", { name: "Retry" }));
  await waitFor(() => expect(screen.getByLabelText("Bio")).toBeVisible()); expect(screen.getByLabelText("Bio")).toHaveValue("Keep my draft");
});
