// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { QueryClientProvider } from "@tanstack/react-query";
import { createQueryClient } from "@/shared/query/create-query-client";
import { SessionProvider } from "./session-provider";
import { AccountDataSettings } from "./account-data-settings";
import { AccountScreen } from "./account-screen";
import { resetCsrfState } from "@/shared/api/csrf";
const navigation = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => navigation }));
const account = { id: "a", handle: "reader", displayName: "Reader", email: "r@test.example", createdAt: "2026-01-01" };
let confirmed: string | null = null;
let signedIn = true;
let password = true;
let exportFails = false;
let lostDelete = false;
let deleteFails = false;
const requests: Array<{
    path: string;
    body: unknown;
}> = [];
beforeEach(() => {
    resetCsrfState();
    confirmed = null;
    signedIn = true;
    password = true;
    exportFails = false;
    lostDelete = false;
    deleteFails = false;
    requests.length = 0;
    navigation.replace.mockReset();
    window.history.replaceState(null, "", "/account");
    vi.stubGlobal("fetch", vi.fn(async (url, options) => {
        const path = String(url);
        requests.push({ path, body: options?.body });
        if (path.endsWith("users/me"))
            return signedIn ? Response.json(account) : new Response(null, { status: 401 });
        if (path.endsWith("auth/csrf"))
            return Response.json({ requestToken: "csrf" });
        if (path.endsWith("auth/recovery-status")) return Response.json({ hasPassword: true, hasRecoveryCode: true });
        if (path.endsWith("users/me/logins")) return Response.json({ hasPassword: true, providers: ["github"] });
        if (path.endsWith("auth/providers")) return Response.json([{ name: "github", enabled: true }]);
        if (path.endsWith("users/me/data"))
            return Response.json({ hasPassword: password, providers: ["github"], confirmedOperation: confirmed, recoveryEventDays: 90, resolvedModerationDays: 365, backupDays: 30 });
        if (path.endsWith("/confirm")) {
            confirmed = JSON.parse(String(options?.body)).operation;
            return new Response(null, { status: 204 });
        }
        if (path.endsWith("/export")) {
            confirmed = null;
            return exportFails ? Response.json({ title: "Archive too large" }, { status: 413 }) : new Response("PK", { headers: { "Content-Type": "application/zip", "Content-Disposition": 'attachment; filename="data.zip"' } });
        }
        if (path.endsWith("/delete")) {
            if (deleteFails) return Response.json({ title: "Appoint another administrator first." }, { status: 409 });
            signedIn = false;
            if (lostDelete)
                throw new TypeError("Lost response");
            return new Response(null, { status: 204 });
        }
        throw new Error(`Unexpected ${path}`);
    }));
    Object.defineProperty(URL, "createObjectURL", { value: vi.fn(() => "blob:export"), configurable: true });
    Object.defineProperty(URL, "revokeObjectURL", { value: vi.fn(), configurable: true });
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => { });
});
function mount() { const client = createQueryClient(); render(<QueryClientProvider client={client}><SessionProvider><AccountDataSettings active/></SessionProvider></QueryClientProvider>); return client; }
async function confirm(operation: "export" | "delete") { fireEvent.click(await screen.findByRole("button", { name: operation === "export" ? "Export my data" : "Delete account" })); fireEvent.change(within(screen.getByRole("form", { name: "Confirm account ownership" })).getByLabelText("Current password"), { target: { value: "Secret-password-7" } }); fireEvent.submit(screen.getByRole("form", { name: "Confirm account ownership" })); }
it("downloads a confirmed export once, clears the credential and revokes the temporary URL", async () => { const client = mount(); await confirm("export"); await waitFor(() => expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:export")); expect(requests.filter(x => x.path.endsWith("/export"))).toHaveLength(1); expect(JSON.stringify(client.getQueryCache().getAll().map(x => x.state))).not.toContain("Secret-password"); expect(screen.queryByLabelText("Current password")).not.toBeInTheDocument(); });
it("requires final confirmation, then clears private caches and redirects", async () => { const client = mount(); await confirm("delete"); await screen.findByRole("dialog"); expect(requests.some(x => x.path.endsWith("/delete"))).toBe(false); client.setQueryData(["private", "a", "draft"], "private data"); fireEvent.change(screen.getByLabelText("Type your handle reader"), { target: { value: "reader" } }); fireEvent.click(screen.getByRole("button", { name: "Permanently delete account" })); await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/account-deleted")); expect(client.getQueryData(["private", "a", "draft"])).toBeUndefined(); });
it("a provider callback marker alone cannot open the deletion dialog or perform an action", async () => { password = false; window.history.replaceState(null, "", "/account?dataAction=delete"); mount(); await screen.findByRole("button", { name: "Delete account" }); expect(screen.queryByRole("dialog")).not.toBeInTheDocument(); expect(requests.some(x => x.path.endsWith("/delete"))).toBe(false); fireEvent.click(screen.getByRole("button", { name: "Delete account" })); expect(screen.getByRole("button", { name: "Confirm with GitHub" })).toBeVisible(); });
it("export failure allows a deliberate retry and never deletes", async () => { exportFails = true; mount(); await confirm("export"); await screen.findByRole("alert"); expect(requests.filter(x => x.path.endsWith("/export"))).toHaveLength(1); expect(requests.some(x => x.path.endsWith("/delete"))).toBe(false); });
it("checks the session after a lost deletion response without resubmitting", async () => { lostDelete = true; mount(); await confirm("delete"); await screen.findByRole("dialog"); fireEvent.change(screen.getByLabelText("Type your handle reader"), { target: { value: "reader" } }); fireEvent.click(screen.getByRole("button", { name: "Permanently delete account" })); await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/account-deleted")); expect(requests.filter(x => x.path.endsWith("/delete"))).toHaveLength(1); });
it("requires a separate explicit review after verified provider reauthentication", async () => {
  password = false;
  confirmed = "delete";
  window.history.replaceState(null, "", "/account?dataAction=delete");
  mount();
  await screen.findByRole("button", { name: "Review deletion" });
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(requests.some(request => request.path.endsWith("/delete"))).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Review deletion" }));
  expect(screen.getByRole("dialog")).toBeVisible();
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Delete account" })).toHaveFocus();
});
it("export from the final dialog requires its own ownership grant and never submits deletion", async () => {
  mount();
  await confirm("delete");
  await screen.findByRole("dialog");
  fireEvent.click(screen.getByRole("button", { name: "Export my data first" }));
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(screen.getByRole("form", { name: "Confirm account ownership" })).toBeVisible();
  expect(requests.some(request => request.path.endsWith("/export") || request.path.endsWith("/delete"))).toBe(false);
});

it.each([false, true])("keeps the real account screen on the deletion confirmation after identity clearing (lost response: %s)", async lost => {
  lostDelete = lost;
  const client = createQueryClient();
  render(<QueryClientProvider client={client}><SessionProvider><AccountScreen /></SessionProvider></QueryClientProvider>);
  fireEvent.click(await screen.findByRole("tab", { name: "Data and privacy" }));
  await confirm("delete");
  await screen.findByRole("dialog");
  client.setQueryData(["private", "a", "draft"], "private data");
  fireEvent.change(screen.getByLabelText("Type your handle reader"), { target: { value: "reader" } });
  fireEvent.click(screen.getByRole("button", { name: "Permanently delete account" }));
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/account-deleted"));
  expect(navigation.replace).not.toHaveBeenCalledWith("/login");
  expect(client.getQueryData(["private", "a", "draft"])).toBeUndefined();
  expect(requests.filter(request => request.path.endsWith("/delete"))).toHaveLength(1);
});

it("restores ordinary session gating after a definite deletion failure", async () => {
  deleteFails = true;
  const client = createQueryClient();
  render(<QueryClientProvider client={client}><SessionProvider><AccountScreen /></SessionProvider></QueryClientProvider>);
  fireEvent.click(await screen.findByRole("tab", { name: "Data and privacy" }));
  await confirm("delete");
  await screen.findByRole("dialog");
  client.setQueryData(["private", "a", "draft"], "keep this draft");
  fireEvent.change(screen.getByLabelText("Type your handle reader"), { target: { value: "reader" } });
  fireEvent.click(screen.getByRole("button", { name: "Permanently delete account" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Appoint another administrator first.");
  expect(client.getQueryData(["private", "a", "draft"])).toBe("keep this draft");
  expect(navigation.replace).not.toHaveBeenCalled();
  signedIn = false;
  fireEvent(window, new Event("focus"));
  await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/login"));
  expect(navigation.replace).not.toHaveBeenCalledWith("/account-deleted");
});
