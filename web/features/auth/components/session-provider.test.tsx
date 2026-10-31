// @vitest-environment jsdom
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { BroadcastChannel } from "node:worker_threads";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { createQueryClient } from "@/shared/query/create-query-client";
import { ApiError } from "@/shared/api/api-error";
import { SessionProvider } from "./session-provider";
import { useSession } from "../hooks/use-session";
import { useLogin } from "../hooks/use-login";
import { useRegister } from "../hooks/use-register";

vi.mock("../api/accounts", () => ({ getCurrentAccount: vi.fn(), login: vi.fn(), register: vi.fn(), logout: vi.fn() }));
import { getCurrentAccount, login, register } from "../api/accounts";

const account = { id: "a", handle: "reader", displayName: "A Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };
const getAccount = vi.mocked(getCurrentAccount);
beforeEach(() => { vi.resetAllMocks(); getAccount.mockResolvedValue(null); });
function harness() {
  const client = createQueryClient();
  function Wrapper({ children }: ChildrenProps) {
    return <QueryClientProvider client={client}><SessionProvider>{children}</SessionProvider></QueryClientProvider>;
  }
  return { client, wrapper: Wrapper };
}

it("resolves anonymous sessions and keeps transport failures explicit", async () => {
  const { client, wrapper } = harness();
  const hook = renderHook(useSession, { wrapper });
  await waitFor(() => expect(hook.result.current.account).toBeNull());
  getAccount.mockRejectedValue(new ApiError(503));
  await act(() => hook.result.current.refresh());
  await waitFor(() => expect(hook.result.current.error).toMatchObject({ status: 503 }));
  expect(hook.result.current.account).toBeUndefined();
  hook.unmount(); client.clear();
});

it("logs in without retaining submitted passwords in mutation state", async () => {
  const { client, wrapper } = harness();
  vi.mocked(login).mockResolvedValue(account);
  const hook = renderHook(() => ({ session: useSession(), mutation: useLogin() }), { wrapper });
  await waitFor(() => expect(hook.result.current.session.account).toBeNull());
  getAccount.mockResolvedValue(account);
  await act(() => hook.result.current.mutation.submit({ email: account.email, password: "Secret-example-72" }));
  await waitFor(() => expect(hook.result.current.session.account?.id).toBe("a"));
  expect(JSON.stringify(client.getMutationCache().getAll().map((entry) => entry.state))).not.toContain("Secret-example-72");
  hook.unmount(); client.clear();
});

it("registration stays anonymous and does not retain credentials", async () => {
  const { client, wrapper } = harness();
  vi.mocked(register).mockResolvedValue(account);
  const hook = renderHook(() => ({ session: useSession(), mutation: useRegister() }), { wrapper });
  await waitFor(() => expect(hook.result.current.session.account).toBeNull());
  await act(() => hook.result.current.mutation.submit({ email: account.email, password: "Secret-example-72", handle: account.handle, displayName: account.displayName }));
  expect(hook.result.current.session.account).toBeNull();
  expect(JSON.stringify(client.getMutationCache().getAll().map((entry) => entry.state))).not.toContain("Secret-example-72");
  hook.unmount(); client.clear();
});

it("serializes conflicting actions and clears private data when a session expires", async () => {
  const { client, wrapper } = harness();
  getAccount.mockResolvedValue(account);
  const hook = renderHook(useSession, { wrapper });
  await waitFor(() => expect(hook.result.current.account?.id).toBe("a"));
  client.setQueryData(["private", "a"], "private data");
  let finish!: () => void;
  let first!: Promise<void>;
  act(() => { first = hook.result.current.runExclusive(() => new Promise<void>((done) => { finish = done; })); });
  await expect(hook.result.current.runExclusive(async () => undefined)).rejects.toThrow("An account action is already in progress.");
  await act(async () => { finish(); await first; });
  getAccount.mockResolvedValue(null);
  await act(() => hook.result.current.refresh());
  await waitFor(() => expect(hook.result.current.account).toBeNull());
  expect(client.getQueryData(["private", "a"])).toBeUndefined();
  hook.unmount(); client.clear();
});

it("detects expiry in an active tab without a manual refresh", async () => {
  vi.useFakeTimers();
  const { client, wrapper } = harness();
  getAccount.mockResolvedValue(account);
  const hook = renderHook(useSession, { wrapper });
  try {
    await act(async () => { await vi.advanceTimersByTimeAsync(10); });
    expect(hook.result.current.account?.id).toBe("a");
    client.setQueryData(["private", "a"], "private data");
    getAccount.mockResolvedValue(null);
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });
    expect(hook.result.current.account).toBeNull();
    expect(client.getQueryData(["private", "a"])).toBeUndefined();
  } finally { hook.unmount(); client.clear(); vi.useRealTimers(); }
});

it("propagates identity changes to a second browser client without credentials", async () => {
  vi.stubGlobal("BroadcastChannel", BroadcastChannel);
  const first = harness(); const second = harness();
  getAccount.mockResolvedValue(account);
  const left = renderHook(useSession, { wrapper: first.wrapper });
  const right = renderHook(useSession, { wrapper: second.wrapper });
  try {
    await waitFor(() => expect(left.result.current.account?.id).toBe("a"));
    await waitFor(() => expect(right.result.current.account?.id).toBe("a"));
    second.client.setQueryData(["private", "a"], "private data");
    const replacement = { ...account, id: "b", handle: "other" };
    getAccount.mockResolvedValue(replacement);
    await act(() => left.result.current.changeIdentity(async () => replacement));
    await waitFor(() => expect(right.result.current.account?.id).toBe("b"));
    expect(second.client.getQueryData(["private", "a"])).toBeUndefined();
    getAccount.mockResolvedValue(null);
    await act(() => left.result.current.changeIdentity(async () => null));
    await waitFor(() => expect(right.result.current.account).toBeNull());
  } finally { left.unmount(); right.unmount(); first.client.clear(); second.client.clear(); }
});
