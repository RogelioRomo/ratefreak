// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { ImageConfigContext } from "next/dist/shared/lib/image-config-context.shared-runtime";
import { imageConfigDefault } from "next/dist/shared/lib/image-config";
import config from "@/next.config";
import { MediaArtwork, supportedArtwork } from "./media-artwork";

function artwork(reference: string, type: "movie" | "video_game" | "book" | "custom" = "movie") {
  return <ImageConfigContext.Provider value={{ ...imageConfigDefault, ...config.images }}><MediaArtwork reference={reference} title="A title" type={type} /></ImageConfigContext.Provider>;
}
it.each([
  ["https://image.tmdb.org/t/p/w500/example.jpg", "movie"],
  ["https://images.igdb.com/igdb/image/upload/t_cover_big/co123.jpg", "video_game"],
  ["https://covers.openlibrary.org/b/id/12345-L.jpg?default=false", "book"],
] as const)("renders public %s through the image optimizer", (url, type) => {
  render(artwork(url, type)); const image = screen.getByRole("img", { name: "Artwork for A title" });
  const optimized = new URL(image.getAttribute("src")!, window.location.origin);
  expect(optimized.origin).toBe(window.location.origin); expect(optimized.pathname).toBe("/_next/image");
  expect(optimized.searchParams.get("url")).toBe(type === "book" ? "/provider-artwork/openlibrary/12345" : url); expect(image).toHaveAttribute("loading", "lazy");
  expect(image).toHaveAttribute("srcSet"); expect(image).toHaveAttribute("sizes");
});
it("keeps uploaded artwork behind its original access-checked route", () => {
  const reference = "/api/v1/media/a1111111-1111-1111-1111-111111111111/artwork/b1111111-1111-1111-1111-111111111111";
  render(artwork(reference, "custom")); const image = screen.getByRole("img");
  expect(image).toHaveAttribute("src", reference); expect(image).not.toHaveAttribute("srcSet");
});
it("shows an honest fallback on errors and retries when the source changes", () => {
  const view = render(artwork("https://image.tmdb.org/t/p/w500/one.jpg"));
  fireEvent.error(screen.getByRole("img")); expect(screen.getByText("Artwork unavailable")).toBeInTheDocument();
  view.rerender(artwork("https://image.tmdb.org/t/p/w500/two.jpg")); expect(screen.getByRole("img")).toBeInTheDocument();
});
it.each(["https://evil.test/image.jpg", "https://image.tmdb.org/t/p/w500/evil.svg", "https://covers.openlibrary.org/b/isbn/123-L.jpg", "https://covers.openlibrary.org/b/id/0-L.jpg?default=false"])("rejects unsafe or unsupported references %s", reference => {
  expect(supportedArtwork(reference)).toBeNull();
});
