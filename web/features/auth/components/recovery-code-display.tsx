"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import type { RecoveryCodeDisplayProps } from "../interfaces/recovery-code-display-props";
import styles from "./auth.module.css";
import recoveryStyles from "./recovery.module.css";

export function RecoveryCodeDisplay({ code, title, doneLabel, onDone }: RecoveryCodeDisplayProps) {
  const heading = useRef<HTMLHeadingElement>(null);
  const [saved, setSaved] = useState(false);
  const [message, setMessage] = useState("");
  useEffect(() => { heading.current?.focus(); }, []);
  async function copy() {
    try { await navigator.clipboard.writeText(code); setMessage("Recovery code copied. Save it somewhere safe."); }
    catch { setMessage("Copy unavailable. Select the code manually or download it."); }
  }
  function download() {
    const url = URL.createObjectURL(new Blob([`RateFreak recovery code\n${code}\n\nKeep this private. Using it replaces your password and this code.\n`], { type: "text/plain" }));
    const link = document.createElement("a"); link.href = url; link.download = "ratefreak-recovery-code.txt";
    document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    setMessage("Recovery code downloaded. Store the file somewhere safe.");
  }
  return <section className={styles.success} aria-label="Save recovery code">
    <h2 ref={heading} tabIndex={-1}>{title}</h2>
    <p>Save this code in your password manager or keep a printed copy somewhere safe. It can reset your password, so keep it private.</p>
    <code className={recoveryStyles.code}>{code}</code>
    <div className={recoveryStyles.actions}>
      <Button variant="secondary" onClick={() => { void copy(); }}>Copy code</Button>
      <Button variant="secondary" onClick={download}>Download code</Button>
    </div>
    <p role="status">{message}</p>
    <p>This code is shown once. Any previous code no longer works. If you lose both your password and this code, you need a previously linked sign-in provider to regain access. Admins cannot reset your password.</p>
    <label className={recoveryStyles.confirm}><input type="checkbox" checked={saved} onChange={event => setSaved(event.target.checked)} />I saved my recovery code</label>
    <Button disabled={!saved} onClick={onDone}>{doneLabel}</Button>
  </section>;
}
