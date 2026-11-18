// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { BookMetadata } from "./book-metadata";

it("shows author names, honest year precision and a canonical work source link", () => {
  render(<BookMetadata metadata={{ authors: [{ id: "OL34184A", name: "Roald Dahl" }], firstPublicationYear: 1970 }} externalId="OL45804W" />);
  expect(screen.getByText("By Roald Dahl")).toBeInTheDocument();
  expect(screen.getByText("1970")).toHaveAttribute("dateTime", "1970");
  expect(screen.getByRole("link", { name: "View on Open Library" })).toHaveAttribute("href", "https://openlibrary.org/works/OL45804W");
});
it("handles missing or malformed metadata without inventing a date or trusting source URLs", () => {
  const view = render(<BookMetadata metadata={null} externalId="https://evil.test/book" />);
  expect(screen.getByText("Publication year unavailable")).toBeInTheDocument(); expect(screen.queryByRole("link")).not.toBeInTheDocument();
  view.rerender(<BookMetadata metadata={{ authors: [null, { name: "<script>Plain text</script>" }], firstPublicationYear: "1970" }} />);
  expect(screen.getByText("By <script>Plain text</script>")).toBeInTheDocument(); expect(screen.getByText("Publication year unavailable")).toBeInTheDocument();
});
