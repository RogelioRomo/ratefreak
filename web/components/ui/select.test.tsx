// @vitest-environment jsdom
import { useState } from "react";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { expect, it } from "vitest";
import { Select } from "./select";

function Choices() {
  const [value, setValue] = useState("");
  return <form aria-label="Filters"><label htmlFor="type">Media type</label><Select id="type" name="type" value={value} onChange={event => setValue(event.target.value)}><option value="">All types</option><option value="album">Albums</option><option value="movie" disabled>Movies unavailable</option></Select></form>;
}

it("renders a custom options panel and submits exact values including the empty choice", async () => {
  render(<Choices />);
  const trigger = screen.getByRole("combobox", { name: "Media type" });
  expect(trigger.tagName).toBe("BUTTON");
  expect(trigger).toHaveTextContent("All types");
  fireEvent.keyDown(trigger, { key: "ArrowDown" });
  expect(await screen.findByRole("listbox")).toBeVisible();
  expect(screen.getByRole("option", { name: "Movies unavailable" })).toHaveAttribute("aria-disabled", "true");
  fireEvent.click(screen.getByRole("option", { name: "Albums" }));
  expect(trigger).toHaveTextContent("Albums");
  expect(new FormData(screen.getByRole("form") as HTMLFormElement).get("type")).toBe("album");
  fireEvent.keyDown(trigger, { key: "Enter" });
  fireEvent.click(await screen.findByRole("option", { name: "All types" }));
  expect(new FormData(screen.getByRole("form") as HTMLFormElement).get("type")).toBe("");
});

it("dismisses with Escape and returns focus without changing the selection", async () => {
  render(<Choices />);
  const trigger = screen.getByRole("combobox");
  fireEvent.keyDown(trigger, { key: "Enter" });
  fireEvent.keyDown(await screen.findByRole("listbox"), { key: "Escape" });
  await waitFor(() => expect(screen.queryByRole("listbox")).not.toBeInTheDocument());
  expect(trigger).toHaveFocus();
  expect(trigger).toHaveTextContent("All types");
});

it("preserves disabled state and field error associations", () => {
  render(<><label htmlFor="disabled">Visibility</label><Select id="disabled" disabled aria-invalid="true" aria-describedby="error"><option value="private">Private</option></Select><p id="error">Choose a visibility</p></>);
  const trigger = screen.getByRole("combobox");
  expect(trigger).toBeDisabled();
  expect(trigger).toHaveAttribute("aria-invalid", "true");
  expect(trigger).toHaveAccessibleDescription("Choose a visibility");
});

it("keeps required validation and focuses the visible control", () => {
  render(<form><label htmlFor="rating">Rating</label><Select id="rating" required defaultValue=""><option value="">Not rated</option><option value="0">0 / 5</option></Select></form>);
  let valid = true;
  act(() => { valid = document.querySelector("form")!.reportValidity(); });
  expect(valid).toBe(false);
  expect(screen.getByRole("combobox")).toHaveFocus();
  expect(screen.getByRole("alert")).toHaveTextContent("Please select an option");
});

it("closes options when the retained form is hidden or disabled", async () => {
  const view = render(<div><fieldset><Choices /></fieldset></div>);
  fireEvent.keyDown(screen.getByRole("combobox"), { key: "Enter" });
  expect(await screen.findByRole("listbox")).toBeVisible();
  view.rerender(<div hidden inert><fieldset disabled><Choices /></fieldset></div>);
  await waitFor(() => expect(screen.queryByRole("listbox", { hidden: true })).not.toBeInTheDocument());
});

it("resets an uncontrolled value with its form", async () => {
  render(<form aria-label="Resettable"><label htmlFor="reset">Scope</label><Select id="reset" name="scope" defaultValue="all"><option value="all">All accessible</option><option value="mine">My collections</option></Select></form>);
  const trigger = screen.getByRole("combobox");
  fireEvent.keyDown(trigger, { key: "Enter" });
  fireEvent.click(screen.getByRole("option", { name: "My collections" }));
  expect(trigger).toHaveTextContent("My collections");
  act(() => { (screen.getByRole("form") as HTMLFormElement).reset(); });
  await waitFor(() => expect(trigger).toHaveTextContent("All accessible"));
  expect(new FormData(screen.getByRole("form") as HTMLFormElement).get("scope")).toBe("all");
});


it("closes an open menu when the control itself becomes disabled", async () => {
  const view = render(<Select aria-label="Type"><option value="text">Text</option></Select>);
  fireEvent.keyDown(screen.getByRole("combobox"), { key: "Enter" });
  expect(await screen.findByRole("listbox")).toBeVisible();
  view.rerender(<Select aria-label="Type" disabled><option value="text">Text</option></Select>);
  await waitFor(() => expect(screen.queryByRole("listbox")).not.toBeInTheDocument());
});

it("restores a non-first default value when resetting the form", async () => {
  render(<form aria-label="Default"><Select aria-label="Scope" name="scope" defaultValue="mine"><option value="all">All accessible</option><option value="mine">My collections</option></Select></form>);
  act(() => { (screen.getByRole("form") as HTMLFormElement).reset(); });
  await waitFor(() => expect(new FormData(screen.getByRole("form") as HTMLFormElement).get("scope")).toBe("mine"));
  expect(screen.getByRole("combobox")).toHaveTextContent("My collections");
});
