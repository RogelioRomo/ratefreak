// @vitest-environment jsdom
import { useState } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, it, vi } from "vitest";
import { selectValue } from "@/test/select-value";
import { CategoryPicker } from "./category-picker";

const category = { id: "b1111111-1111-1111-1111-111111111111", name: "Flowers", scope: "private", revision: 1 };
const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});
function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Picker() {
    const [value, setValue] = useState("");
    return <><CategoryPicker viewer="owner" value={value} onChange={setValue} /><output aria-label="Selected category">{value || "None"}</output></>;
  }
  render(<QueryClientProvider client={client}><Picker /></QueryClientProvider>);
}

it("shows only the chosen category workflow and clears the old selection when creating", async () => {
  fetchMock.mockResolvedValue(Response.json({ items: [category], page: 1, pageSize: 100, totalCount: 1 }));
  mount();
  await waitFor(() => expect(screen.getByRole("combobox", { name: "Category" })).toBeEnabled());
  expect(screen.queryByLabelText("New category name")).not.toBeInTheDocument();
  expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
  selectValue(screen.getByRole("combobox", { name: "Category" }), category.id);
  expect(screen.getByLabelText("Selected category")).toHaveTextContent(category.id);
  selectValue(screen.getByRole("combobox", { name: "Category option" }), "new");
  expect(screen.queryByRole("combobox", { name: "Category" })).not.toBeInTheDocument();
  expect(screen.getByLabelText("New category name")).toBeVisible();
  expect(screen.getByLabelText("Category visibility")).toBeVisible();
  expect(screen.getByLabelText("Selected category")).toHaveTextContent("None");
  selectValue(screen.getByRole("combobox", { name: "Category option" }), "existing");
  expect(screen.queryByLabelText("New category name")).not.toBeInTheDocument();
  expect(screen.getByRole("combobox", { name: "Category" })).toBeVisible();
});

it("keeps a failed category draft and selects the category after a successful retry", async () => {
  let attempts = 0;
  fetchMock.mockImplementation(async (url, init) => {
    if (String(url).endsWith("auth/csrf")) return Response.json({ requestToken: "csrf" });
    if (init?.method === "POST") return ++attempts === 1 ? Response.json({}, { status: 503 }) : Response.json(category, { status: 201 });
    return Response.json({ items: [], page: 1, pageSize: 100, totalCount: 0 });
  });
  mount();
  selectValue(screen.getByRole("combobox", { name: "Category option" }), "new");
  fireEvent.change(screen.getByLabelText("New category name"), { target: { value: "Flowers" } });
  fireEvent.click(screen.getByRole("button", { name: "Create category" }));
  await screen.findByRole("alert");
  expect(screen.getByLabelText("New category name")).toHaveValue("Flowers");
  fireEvent.click(screen.getByRole("button", { name: "Create category" }));
  await waitFor(() => expect(screen.getByLabelText("Selected category")).toHaveTextContent(category.id));
  expect(screen.getByRole("combobox", { name: "Category" })).toHaveTextContent("Flowers (private)");
  expect(screen.queryByLabelText("New category name")).not.toBeInTheDocument();
});
