// @vitest-environment jsdom
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import type { SessionContextValue } from "@/features/auth/interfaces/session-context-value";
import { useAggregate } from "@/features/personal/hooks/use-personal";
it("never reuses a creator's private rating aggregate after identity changes", async () => {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValueOnce(Response.json({ count: 7, average: 4.5 })).mockResolvedValue(Response.json({}, { status: 404 })); vi.stubGlobal("fetch", fetchMock);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const base: SessionContextValue = { account: { id: "owner", handle: "owner", email: "a@test.example", displayName: "Owner", createdAt: "2026-01-01" }, isLoading: false, isTransitioning: false, error: null, refresh: vi.fn(), runExclusive: fn => fn(), changeIdentity: vi.fn() };
  function Aggregate() { const query = useAggregate("a1111111-1111-1111-1111-111111111111"); return <p>{query.data ? `Private count ${query.data.count}` : "No aggregate"}</p>; }
  const tree = (value: SessionContextValue) => <QueryClientProvider client={client}><SessionContext.Provider value={value}><Aggregate /></SessionContext.Provider></QueryClientProvider>;
  const view = render(tree(base)); await screen.findByText("Private count 7"); view.rerender(tree({ ...base, account: null }));
  expect(screen.queryByText("Private count 7")).not.toBeInTheDocument(); await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
});
