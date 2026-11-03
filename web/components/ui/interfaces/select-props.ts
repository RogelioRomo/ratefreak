import type { SelectHTMLAttributes } from "react";

/** Single-value select. Options use the same values and labels as HTML options. */
export interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "multiple" | "size" | "value" | "defaultValue"> {
  value?: string | number;
  defaultValue?: string | number;
}
