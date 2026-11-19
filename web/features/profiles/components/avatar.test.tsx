// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import { Avatar } from "./avatar";

const first = "/api/v1/users/11111111-1111-1111-1111-111111111111/avatar/22222222-2222-2222-2222-222222222222";
it("falls back on a failed image and retries when its reference changes", () => {
  const view = render(<Avatar reference={first} label="Reader avatar" />);
  fireEvent.error(screen.getByRole("img", { name: "Reader avatar" }));
  expect(view.container.querySelector("img")).toBeNull();
  view.rerender(<Avatar reference={first.replace(/2/g, "3")} label="Reader avatar" />);
  expect(view.container.querySelector("img")).toHaveAttribute("src", first.replace(/2/g, "3"));
});
it("uses a decorative fallback without exposing an unsafe remote URL", () => {
  const view = render(<Avatar reference="https://example.test/tracker.png" />);
  expect(view.container.querySelector("img")).toBeNull();
  expect(view.container.firstChild).toHaveAttribute("aria-hidden", "true");
});
