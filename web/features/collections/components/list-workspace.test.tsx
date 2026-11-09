// @vitest-environment jsdom
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { CollectionDetailScreen } from "./collection-detail-screen";
import { details, id, mount } from "./test-support";
vi.mock("next/navigation", () => ({ usePathname: () => `/collections/${id}`, useSearchParams: () => new URLSearchParams() }));
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => { fetchMock.mockReset(); vi.stubGlobal("fetch", fetchMock); });
it("adds an existing catalog identity to a list without free title or field data", async () => {
  const mediaId = "b1111111-1111-1111-1111-111111111111";
  const media = { id: mediaId, title: "Hydrangea", type: "custom", imageReference: null, releaseDate: null };
  let current = structuredClone(details); let items: unknown[] = [];
  fetchMock.mockImplementation(async (url, init) => {
    const path = String(url);
    if (path.endsWith("csrf")) return Response.json({ requestToken: "token" });
    if (path.includes("/categories?")) return Response.json({ items: [], page: 1, pageSize: 100, totalCount: 0 });
    if (path.includes("/media?")) return Response.json({ items: [media], page: 1, pageSize: 20, totalCount: 1 });
    if (init?.method === "POST" && path.endsWith("/items")) {
      current = { ...current, collection: { ...current.collection, revision: 2 } };
      const added = { id: mediaId, mediaItemId: mediaId, media, position: 0, createdAt: "date", updatedAt: "date" }; items = [added]; return Response.json(added, { status: 201 });
    }
    if (path.includes("/items?")) return Response.json({ items, page: 1, pageSize: 20, totalCount: items.length });
    return Response.json(current);
  });
  mount(<CollectionDetailScreen id={id} />);
  fireEvent.click(await screen.findByRole("button", { name: "Add item" })); fireEvent.click(await screen.findByRole("button", { name: "Search catalog" }));
  fireEvent.click(await screen.findByRole("button", { name: "Choose Hydrangea" })); fireEvent.click(screen.getByRole("button", { name: "Add to list" }));
  await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
  const posted = JSON.parse(String(fetchMock.mock.calls.find(([, init]) => init?.method === "POST")?.[1]?.body)); expect(posted).toEqual({ expectedRevision: 1, mediaItemId: mediaId, position: 0 });
  expect(screen.queryByRole("button", { name: /add field/i })).toBeNull(); expect(screen.queryByLabelText(/custom title/i)).toBeNull();
});

it("requires explicit confirmation before clearing unavailable memberships", async () => {
 fetchMock.mockImplementation(async (url, init) => {
  if (String(url).endsWith("csrf")) return Response.json({requestToken: "token"});
  if (init?.method === "DELETE") return new Response(null, {status: 204});
  if (String(url).includes("/items?")) return Response.json({items: [], page: 1, pageSize: 20, totalCount: 0});
  return Response.json(details);
 });
 mount(<CollectionDetailScreen id={id} />);
 const remove = await screen.findByRole("button", {name: "Remove unavailable memberships"});
 expect(remove).toBeDisabled();
 fireEvent.click(screen.getByRole("checkbox", {name: "Remove all unavailable memberships from this list"}));fireEvent.click(remove);
 await screen.findByText("Unavailable memberships removed. Catalog items and contributions remain.");
 expect(fetchMock.mock.calls.find(([, init]) => init?.method === "DELETE")?.[0]).toContain(`/collections/${id}/unavailable-items?expectedRevision=1`);
});
