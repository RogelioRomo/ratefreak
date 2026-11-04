// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { ProfileEditor } from "./profile-editor";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
const state = vi.hoisted(() => ({ account: { id: "owner", handle: "owner", displayName: "Original", bio: null }, error: null, isTransitioning: false, isLoading: false, refresh: vi.fn() }));
vi.mock("@/features/auth/hooks/use-session", () => ({ useSession: () => state }));
beforeEach(() => { state.isTransitioning = false; vi.stubGlobal("fetch", vi.fn()); });
function setup() { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); client.setQueryData(sessionQueryKey, state.account); const element = <QueryClientProvider client={client}><ProfileEditor /></QueryClientProvider>; return { ...render(element), element, client }; }
it("retains draft through session uncertainty and associates server validation", async () => {
 const view = setup(); fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Draft" } });
 state.isTransitioning = true; view.rerender(view.element); expect(screen.getByLabelText("Display name")).toHaveValue("Draft");
 state.isTransitioning = false; view.rerender(<QueryClientProvider client={view.client}><ProfileEditor /></QueryClientProvider>);
 vi.mocked(fetch).mockImplementation(async url => String(url).endsWith("auth/csrf") ? Response.json({ requestToken: "csrf" }) : Response.json({ title: "Invalid", errors: { displayName: ["Invalid name"] } }, { status: 400 }));
 fireEvent.submit(screen.getByRole("form")); await screen.findByText("Invalid name"); expect(screen.getByLabelText("Display name")).toHaveValue("Draft"); expect(screen.getByLabelText("Display name")).toHaveAttribute("aria-invalid", "true"); await waitFor(() => expect(screen.getByRole("button", { name: "Save profile" })).toBeEnabled());
});
it("keeps the form mounted and updates cached account after a confirmed save", async () => {
 const view = setup(); view.client.setQueryData(["connections", "target", { direction: "followers", page: 1 }], { items: [{ displayName: "Original" }] }); vi.mocked(fetch).mockImplementation(async url => String(url).endsWith("auth/csrf") ? Response.json({ requestToken: "csrf" }) : Response.json({ ...state.account, displayName: "Saved" }));
 fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Saved" } }); fireEvent.submit(screen.getByRole("form")); await screen.findByText("Profile saved."); expect(view.client.getQueryData(sessionQueryKey)).toMatchObject({ displayName: "Saved" }); expect(view.client.getQueryState(["connections", "target", { direction: "followers", page: 1 }])?.isInvalidated).toBe(true);
});
it("blocks another write after an unknown outcome while retaining the draft", async () => {
 setup(); vi.mocked(fetch).mockImplementation(async url => { if (String(url).endsWith("auth/csrf")) return Response.json({ requestToken: "csrf" }); throw new TypeError("offline"); });
 fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Uncertain draft" } }); fireEvent.submit(screen.getByRole("form")); await screen.findByText("Check your account by reloading before saving again."); expect(screen.getByRole("button", { name: "Save profile" })).toBeDisabled(); expect(screen.getByLabelText("Display name")).toHaveValue("Uncertain draft");
});
