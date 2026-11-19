// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { AvatarEditor } from "./avatar-editor";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
const state = vi.hoisted(() => ({ account: { id: "owner", handle: "owner", displayName: "Reader", avatarRevision: 0 }, error: null, isTransitioning: false, isLoading: false, refresh: vi.fn(async () => {}) }));
vi.mock("@/features/auth/hooks/use-session", () => ({ useSession: () => state }));
beforeEach(() => { state.isTransitioning=false;state.refresh.mockClear();vi.stubGlobal("fetch", vi.fn()); vi.stubGlobal("URL", class extends URL { static createObjectURL = vi.fn(() => "blob:preview"); static revokeObjectURL = vi.fn(); }); });
function setup() { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); client.setQueryData(sessionQueryKey, state.account); const view=render(<QueryClientProvider client={client}><AvatarEditor /></QueryClientProvider>); Object.assign(client,{avatarView:view}); return client; }
it("uploads multipart with CSRF and preserves unrelated profile state", async () => {
 const client = setup();
 vi.mocked(fetch).mockImplementation(async (url, options) => {
  if (String(url).endsWith("auth/csrf")) return Response.json({ requestToken: "token" });
  expect(options?.body).toBeInstanceOf(FormData); expect(new Headers(options?.headers).has("Content-Type")).toBe(false);
  expect(new Headers(options?.headers).get("X-CSRF-TOKEN")).toBe("token");
  expect(new Headers(options?.headers).get("X-RateFreak-Account-Id")).toBe("owner");
  expect((options?.body as FormData).get("expectedRevision")).toBe("0");
  return Response.json({ avatarUrl: "/api/v1/users/me/avatar/11111111-1111-1111-1111-111111111111", revision: 1 });
 });
 fireEvent.change(screen.getByLabelText("Choose avatar image"), { target: { files: [new File(["png"], "avatar.png", { type: "image/png" })] } });
 fireEvent.click(screen.getByRole("button", { name: "Save avatar" }));
 await screen.findByText("Avatar saved.");
 expect(client.getQueryData(sessionQueryKey)).toMatchObject({ displayName: "Reader", avatarRevision: 1 });
});
it("rejects oversized files before an upload", async () => {
 setup(); fireEvent.change(screen.getByLabelText("Choose avatar image"), { target: { files: [new File([new Uint8Array(5 * 1024 * 1024 + 1)], "large.png", { type: "image/png" })] } });
 expect(await screen.findByRole("alert")).toHaveTextContent("5 MiB"); expect(fetch).not.toHaveBeenCalled();
});
it("blocks retry after an unknown upload outcome and offers a fresh account check", async () => {
 setup(); vi.mocked(fetch).mockImplementation(async url => { if (String(url).endsWith("auth/csrf")) return Response.json({ requestToken: "token" }); throw new TypeError("offline"); });
 fireEvent.change(screen.getByLabelText("Choose avatar image"), { target: { files: [new File(["png"], "avatar.png", { type: "image/png" })] } });
 fireEvent.click(screen.getByRole("button", { name: "Save avatar" }));
 await screen.findByText(/couldn’t confirm/); await waitFor(() => expect(screen.getByRole("button", { name: "Save avatar" })).toBeDisabled());
 expect(screen.getByRole("button", { name: "Check current avatar" })).toBeEnabled();
});
it("allows another file choice after a definitive validation rejection", async () => {
 setup();vi.mocked(fetch).mockImplementation(async url => String(url).endsWith("auth/csrf") ? Response.json({requestToken:"token"}) : Response.json({title:"Invalid image"},{status:400}));
 fireEvent.change(screen.getByLabelText("Choose avatar image"),{target:{files:[new File(["bad"],"bad.png",{type:"image/png"})]}});
 fireEvent.click(screen.getByRole("button",{name:"Save avatar"}));await screen.findByRole("alert");
 await waitFor(()=>expect(screen.getByLabelText("Choose avatar image")).toBeEnabled());
 fireEvent.change(screen.getByLabelText("Choose avatar image"),{target:{files:[new File(["second"],"second.png",{type:"image/png"})]}});
 expect(screen.getByRole("button",{name:"Save avatar"})).toBeEnabled();
});
it("conceals and aborts a pending upload when the account transitions", async () => {
 const client=setup();let aborted=false;
 vi.mocked(fetch).mockImplementation(async(url,options)=>{if(String(url).endsWith("auth/csrf"))return Response.json({requestToken:"token"});return new Promise((_resolve,reject)=>options?.signal?.addEventListener("abort",()=>{aborted=true;reject(new DOMException("Aborted","AbortError"));}));});
 fireEvent.change(screen.getByLabelText("Choose avatar image"),{target:{files:[new File(["png"],"one.png",{type:"image/png"})]}});
 fireEvent.click(screen.getByRole("button",{name:"Save avatar"}));await waitFor(()=>expect(fetch).toHaveBeenCalledTimes(2));
 state.isTransitioning=true;client.setQueryData(sessionQueryKey,{...state.account,id:"next-account"});
 // Explicitly render the new identity: the old editor and request must be discarded.
 const view=(client as typeof client & {avatarView:ReturnType<typeof render>}).avatarView;
 view.rerender(<QueryClientProvider client={client}><AvatarEditor/></QueryClientProvider>);
 await waitFor(()=>expect(aborted).toBe(true));expect(screen.queryByAltText("Selected avatar preview")).toBeNull();state.isTransitioning=false;
});
