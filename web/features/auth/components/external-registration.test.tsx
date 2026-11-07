// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { createQueryClient } from "@/shared/query/create-query-client";
import { resetCsrfState } from "@/shared/api/csrf";
import { SessionProvider } from "./session-provider";
import { ExternalRegistrationScreen } from "./external-registration-screen";
const navigation = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => navigation }));
let pending = true; let registerStatus = 201; let signedIn = false;
const account = { id: "a", handle: "new_reader", displayName: "Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
beforeEach(() => {
  resetCsrfState(); pending = true; registerStatus = 201; signedIn = false; navigation.replace.mockReset();
  vi.stubGlobal("fetch", vi.fn(async (url) => {
    const path = String(url);
    if (path.endsWith("auth/csrf")) return Response.json({ requestToken: "token" });
    if (path.endsWith("users/me")) return signedIn ? Response.json(account) : new Response(null, { status: 401 });
    if (path.endsWith("auth/external/pending")) return pending ? Response.json({ requiresRegistration: true, provider: "github" }) : new Response(null, { status: 401 });
    if (path.endsWith("auth/external/register")) {
      if (registerStatus === 201) { signedIn = true; return Response.json(account, { status: 201 }); }
      return Response.json({ title: "Unavailable", errors: { handle: ["Handle is unavailable."] } }, { status: registerStatus });
    }
    throw new Error(`Unexpected URL ${path}`);
  }));
});
function mount() {
  const client = createQueryClient(); render(<QueryClientProvider client={client}><SessionProvider><ExternalRegistrationScreen /></SessionProvider></QueryClientProvider>); return client;
}
async function submit() {
  fireEvent.change(await screen.findByLabelText("Handle"), { target: { value: "new_reader" } });
  fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Reader" } });
  fireEvent.submit(screen.getByRole("form", { name: "Finish creating account" }));
}
it("collects only profile fields and establishes the normal session", async () => {
  const client = mount(); await submit(); await waitFor(() => expect(navigation.replace).toHaveBeenCalledWith("/"));
  expect(signedIn).toBe(true); expect(client.getQueryData(["session"])).toMatchObject({ id: "a" });
  expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
});
it("offers restart when onboarding expired", async () => {
  pending = false; mount(); expect(await screen.findByText(/sign-in has expired/i)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Back to login" })).toHaveAttribute("href", "/login"); expect(screen.queryByLabelText("Handle")).not.toBeInTheDocument();
});
it("keeps the form for handle conflicts and allows retry", async () => {
  registerStatus = 409; mount(); await submit(); expect(await screen.findByRole("alert")).toHaveTextContent(/handle/i);
  expect(screen.getByLabelText("Handle")).toHaveValue("new_reader"); registerStatus = 201;
  fireEvent.change(screen.getByLabelText("Handle"), { target: { value: "another_reader" } });
  fireEvent.submit(screen.getByRole("form", { name: "Finish creating account" })); await waitFor(() => expect(signedIn).toBe(true));
});
it("replaces the form with restart guidance if the ticket expires during submission", async () => {
  registerStatus = 401; mount(); await submit(); expect(await screen.findByText(/sign-in has expired/i)).toBeInTheDocument();
  expect(screen.queryByLabelText("Handle")).not.toBeInTheDocument();
});
