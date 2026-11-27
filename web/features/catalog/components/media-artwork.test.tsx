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
it("renders Open Library covers directly without proxying or optimization", () => {
  const url = "https://covers.openlibrary.org/b/id/12345-L.jpg?default=false";
  render(artwork(url, "book"));
  const image = screen.getByRole("img", { name: "Artwork for A title" });
  expect(image).toHaveAttribute("src", url);
  expect(image).not.toHaveAttribute("srcset");
  expect(image).toHaveAttribute("loading", "lazy");
  expect(image).toHaveAttribute("decoding", "async");
  expect(image).toHaveAttribute("referrerpolicy", "no-referrer");
});
it("shows a fallback when a direct Open Library cover is unavailable", () => {
  render(artwork("https://covers.openlibrary.org/b/id/12345-L.jpg?default=false", "book"));
  fireEvent.error(screen.getByRole("img"));
  expect(screen.queryByRole("img")).not.toBeInTheDocument();
  expect(screen.getByText("Artwork unavailable")).toBeInTheDocument();
});
it("renders TMDB artwork from the unoptimized same-origin poster route", () => {
  render(artwork("https://image.tmdb.org/t/p/w500/example.jpg"));
  const image = screen.getByRole("img", { name: "Artwork for A title" });
  const url = new URL(image.getAttribute("src")!, window.location.origin);
  expect(url.origin).toBe(window.location.origin);
  expect(url.pathname + url.search).toBe("/tmdb-posters/example.jpg");
  expect(image).toHaveAttribute("loading", "lazy");
  expect(image).not.toHaveAttribute("srcset");
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
it.each(["https://evil.test/image.jpg", "https://image.tmdb.org/t/p/w500/evil.svg", "https://covers.openlibrary.org/b/isbn/123-L.jpg", "https://covers.openlibrary.org/b/id/0-L.jpg?default=false", "https://covers.openlibrary.org/b/id/12345-L.jpg?default=false&target=http://127.0.0.1", "https://covers.openlibrary.org/b/id/12345-L.jpg?default=false#fragment", "https://covers.openlibrary.org.evil.test/b/id/12345-L.jpg?default=false", "https://covers.openlibrary.org/b/id/12345-L.jpg?default=false\n"])("rejects unsafe or unsupported references %s", reference => {
  expect(supportedArtwork(reference)).toBeNull();
});

it("renders IGDB artwork through the unoptimized managed cover route", () => {
  render(artwork("https://images.igdb.com/igdb/image/upload/t_cover_big/co123.jpg", "video_game"));
  const image = screen.getByRole("img", { name: "Artwork for A title" });
  const url = new URL(image.getAttribute("src")!, window.location.origin);
  expect(url.origin).toBe(window.location.origin);
  expect(url.pathname + url.search).toBe("/igdb-covers/co123.jpg");
  expect(image).not.toHaveAttribute("srcset");
});

it("renders canonical Cover Art Archive covers directly and falls back on a miss", () => {
  const reference = "https://coverartarchive.org/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500";
  render(artwork(reference));
  expect(screen.getByRole("img")).toHaveAttribute("src", reference);
  expect(screen.getByRole("img")).not.toHaveAttribute("srcset");
  fireEvent.error(screen.getByRole("img"));
  expect(screen.getByText("Artwork unavailable")).toBeVisible();
});
it.each([
  "https://coverartarchive.org.evil.test/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500",
  "https://coverartarchive.org/release-group/00000000-0000-0000-0000-000000000000/front-500",
  "https://coverartarchive.org/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500?url=http://localhost",
  "https://coverartarchive.org/release-group/3bd76d40-7f0e-36b7-9348-91a33afee20e/front-500\n"
])("rejects noncanonical CAA reference %s", reference => expect(supportedArtwork(reference)).toBeNull());
it("renders artwork from a song’s associated release", () => {
  const reference = "https://coverartarchive.org/release/df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51/front-500";
  render(artwork(reference));
  expect(screen.getByRole("img")).toHaveAttribute("src", reference);
  expect(screen.getByRole("img")).toHaveAttribute("referrerpolicy", "no-referrer");
});
it.each([
  "https://coverartarchive.org/release/00000000-0000-0000-0000-000000000000/front-500",
  "https://coverartarchive.org/release/df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51/front-500?target=http://localhost",
  "https://coverartarchive.org/release/df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51/front-500\n"
])("rejects malformed release cover %s", reference => expect(supportedArtwork(reference)).toBeNull());
