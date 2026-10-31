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
