import type { Query, QueryClient } from "@tanstack/react-query";
import { resetCsrfState } from "@/shared/api/csrf";
import type { AccountProfile } from "../interfaces/account-profile";
import { sessionQueryKey } from "./query-keys";

export async function applyIdentity(client: QueryClient, account: AccountProfile | null): Promise<void> {
  resetCsrfState();
  await client.cancelQueries();
  client.clear();
  client.setQueryData(sessionQueryKey, account);
}

export async function reconcileIdentity(client: QueryClient, next: AccountProfile | null): Promise<void> {
  const previous = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  if (previous?.id === next?.id) return;
  resetCsrfState();
  const privateQueries = { predicate: (query: Query) => query.queryKey[0] !== "session" };
  await client.cancelQueries(privateQueries);
  client.removeQueries(privateQueries);
}
