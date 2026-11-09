// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { SessionContext } from "@/features/auth/session/session-context";
import { selectValue } from "@/test/select-value";
import type { CustomItem } from "../interfaces/custom-catalog";
import { ItemEditor } from "./item-editor";
import { CreateItemScreen } from "./create-item-screen";
const id = "a1111111-1111-1111-1111-111111111111";
const categoryId = "b1111111-1111-1111-1111-111111111111";
const saved: CustomItem = { isHidden: false, metadata: {}, createdAt: "2026-11-08", updatedAt: "2026-11-08", externalIds: [], releaseDate: null, id, type: "custom", title: "Blue hydrangea", description: null, visibility: "private", revision: 1, category: { id: categoryId, name: "Flowers", scope: "public", revision: 1 }, creatorUserId: id, isArchived: false, imageReference: null };
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => { fetchMock.mockReset(); vi.stubGlobal("fetch", fetchMock); });
it("creates privately and retries an image without creating a duplicate item", async () => {
  let uploads = 0;
  fetchMock.mockImplementation(async (url, init) => {
    const path = String(url);
    if (path.includes("auth/csrf")) return Response.json({ requestToken: "csrf" });
    if (path.includes("/categories")) return Response.json({ items: [saved.category], totalCount: 1, page: 1, pageSize: 20 });
    if (path.endsWith("/manage")) return Response.json(saved);
    if (path.endsWith("/artwork")) return ++uploads === 1 ? Response.json({ title: "Unavailable" }, { status: 503 }) : Response.json({ ...saved, revision: 2, imageReference: `/api/v1/media/${id}/artwork/${id}` });
    if (path.endsWith("/media") && init?.method === "POST") return Response.json(saved, { status: 201 });
    throw new Error("Unexpected request " + path);
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<QueryClientProvider client={client}><SessionContext.Provider value={{ account: { id, handle: "gardener", displayName: "Gardener", email: "a@example.test", createdAt: "2026-01-01" }, isLoading: false, isTransitioning: false, error: null, refresh: vi.fn().mockResolvedValue(undefined), runExclusive: fn => fn(), changeIdentity: vi.fn() }}><CreateItemScreen /></SessionContext.Provider></QueryClientProvider>);
  fireEvent.change(await screen.findByLabelText("Title"), { target: { value: "Blue hydrangea" } });
  await screen.findAllByText("Flowers"); selectValue(screen.getByLabelText("Category"), categoryId);
  fireEvent.click(screen.getByRole("button", { name: "Create item" }));
  await screen.findByRole("link", { name: "View item" });
  const create = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith("/media") && init?.method === "POST"); expect(JSON.parse(String(create?.[1]?.body)).visibility).toBe("private");
  fireEvent.change(screen.getByLabelText("Item image", { selector: "input" }), { target: { files: [new File(["image"], "flower.png", { type: "image/png" })] } });
  fireEvent.click(screen.getByRole("button", { name: "Upload image" })); await screen.findAllByRole("alert");
  fireEvent.click(screen.getByRole("button", { name: "Reload saved item" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Upload image" })).toBeEnabled());
  fireEvent.click(screen.getByRole("button", { name: "Upload image" })); await waitFor(() => expect(uploads).toBe(2));
  expect(fetchMock.mock.calls.filter(([url, init]) => String(url).endsWith("/media") && init?.method === "POST")).toHaveLength(1);
});

it("reloads a conflicting saved revision while preserving edited fields", async () => {
  let patches = 0;
  fetchMock.mockImplementation(async (url, init) => {
    const path = String(url);
    if (path.includes("auth/csrf")) return Response.json({ requestToken: "csrf" });
    if (path.includes("/categories")) return Response.json({ items: [saved.category], totalCount: 1, page: 1, pageSize: 20 });
    if (path.endsWith("/manage")) return Response.json({ ...saved, title: "Changed elsewhere", revision: 2 });
    if (init?.method === "PATCH") return ++patches === 1 ? Response.json({ title: "Conflict" }, { status: 409 }) : Response.json({ ...saved, revision: 3 });
    throw new Error("Unexpected request " + path);
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><SessionContext.Provider value={{ account: { id, handle: "gardener", displayName: "Gardener", email: "a@example.test", createdAt: "2026-01-01" }, isLoading: false, isTransitioning: false, error: null, refresh: vi.fn(), runExclusive: fn => fn(), changeIdentity: vi.fn() }}><ItemEditor initial={saved} viewer={id} active /></SessionContext.Provider></QueryClientProvider>);
  fireEvent.change(screen.getByLabelText("Title"), { target: { value: "My retained correction" } });
  fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
  fireEvent.click(await screen.findByRole("button", { name: "Reload saved item" }));
  await waitFor(() => expect(screen.queryByText(/This item changed/)).toBeNull());
  expect(screen.getByLabelText("Title")).toHaveValue("My retained correction");
  fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
  await waitFor(() => expect(patches).toBe(2));
  const writes = fetchMock.mock.calls.filter(([, init]) => init?.method === "PATCH");
  expect(JSON.parse(String(writes[1][1]?.body))).toMatchObject({ expectedRevision: 2, title: "My retained correction" });
});
