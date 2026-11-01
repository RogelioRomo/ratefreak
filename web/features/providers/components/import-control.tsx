"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { IconDownload, IconArrowUpRight } from "@tabler/icons-react";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import type { ImportControlProps } from "../interfaces/component-props";
import type { ImportMediaResult } from "../interfaces/provider";
import { importMedia } from "../api/providers";
import { previewHref } from "../query/identity";
import styles from "./providers.module.css";
function LoginToImport({ identity }: ImportControlProps) {
  return <Link className={styles.link} href={`/login?${new URLSearchParams({ returnTo: previewHref(identity) })}`}>Log in to import<IconArrowUpRight size={16} aria-hidden="true" /></Link>;
}
export function ImportControl({ identity }: ImportControlProps) {
  const session = useSession();
  if (session.error) return <div className={styles.import}><p role="alert">Your session could not be checked.</p><Button variant="secondary" onClick={() => void session.refresh()}>Retry session</Button></div>;
  if (session.isLoading || session.isTransitioning || session.account === undefined) return <p role="status">Checking your session…</p>;
  if (!session.account) return <div className={styles.import}><p>Log in to bring this title into the shared catalog.</p><LoginToImport identity={identity} /></div>;
  return <AuthenticatedImport key={`${session.account.id}:${previewHref(identity)}`} identity={identity} />;
}
function AuthenticatedImport({ identity }: ImportControlProps) {
  const session = useSession();
  const client = useQueryClient();
  const request = useRef<AbortController | null>(null);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<ImportMediaResult | null>(null);
  const [error, setError] = useState<Error | null>(null);
  useEffect(() => () => { request.current?.abort(); }, []);
  async function submit() {
    if (request.current) return;
    const controller = new AbortController(); request.current = controller;
    setPending(true); setError(null);
    try {
      const outcome = await importMedia({ provider: identity.provider, mediaType: identity.type, externalId: identity.externalId }, controller.signal);
      controller.signal.throwIfAborted();
      await Promise.all([client.invalidateQueries({ queryKey: ["catalog", "list"] }), client.invalidateQueries({ queryKey: ["catalog", "detail", outcome.mediaId.toLowerCase()] })]);
      controller.signal.throwIfAborted(); setResult(outcome);
    } catch (failure) {
      if (!controller.signal.aborted) {
        setError(failure instanceof Error ? failure : new Error("Import failed."));
        if (failure instanceof ApiError && failure.status === 401) void session.refresh().catch(() => {});
      }
    } finally {
      if (!controller.signal.aborted) { setPending(false); request.current = null; }
    }
  }
  if (result) return <div className={styles.import}><p role="status">{result.created ? "Imported to the catalog." : "Already in the catalog."}</p><Link className={styles.link} href={`/catalog/${result.mediaId}`}>View in catalog<IconArrowUpRight size={16} aria-hidden="true" /></Link></div>;
  const expired = error instanceof ApiError && error.status === 401;
  return <div className={styles.import}>
    <p>Add this title to the shared catalog. Your personal library stays separate.</p>
    {error && <p role="alert">{expired ? "Your session expired. Log in before importing." : error instanceof ApiError && (error.status === 429 || error.retryAfter) ? "Too many requests. Please wait before trying again." : "Import unavailable. Please try again."}</p>}
    {expired ? <LoginToImport identity={identity} /> : <Button disabled={pending} onClick={() => void submit()}><IconDownload size={18} aria-hidden="true" />{pending ? "Importing…" : "Import to catalog"}</Button>}
  </div>;
}
