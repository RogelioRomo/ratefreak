// @vitest-environment jsdom
import { selectValue } from "@/test/select-value";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { CatalogScreen } from "./catalog-screen";
import { MediaDetailScreen } from "./media-detail-screen";

vi.mock("@/features/personal/components/title-activity", () => ({ TitleActivity: () => null }));
const route = vi.hoisted(() => ({ query: "", push: vi.fn() }));
vi.mock("next/navigation", () => ({ useSearchParams: () => new URLSearchParams(route.query), useRouter: () => ({ push: route.push }) }));
const fetchMock = vi.fn<typeof fetch>();
const id = "a1111111-1111-1111-1111-111111111111";
const item = { id, type: "movie", title: "A very long title ".repeat(12), releaseDate: null, imageReference: null };
const page = { items: [item], totalCount: 42, page: 1, pageSize: 20 };
function setup(child = <CatalogScreen />) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const wrapper = ({ children }: { children: React.ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  return render(child, { wrapper });
}
beforeEach(() => { route.query = ""; route.push.mockReset(); fetchMock.mockReset(); vi.stubGlobal("fetch", fetchMock); });
it("shows loading then linked results and filter-preserving pagination", async () => {
  fetchMock.mockResolvedValue(Response.json(page));
  setup();
  expect(screen.getByRole("status")).toHaveTextContent("Loading catalog");
  expect(await screen.findByRole("link", { name: item.title.trim() })).toHaveAttribute("href", `/catalog/${id}`);
  expect(screen.getByText("42 titles")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /next/i })).toHaveAttribute("href", "/catalog?page=2");
  expect(screen.getByText("Artwork unavailable")).toBeInTheDocument();
});
it("submits explicit filters and resets pagination", async () => {
  route.query = "q=old&type=movie&page=3";
  fetchMock.mockResolvedValue(Response.json(page));
  setup();
  fireEvent.change(screen.getByRole("searchbox", { name: "Title" }), { target: { value: "  A & B  " } });
  selectValue(screen.getByLabelText("Media type"), "album");
  fireEvent.click(screen.getByRole("button", { name: "Search catalog" }));
  expect(route.push).toHaveBeenCalledWith("/catalog?q=A+%26+B&type=album");
  expect(fetchMock).toHaveBeenCalledTimes(1);
});
it("restores form inputs on URL changes and isolates a late response", async () => {
  let resolve!: (response: Response) => void;
  fetchMock.mockImplementationOnce(() => new Promise(r => { resolve = r; })).mockResolvedValue(Response.json({ ...page, items: [{ ...item, title: "New result" }] }));
  const view = setup();
  route.query = "q=new&type=album&page=2";
  view.rerender(<CatalogScreen />);
  expect(screen.getByRole("searchbox")).toHaveValue("new");
  expect(screen.getByLabelText("Media type")).toHaveTextContent("Albums");
  expect(await screen.findByText("New result")).toBeInTheDocument();
  await act(async () => { resolve(Response.json(page)); });
  expect(screen.queryByText(item.title.trim())).not.toBeInTheDocument();
  expect(screen.getByRole("link", { name: /previous/i })).toHaveAttribute("href", "/catalog?q=new&type=album");
  route.query = "";
  view.rerender(<CatalogScreen />);
  expect(screen.getByRole("searchbox")).toHaveValue("");
});
it.each([
  ["", 0, "The catalog is waiting for its first title."],
  ["q=unknown", 0, "No matching titles."],
  ["q=film&page=8", 42, "This page has no titles."],
])("provides empty recovery for %s", async (query, totalCount, heading) => {
  route.query = query;
  fetchMock.mockResolvedValue(Response.json({ ...page, items: [], totalCount }));
  setup();
  expect(await screen.findByRole("heading", { name: heading })).toBeInTheDocument();
  if (query) expect(screen.getByRole("link", { name: /all titles|first page/i })).toHaveAttribute("href", query.includes("page=") ? "/catalog?q=film" : "/catalog");
});
it("does not fetch invalid URL filters", () => {
  route.query = "q=a&q=b";
  setup();
  expect(screen.getByRole("alert")).toHaveTextContent("Invalid catalog filters");
  expect(fetchMock).not.toHaveBeenCalled();
  expect(screen.getByRole("link", { name: "Reset filters" })).toHaveAttribute("href", "/catalog");
});
it.each([429, 503])("shows retryable %s and recovers", async (status) => {
  fetchMock.mockResolvedValueOnce(Response.json({ title: "Failure" }, { status })).mockResolvedValue(Response.json(page));
  setup();
  expect(await screen.findByRole("alert")).toHaveTextContent(status === 429 ? "Too many requests" : "Catalog unavailable");
  fireEvent.click(screen.getByRole("button", { name: "Retry" }));
  expect(await screen.findByText(item.title.trim())).toBeInTheDocument();
});
it("renders details with honest absent metadata and artwork", async () => {
  fetchMock.mockResolvedValue(Response.json({ ...item, description: null }));
  setup(<MediaDetailScreen id={id} />);
  expect(await screen.findByRole("heading", { level: 1, name: item.title.trim() })).toBeInTheDocument();
  expect(screen.getByText("Release date unavailable")).toBeInTheDocument();
  expect(screen.getByText("No description available yet.")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /catalog/i })).toHaveAttribute("href", "/catalog");
});
it.each([404, 503, 429])("handles detail %s", async (status) => {
  fetchMock.mockResolvedValue(Response.json({}, { status }));
  setup(<MediaDetailScreen id={id} />);
  expect(await screen.findByRole("heading", { name: status === 404 ? "Title not found." : status === 429 ? "Too many requests." : "Title unavailable." })).toBeInTheDocument();
  if (status !== 404) expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
});
it("rejects invalid detail IDs locally", () => {
  setup(<MediaDetailScreen id="invalid" />);
  expect(screen.getByRole("heading", { name: "Title not found." })).toBeInTheDocument();
  expect(fetchMock).not.toHaveBeenCalled();
});
it("falls back when supported artwork fails and blocks untrusted hosts", async () => {
  fetchMock.mockResolvedValue(Response.json({ ...page, items: [{ ...item, imageReference: "https://image.tmdb.org/t/p/w500/example.jpg" }, { ...item, id: "b", title: "Unsafe artwork", imageReference: "https://evil.test/track.jpg" }] }));
  setup();
  const image = await screen.findByRole("img", { name: /artwork for/i });
  expect(document.querySelector('img[src*="evil"]')).toBeNull();
  fireEvent.error(image);
  await waitFor(() => expect(screen.queryByRole("img")).not.toBeInTheDocument());
  expect(screen.getAllByText("Artwork unavailable")).toHaveLength(2);
});
