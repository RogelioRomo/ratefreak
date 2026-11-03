"use client";

import { useEffect, useId, useRef, useState } from "react";
import { IconMenu2, IconX } from "@tabler/icons-react";
import type { MobileNavigationProps } from "./interfaces/mobile-navigation-props";
import styles from "./mobile-navigation.module.css";

export function MobileNavigation({ children }: MobileNavigationProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const previousOverflow = useRef<string | null>(null);
  const [open, setOpen] = useState(false);
  const id = useId();

  function restore() {
    if (previousOverflow.current !== null) {
      document.body.style.overflow = previousOverflow.current;
      previousOverflow.current = null;
    }
    setOpen(false);
    trigger.current?.focus();
  }

  function close() {
    dialog.current?.close();
  }

  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 36.001rem)");
    const handleResize = () => { if (desktop.matches && dialog.current?.open) dialog.current.close(); };
    desktop.addEventListener("change", handleResize);
    return () => {
      desktop.removeEventListener("change", handleResize);
      if (previousOverflow.current !== null) document.body.style.overflow = previousOverflow.current;
    };
  }, []);

  return <div className={styles.mobile}>
    <button ref={trigger} className={styles.iconButton} type="button" aria-label="Open menu" aria-expanded={open} aria-controls={id} aria-haspopup="dialog" onClick={() => {
      dialog.current?.showModal();
      previousOverflow.current = document.body.style.overflow;
      document.body.style.overflow = "hidden";
      setOpen(true);
    }}><IconMenu2 size={24} stroke={1.75} aria-hidden="true" /></button>
    <dialog ref={dialog} id={id} className={styles.panel} aria-label="Navigation menu" onClose={restore} onCancel={(event) => { event.preventDefault(); close(); }}>
      <div className={styles.heading}>
        <span className={styles.brand}>RateFreak<span>.</span></span>
        <button className={styles.iconButton} type="button" aria-label="Close menu" onClick={close} autoFocus><IconX size={24} stroke={1.75} aria-hidden="true" /></button>
      </div>
      <nav aria-label="Mobile navigation" onClick={(event) => {
        if ((event.target as HTMLElement).closest("a")) close();
      }}>{children}</nav>
    </dialog>
  </div>;
}
