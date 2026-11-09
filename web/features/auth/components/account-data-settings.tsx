"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import { ApiError } from "@/shared/api/api-error";
import { getCurrentAccount } from "../api/accounts";
import {
  confirmAccountData, deleteAccount, downloadAccountData, getAccountDataStatus, startAccountDataProvider,
  type AccountDataStatus, type DataOperation,
} from "../api/account-data";
import { navigateToProvider } from "../api/external-navigation";
import { useSession } from "../hooks/use-session";
import { sessionQueryKey } from "../session/query-keys";
import type { AccountProfile } from "../interfaces/account-profile";
import { DeleteAccountDialog } from "./delete-account-dialog";
import styles from "./account-data.module.css";

interface AccountDataSettingsProps {
  active?: boolean;
  onDeletionPendingChange?: (pending: boolean) => void;
}

export function AccountDataSettings({ active = false, onDeletionPendingChange }: AccountDataSettingsProps) {
  useSession();
  const client = useQueryClient();
  const account = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  // A confirmed identity change unmounts every credential, dialog and status.
  return account ? <AccountDataEditor key={account.id} account={account} active={active} onDeletionPendingChange={onDeletionPendingChange} /> : null;
}

function AccountDataEditor({ account, active, onDeletionPendingChange }: AccountDataSettingsProps & { account: AccountProfile }) {
  const session = useSession();
  const router = useRouter();
  const [status, setStatus] = useState<AccountDataStatus | null>(null);
  const [operation, setOperation] = useState<DataOperation | null>(null);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const deleteTrigger = useRef<HTMLDivElement>(null);
  const locked = useRef(false);
  const passwordForm = useRef<HTMLFormElement>(null);

  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    void getAccountDataStatus(controller.signal).then(data => {
      if (controller.signal.aborted) return;
      setStatus(data);
      const marker = new URLSearchParams(window.location.search).get("dataAction");
      // This navigation hint never authorizes an operation. Only the server's
      // account-bound HttpOnly grant can establish confirmedOperation.
      if ((marker === "export" || marker === "delete") && data.confirmedOperation === marker) {
        setOperation(marker);
        setNotice("Ownership confirmed. Choose the action below to continue.");
      }
    }).catch(() => {
      if (!controller.signal.aborted) setError("We couldn’t load data settings. Retry to continue.");
    });
    return () => controller.abort();
  }, [active, account.id, reload]);

  useEffect(() => {
    if (!active || !session.account) passwordForm.current?.reset();
  }, [active, session.account]);

  async function perform(action: () => Promise<void>) {
    if (locked.current) return;
    locked.current = true;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (cause) {
      setError(cause instanceof ApiError && cause.status === 409 ? cause.message
        : cause instanceof ApiError && cause.status === 429 ? "Too many attempts. Wait before trying again."
          : "We couldn’t complete this action. Confirm ownership again and retry. If deletion was requested, check your session before doing anything else.");
    } finally {
      locked.current = false;
      setBusy(false);
    }
  }

  function clearConfirmation() {
    setOperation(null);
    setStatus(current => current ? { ...current, confirmedOperation: null } : null);
  }

  async function exportData() {
    setDialog(false);
    await perform(async () => {
      clearConfirmation();
      await session.runExclusive(downloadAccountData);
      setNotice("Your export has been downloaded.");
    });
  }

  function choose(next: DataOperation) {
    setError(null);
    setNotice(null);
    setOperation(next);
    if (status?.confirmedOperation !== next) return;
    if (next === "delete") setDialog(true);
    else void exportData();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!operation || busy) return;
    const password = String(new FormData(event.currentTarget).get("data-password") ?? "");
    event.currentTarget.reset();
    const next = operation;
    void perform(async () => {
      await session.runExclusive(() => confirmAccountData(next, password));
      setStatus(current => current ? { ...current, confirmedOperation: next } : null);
      if (next === "delete") setDialog(true);
      else {
        clearConfirmation();
        await session.runExclusive(downloadAccountData);
        setNotice("Your export has been downloaded.");
      }
    });
  }

  async function erase(handle: string) {
    // Notify the screen synchronously before changeIdentity can clear its cache
    // and unmount this editor. Success keeps that screen's login guard paused
    // until navigation; a failed operation restores ordinary session gating.
    onDeletionPendingChange?.(true);
    let completed = false;
    await perform(async () => {
      let erased = false;
      await session.changeIdentity(async () => {
        try {
          await deleteAccount(handle);
          erased = true;
          return null;
        } catch (cause) {
          if (cause instanceof ApiError && cause.status < 500) throw cause;
          // A lost response is ambiguous: reconcile once, never resubmit.
          const current = await getCurrentAccount();
          if (current === null) {
            erased = true;
            return null;
          }
          throw new Error("Deletion could not be confirmed. Check your account before retrying.");
        }
      });
      if (erased) {
        completed = true;
        router.replace("/account-deleted");
      }
    });
    if (!completed) onDeletionPendingChange?.(false);
    setDialog(false);
    clearConfirmation();
  }

  return <section className={styles.settings} aria-label="Data and privacy">
    <h2>Data and privacy</h2>
    <p>Download your account, library, reviews, comments, collections, connections, custom catalog contributions, artwork, recovery history and submitted reports. Credentials and staff-only records are excluded. Active account recovery events expire after 90 days.</p>
    {error && <p role="alert">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    {!status ? error ? <Button disabled={busy} onClick={() => { setError(null); setReload(value => value + 1); }}>Retry data settings</Button> : <p role="status">Loading data settings…</p> : <>
      <div>
        <h3>A copy for you</h3>
        <p>Export a ZIP archive with readable JSON and your stored artwork. Downloads are limited to 256 MiB and two attempts per ten minutes. Ownership confirmation lasts five minutes and applies to one action.</p>
        <Button disabled={busy} onClick={() => choose("export")}>{busy && operation !== "delete" ? "Preparing export…" : "Export my data"}</Button>
      </div>
      <div ref={deleteTrigger} className={styles.danger}>
        <h3>Delete account</h3>
        <p>Permanently remove your account and personal contributions. Public catalog entries remain without attribution. You’ll review retention details and confirm your handle before deletion.</p>
        <Button variant="secondary" disabled={busy} onClick={() => choose("delete")}>Delete account</Button>
      </div>
      {operation && !dialog && <div className={styles.confirmation}>
        <h3>Confirm ownership to {operation === "export" ? "export your data" : "delete your account"}</h3>
        {status.confirmedOperation === operation ? <Button disabled={busy} onClick={() => {
          if (operation === "delete") setDialog(true);
          else void exportData();
        }}>{operation === "delete" ? "Review deletion" : "Download export"}</Button>
          : status.hasPassword ? <form ref={passwordForm} aria-label="Confirm account ownership" onSubmit={submit}>
            <TextField name="data-password" label="Current password" type="password" autoComplete="current-password" required maxLength={128} disabled={busy} />
            <Button type="submit" disabled={busy}>{busy ? "Confirming…" : "Confirm ownership"}</Button>
          </form> : <div className={styles.actions}>
            {status.providers.filter(provider => provider === "google" || provider === "github").map(provider => <Button key={provider} disabled={busy} onClick={() => void perform(async () => {
              const response = await session.runExclusive(() => startAccountDataProvider(operation, provider));
              navigateToProvider(response.authorizationUrl);
            })}>Confirm with {provider === "github" ? "GitHub" : "Google"}</Button>)}
          </div>}
        <Button variant="secondary" disabled={busy} onClick={() => setOperation(null)}>Cancel confirmation</Button>
      </div>}
    </>}
    {dialog && <DeleteAccountDialog handle={account.handle} pending={busy}
      restoreFocus={() => deleteTrigger.current?.querySelector<HTMLButtonElement>("button")?.focus()}
      onCancel={() => { setDialog(false); setOperation(null); }} onConfirm={handle => void erase(handle)}
      onExport={() => {
        setDialog(false);
        setOperation("export");
        setNotice("Confirm ownership for an export. Deletion remains a separate action.");
      }} />}
  </section>;
}
