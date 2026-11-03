import { fireEvent, screen } from "@testing-library/react";

/** Use the visible custom dropdown, rather than changing its hidden form control. */
export function selectValue(trigger: HTMLElement, value: string) {
  fireEvent.keyDown(trigger, { key: "Enter" });
  const option = screen.getAllByRole("option").find(item => item.getAttribute("data-value") === value);
  if (!option) throw new Error(`Dropdown option not found: ${value}`);
  fireEvent.click(option);
}
