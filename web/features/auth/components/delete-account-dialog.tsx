"use client";

import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import styles from "./account-data.module.css";

interface DeleteAccountDialogProps {
  handle: string;
  pending: boolean;
  onCancel: () => void;
  onConfirm: (handle: string) => void;
  onExport: () => void;
  restoreFocus?: () => void;
}

export function DeleteAccountDialog({ handle, pending, onCancel, onConfirm, onExport, restoreFocus }: DeleteAccountDialogProps) {
  const [typed, setTyped] = useState("");
  const container = useRef<HTMLDivElement>(null);
  const overlay = useRef<HTMLDivElement>(null);
  const restore = useRef(restoreFocus);

  useEffect(() => {
    const trigger = document.activeElement as HTMLElement | null;
    const restoreTrigger = restore.current;
    const siblings = [...document.body.children].filter(element => element !== overlay.current);
    const previous = siblings.map(element => element.hasAttribute("inert"));
    siblings.forEach(element => element.setAttribute("inert", ""));
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const containFocus = (event: FocusEvent) => {
      if (!container.current?.contains(event.target as Node)) container.current?.focus();
    };
    document.addEventListener("focusin", containFocus);
    container.current?.querySelector<HTMLButtonElement>("button")?.focus();
    return () => {
      document.removeEventListener("focusin", containFocus);
      siblings.forEach((element, index) => { if (!previous[index]) element.removeAttribute("inert"); });
      document.body.style.overflow = overflow;
      if (restoreTrigger) restoreTrigger();
      else trigger?.focus();
    };
  }, []);

  useEffect(() => { if (pending) container.current?.focus(); }, [pending]);

  return createPortal(<div ref={overlay} className={styles.overlay}>
    <div ref={container} role="dialog" aria-modal="true" aria-busy={pending} tabIndex={-1}
      aria-labelledby="delete-account-title" aria-describedby="delete-account-retention" className={styles.dialog}
      onKeyDown={event => {
        if (event.key === "Escape") { event.preventDefault(); if (!pending) onCancel(); }
        if (event.key !== "Tab") return;
        const nodes = container.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), [tabindex="0"]');
        if (!nodes?.length) { event.preventDefault(); container.current?.focus(); return; }
        const first = nodes[0], last = nodes[nodes.length - 1];
        if (event.shiftKey && (document.activeElement === first || document.activeElement === container.current)) {
          event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || document.activeElement === container.current)) {
          event.preventDefault(); first.focus();
        }
      }}>
      <h2 id="delete-account-title">Permanently delete your account?</h2>
      <div id="delete-account-retention">
        <p>This cannot be undone. Your live account, personal data and recovery events are deleted immediately.</p>
        <p>Recovery events are kept for up to 90 days while your account exists. Deleting your account removes them immediately.</p>
        <p>Your library, ratings, reviews, comments, follows and collections are removed. Comments by others on your reviews are removed too. Deleting private catalog items also removes their dependent references and discussions.</p>
        <p>Private artwork is removed immediately where possible. If removal fails, the hourly orphan sweep retries files older than 24 hours. Backup copies expire within 30 days.</p>
        <p>Pseudonymized moderation records may still contain identifying information. Resolved records expire 365 days after resolution. Open reports remain until resolved.</p>
        <p>Public catalog items and categories remain without account attribution. Their artwork and other people’s references remain.</p>
      </div>
      <div className={styles.actions}>
        <Button variant="secondary" disabled={pending} onClick={onCancel}>Cancel</Button>
        <Button variant="secondary" disabled={pending} onClick={onExport}>Export my data first</Button>
      </div>
      <TextField name="delete-handle" label={`Type your handle ${handle}`} value={typed}
        onChange={event => setTyped(event.target.value)} disabled={pending} autoComplete="off" />
      <Button className={styles.destructive} disabled={pending || typed !== handle} onClick={() => onConfirm(typed)}>
        {pending ? "Deleting account…" : "Permanently delete account"}
      </Button>
    </div>
  </div>, document.body);
}
