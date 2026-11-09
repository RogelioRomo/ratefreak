"use client";

import { useRef, useState, type KeyboardEvent } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import { ProfileEditor } from "@/features/profiles/components/profile-editor";
import { useSession } from "../hooks/use-session";
import { AccountDataSettings } from "./account-data-settings";
import { AccountDetails } from "./account-details";
import { RecoverySettings } from "./recovery-settings";
import { LinkedProviderSettings } from "./linked-provider-settings";
import { LogoutControl } from "./logout-control";
import styles from "./account-settings.module.css";

const tabs = [
  { id: "overview", label: "Overview" },
  { id: "profile", label: "Profile" },
  { id: "sign-in", label: "Sign-in options" },
  { id: "recovery", label: "Password recovery" },
  { id: "data", label: "Data and privacy" },
] as const;

export function AccountSections({ onDeletionPendingChange }: { onDeletionPendingChange?: (pending: boolean) => void }) {
  const session = useSession();
  const [selected, setSelected] = useState(() => typeof window !== "undefined" && ["export", "delete"].includes(new URLSearchParams(window.location.search).get("dataAction") ?? "") ? 4 : 0);
  const buttons = useRef<Array<HTMLButtonElement | null>>([]);
  const unavailable = !session.account || session.isTransitioning;
  function navigate(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    let next: number;
    switch (event.key) {
      case "ArrowDown": next = (index + 1) % tabs.length; break;
      case "ArrowUp": next = (index + tabs.length - 1) % tabs.length; break;
      case "Home": next = 0; break;
      case "End": next = tabs.length - 1; break;
      default: return;
    }
    event.preventDefault(); setSelected(next); buttons.current[next]?.focus();
  }
  return <div className={styles.layout}>
    <aside className={styles.sidebar}>
      <div role="tablist" aria-label="Account settings" aria-orientation="vertical" className={styles.menu}>
        {tabs.map((tab, index) => <button key={tab.id} ref={element => { buttons.current[index] = element; }} type="button" role="tab"
          id={`account-tab-${tab.id}`} aria-controls={`account-panel-${tab.id}`} aria-selected={selected === index}
          tabIndex={selected === index ? 0 : -1} disabled={unavailable}
          onClick={() => setSelected(index)} onKeyDown={event => navigate(event, index)}>{tab.label}</button>)}
      </div>
      <div className={styles.logout}><LogoutControl /></div>
    </aside>
    <div className={styles.content}>
      {session.error && selected !== 0 && <ErrorState title="We couldn’t load your account" description="Please try again." onRetry={() => { void session.refresh(); }} />}
      {!session.account && !session.error && selected !== 0 && <LoadingState label="Checking your session…" />}
      {/* Keep panels mounted so switching sections preserves drafts and unsaved codes. */}
      {tabs.map((tab, index) => <div key={tab.id} role="tabpanel" id={`account-panel-${tab.id}`} aria-labelledby={`account-tab-${tab.id}`}
        hidden={selected !== index || (index !== 0 && unavailable)} inert={selected !== index || (index !== 0 && unavailable)} tabIndex={0} className={styles.panel}>
        {tab.id === "overview" && <><h2 className={styles.sectionHeading}>Account details</h2><AccountDetails /></>}
        {tab.id === "profile" && <ProfileEditor />}
        {tab.id === "sign-in" && <LinkedProviderSettings />}
        {tab.id === "recovery" && <RecoverySettings />}
        {tab.id === "data" && <AccountDataSettings active={selected === index} onDeletionPendingChange={onDeletionPendingChange} />}
      </div>)}
    </div>
  </div>;
}
