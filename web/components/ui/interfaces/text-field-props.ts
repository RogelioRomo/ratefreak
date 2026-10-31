import type { InputHTMLAttributes } from "react";

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "children"> {
  name: string;
  label: string;
  hint?: string;
  errors?: readonly string[];
}
