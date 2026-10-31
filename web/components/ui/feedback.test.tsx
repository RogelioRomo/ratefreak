// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { Button } from "./button";
import { LoadingState } from "./loading-state";
import { EmptyState } from "./empty-state";
import { ErrorState } from "./error-state";

it("announces loading with an overridable label", () => {
  const view = render(<LoadingState />);
  expect(screen.getByRole("status")).toHaveTextContent("Loading…");
  view.rerender(<LoadingState label="Loading your library…" />);
  expect(screen.getByRole("status")).toHaveTextContent("Loading your library…");
});

it("shows empty state context", () => {
  render(<EmptyState title="No media yet" description="Your library starts here." />);
  expect(screen.getByRole("heading", { name: "No media yet" })).toBeInTheDocument();
  expect(screen.getByText("Your library starts here.")).toBeInTheDocument();
});

it("announces errors and offers retry only when provided", () => {
  const retry = vi.fn();
  const view = render(<ErrorState description="Please try again." />);
  expect(screen.getByRole("alert")).toHaveTextContent("Something went wrong.");
  expect(screen.queryByRole("button")).not.toBeInTheDocument();
  view.rerender(<ErrorState onRetry={retry} />);
  fireEvent.click(screen.getByRole("button", { name: "Retry" }));
  expect(retry).toHaveBeenCalledTimes(1);
});

it("defaults buttons to non-submit and honors disabled/explicit type", () => {
  const click = vi.fn();
  const view = render(<Button onClick={click}>Save</Button>);
  expect(screen.getByRole("button")).toHaveAttribute("type", "button");
  view.rerender(<Button type="submit" disabled onClick={click}>Save</Button>);
  expect(screen.getByRole("button")).toHaveAttribute("type", "submit");
  fireEvent.click(screen.getByRole("button"));
  expect(click).not.toHaveBeenCalled();
});
