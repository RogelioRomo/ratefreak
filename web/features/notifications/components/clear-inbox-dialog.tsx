"use client";
import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import styles from "./notifications.module.css";
export function ClearInboxDialog({ pending, onCancel, onConfirm }: { pending: boolean; onCancel: () => void; onConfirm: () => void }) {
  const container = useRef<HTMLDivElement>(null); const overlay = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const trigger = document.activeElement as HTMLElement | null;
    const siblings = [...document.body.children].filter(el => el !== overlay.current);
    const previous = siblings.map(el => el.hasAttribute("inert")); siblings.forEach(el => el.setAttribute("inert", ""));
    const overflow = document.body.style.overflow; document.body.style.overflow = "hidden";
    const contain = (e: FocusEvent) => { if (!container.current?.contains(e.target as Node)) container.current?.focus(); };
    document.addEventListener("focusin", contain); container.current?.querySelector("button")?.focus();
    return () => { document.removeEventListener("focusin", contain); siblings.forEach((el,i) => { if (!previous[i]) el.removeAttribute("inert"); }); document.body.style.overflow = overflow; trigger?.focus(); };
  }, []);
  useEffect(() => { if (pending) container.current?.focus(); }, [pending]);
  return createPortal(<div ref={overlay} className={styles.overlay}><div ref={container} className={styles.dialog} role="dialog" aria-modal="true" tabIndex={-1}
    aria-labelledby="clear-inbox-title" aria-describedby="clear-inbox-description" aria-busy={pending}
    onKeyDown={e => {
      if (e.key === "Escape") { e.preventDefault(); if (!pending) onCancel(); }
      if (e.key !== "Tab") return;
      const nodes = container.current?.querySelectorAll<HTMLButtonElement>("button:not(:disabled)");
      if (!nodes?.length) { e.preventDefault(); container.current?.focus(); return; }
      const first = nodes[0], last = nodes[nodes.length - 1];
      if (e.shiftKey && (document.activeElement === first || document.activeElement === container.current)) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && (document.activeElement === last || document.activeElement === container.current)) { e.preventDefault(); first.focus(); }
    }}><h2 id="clear-inbox-title">Clear your notification inbox?</h2>
    <p id="clear-inbox-description">This permanently deletes all your existing notifications, including temporarily hidden notifications. Your comments and follow relationships stay intact. This cannot be undone. Notifications received after clearing begins will remain.</p>
    <div className={styles.actions}><Button variant="secondary" disabled={pending} onClick={onCancel}>Cancel</Button><Button disabled={pending} onClick={onConfirm}>{pending ? "Clearing inbox…" : "Permanently clear inbox"}</Button></div>
  </div></div>, document.body);
}
