// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { CreditsScreen } from "./credits-screen";
import { CatalogCredits } from "./catalog-credits";

it("credits all providers with an approved TMDB logo and required disclaimer", () => {
  render(<CreditsScreen />);
  expect(screen.getByRole("heading", { level: 1, name: "Credits" })).toBeInTheDocument();
  for (const name of ["TMDB", "MusicBrainz", "IGDB"]) expect(screen.getByRole("heading", { name })).toBeInTheDocument();
  expect(screen.getByRole("img", { name: "TMDB" })).toHaveAttribute("src", "/branding/tmdb.svg");
  expect(screen.getByText("This product uses the TMDB API but is not endorsed or certified by TMDB.")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Third-party software notices" })).toHaveAttribute("href", "/notices/THIRD-PARTY-NOTICES.txt");
});

it("credits stored identities once each without fetching provider availability or using untrusted URLs", () => {
  const view = render(<CatalogCredits type="movie" providers={["tmdb", "tmdb", "musicbrainz", "igdb", "unknown", "constructor", "__proto__"]} />);
  expect(screen.getAllByRole("link", { name: "TMDB" })).toHaveLength(1);
  expect(screen.getByRole("link", { name: "TMDB" })).toHaveAttribute("href", "https://www.themoviedb.org");
  expect(screen.getByRole("link", { name: "MusicBrainz" })).toHaveAttribute("href", "https://musicbrainz.org");
  expect(screen.getByRole("link", { name: "IGDB" })).toHaveAttribute("href", "https://www.igdb.com");
  expect(screen.getByRole("link", { name: "Provider credits" })).toHaveAttribute("href", "/credits");
  view.rerender(<CatalogCredits type="custom" providers={["tmdb"]} />);
  expect(screen.queryByRole("link")).not.toBeInTheDocument();
  view.rerender(<CatalogCredits type="movie" providers={["unknown"]} />);
  expect(screen.queryByRole("link")).not.toBeInTheDocument();
});
