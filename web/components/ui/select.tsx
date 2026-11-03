"use client";

import { Children, isValidElement, useEffect, useId, useRef, useState } from "react";
import type { ReactElement, OptionHTMLAttributes } from "react";
import * as SelectPrimitive from "@radix-ui/react-select";
import { IconCheck, IconChevronDown, IconChevronUp } from "@tabler/icons-react";
import type { SelectProps } from "./interfaces/select-props";
import styles from "./select.module.css";

export function Select({ children, value, defaultValue, disabled, required, name, id, className, onChange, onInvalid, ...props }: SelectProps) {
  const generatedId = useId();
  const controlId = id ?? generatedId;
  const errorId = `${controlId}-required`;
  const native = useRef<HTMLSelectElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const options = Children.toArray(children).filter((child): child is ReactElement<OptionHTMLAttributes<HTMLOptionElement>> => isValidElement(child) && child.type === "option");
  const initialValue = String(defaultValue ?? options.find(option => !option.props.disabled)?.props.value ?? "");
  const [uncontrolled, setUncontrolled] = useState(initialValue);
  const [container, setContainer] = useState<HTMLDivElement | null>(null);
  const [open, setOpen] = useState(false);
  const [invalid, setInvalid] = useState(false);
  const selected = String(value ?? uncontrolled);
  const optionValue = (option: ReactElement<OptionHTMLAttributes<HTMLOptionElement>>) => String(option.props.value ?? option.props.children ?? "");
  // Radix reserves "" for its placeholder. Encode UI values only; form values stay exact.
  const encode = (input: string) => `value:${input}`;

  useEffect(() => {
    const form = native.current?.form;
    const reset = (event: Event) => {
      // The browser resets native values after dispatching the reset event.
      queueMicrotask(() => {
        if (event.defaultPrevented || !native.current) return;
        native.current.value = String(value ?? initialValue);
        if (value === undefined) setUncontrolled(initialValue);
        setInvalid(false);
        setOpen(false);
      });
    };
    form?.addEventListener("reset", reset);
    return () => form?.removeEventListener("reset", reset);
  }, [initialValue, value]);

  useEffect(() => {
    if (!open || !container) return;
    const observer = new MutationObserver(() => {
      if (native.current?.matches(":disabled") || container.closest("[hidden], [inert]")) setOpen(false);
    });
    if (native.current) observer.observe(native.current, { attributes: true, attributeFilter: ["disabled"] });
    for (let node: HTMLElement | null = container; node; node = node.parentElement) {
      observer.observe(node, { attributes: true, attributeFilter: ["hidden", "inert", "disabled"] });
    }
    return () => observer.disconnect();
  }, [open, container]);

  return <div ref={setContainer} className={styles.root}>
    <SelectPrimitive.Root open={open} onOpenChange={next => {
      if (!next || (!native.current?.matches(":disabled") && !container?.closest("[hidden], [inert]"))) setOpen(next);
    }} value={encode(selected)} disabled={disabled} onValueChange={next => {
      if (!native.current || native.current.matches(":disabled") || trigger.current?.closest("[inert], [hidden]")) return;
      native.current.value = next.slice("value:".length);
      native.current.dispatchEvent(new Event("change", { bubbles: true }));
    }}>
      <SelectPrimitive.Trigger {...{
        id: controlId,
        disabled,
        "aria-label": props["aria-label"],
        "aria-labelledby": props["aria-labelledby"],
        "aria-describedby": [props["aria-describedby"], invalid ? errorId : undefined].filter(Boolean).join(" ") || undefined,
        "aria-invalid": invalid || props["aria-invalid"],
        "aria-required": required,
      }} ref={trigger} className={[styles.trigger, className].filter(Boolean).join(" ")}>
        <SelectPrimitive.Value placeholder="Choose an option" />
        <SelectPrimitive.Icon asChild><IconChevronDown size={18} stroke={1.75} aria-hidden="true" /></SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal container={container}>
        <SelectPrimitive.Content position="popper" sideOffset={6} collisionPadding={12} className={styles.content}>
          <SelectPrimitive.ScrollUpButton className={styles.scrollButton}><IconChevronUp size={16} aria-hidden="true" /></SelectPrimitive.ScrollUpButton>
          <SelectPrimitive.Viewport className={styles.viewport}>
            {options.map(option => <SelectPrimitive.Item key={optionValue(option)} value={encode(optionValue(option))} data-value={optionValue(option)} disabled={option.props.disabled} className={styles.option}>
              <SelectPrimitive.ItemText>{option.props.children}</SelectPrimitive.ItemText>
              <SelectPrimitive.ItemIndicator className={styles.indicator}><IconCheck size={16} stroke={2} aria-hidden="true" /></SelectPrimitive.ItemIndicator>
            </SelectPrimitive.Item>)}
          </SelectPrimitive.Viewport>
          <SelectPrimitive.ScrollDownButton className={styles.scrollButton}><IconChevronDown size={16} aria-hidden="true" /></SelectPrimitive.ScrollDownButton>
        </SelectPrimitive.Content>
      </SelectPrimitive.Portal>
    </SelectPrimitive.Root>
    <select {...props} ref={native} id={`${controlId}-native`} aria-hidden="true" tabIndex={-1} hidden style={{ display: "none" }} name={name} disabled={disabled} required={required} value={selected} onChange={event => {
      if (value === undefined) setUncontrolled(event.target.value);
      setInvalid(false);
      onChange?.(event);
    }} onInvalid={event => {
      event.preventDefault();
      setInvalid(true);
      trigger.current?.focus();
      onInvalid?.(event);
    }}>{children}</select>
    {invalid && <p className={styles.error} id={errorId} role="alert">Please select an option.</p>}
  </div>;
}
