import { expect, it } from "vitest";
import { createQueryClient } from "@/shared/query/create-query-client";
import { applyIdentity, reconcileIdentity } from "./transition";

const account = { id: "a", handle: "reader", displayName: "A Reader", email: "reader@example.test", createdAt: "2026-10-30T00:00:00Z" };

it("cancels old requests, clears cached private data and seeds the new session", async () => {
  const client = createQueryClient();
  let signal!: AbortSignal;
  client.setQueryData(["private", "a"], "private data");
  const old = client.fetchQuery({ queryKey: ["late", "a"], queryFn: ({ signal: current }) => {
    signal = current;
    return new Promise(() => {});
  } }).catch(() => undefined);
  await applyIdentity(client, account);
  expect(signal.aborted).toBe(true);
  expect(client.getQueryData(["private", "a"])).toBeUndefined();
  expect(client.getQueryData(["session"])).toEqual(account);
  await old;
  await applyIdentity(client, null);
  expect(client.getQueryData(["session"])).toBeNull();
  client.clear();
});

it("clears private data on expiry or account replacement without cancelling its own session fetch", async () => {
  const client = createQueryClient();
  client.setQueryData(["session"], account);
  client.setQueryData(["private", "a"], "private data");
  await reconcileIdentity(client, null);
  expect(client.getQueryData(["private", "a"])).toBeUndefined();
  expect(client.getQueryData(["session"])).toEqual(account);
  client.clear();
});

it("keeps public requests alive during initial session discovery while clearing private state", async () => {
  const client = createQueryClient();
  let publicSignal!: AbortSignal;
  let complete!: (value: string) => void;
  const pending = client.fetchQuery({ queryKey: ["catalog", "detail", "title"], queryFn: ({ signal }) => {
    publicSignal = signal;
    return new Promise<string>(resolve => { complete = resolve; });
  } }).catch(() => "cancelled");
  client.setQueryData(["personal", "old", "review"], "private text");
  client.setQueryData(["collections", "anonymous", "detail", "id"], "viewer-sensitive data");
  await reconcileIdentity(client, account);
  expect(client.getQueryData(["collections", "anonymous", "detail", "id"])).toBeUndefined();
  expect(client.getQueryData(["personal", "old", "review"])).toBeUndefined();
  expect(publicSignal.aborted).toBe(false);
  complete("public title");
  expect(await pending).toBe("public title");
  client.clear();
});
