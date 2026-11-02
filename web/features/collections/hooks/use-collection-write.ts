"use client";
import { useLayoutEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
export function useCollectionWrite() {
  const session = useSession(), pathname = usePathname();
  const request = useRef<AbortController | null>(null);
  const live = useRef(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [outcomeUnknown, setUnknown] = useState(false);
  const owner = session.account?.id;
  const blocked = !owner || !!session.error || session.isTransitioning || session.isLoading;
  useLayoutEffect(() => { live.current = true; return () => { live.current = false; request.current?.abort(); }; }, []);
  useLayoutEffect(() => () => { request.current?.abort(); }, [owner, blocked, pathname]);
  async function run<T>(operation: (signal: AbortSignal) => Promise<T>): Promise<{value:T} | null> {
    if (blocked || request.current) return null;
    const controller = new AbortController();
    request.current = controller;
    setPending(true); setError(null); setUnknown(false);
    let abort!: () => void;
    const cancelled = new Promise<never>((_,reject) => { abort = () => reject(new DOMException("Cancelled", "AbortError")); controller.signal.addEventListener("abort", abort, {once:true}); });
    try {
      const value = await Promise.race([operation(controller.signal), cancelled]);
      controller.signal.throwIfAborted();
      return live.current ? {value} : null;
    } catch (failure) {
      if (live.current) {
        if (controller.signal.aborted || !(failure instanceof ApiError) || failure.status >= 500) setUnknown(true);
        if (!controller.signal.aborted) setError(failure instanceof Error ? failure : new Error("Request failed."));
        if (failure instanceof ApiError && failure.status === 401) void session.refresh().catch(() => {});
      }
      return null;
    } finally {
      controller.signal.removeEventListener("abort", abort);
      if (request.current === controller) {request.current = null; if (live.current) setPending(false);}
    }
  }
  return {run, pending, error, outcomeUnknown, clearError: () => {setError(null); setUnknown(false);}};
}
