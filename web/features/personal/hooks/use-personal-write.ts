"use client";
import { useEffect, useRef, useState } from "react";
import { useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
export function usePersonalWrite() {
  const client = useQueryClient();
  const session = useSession();
  const request = useRef<AbortController | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const blocked = !session.account || !!session.error || session.isTransitioning || session.isLoading;
  useEffect(() => () => { request.current?.abort(); }, []);
  useEffect(() => { if (blocked) request.current?.abort(); }, [blocked]);
  async function run<T>(operation: (signal: AbortSignal) => Promise<T>, invalidate: readonly QueryKey[] = []): Promise<{ value: T } | null> {
    if (blocked || request.current) return null;
    const controller = new AbortController(); request.current = controller;
    setPending(true); setError(null);
    try {
      const value = await operation(controller.signal); controller.signal.throwIfAborted();
      await Promise.all(invalidate.map(queryKey => client.invalidateQueries({ queryKey }))); controller.signal.throwIfAborted();
      return { value };
    } catch (failure) {
      if (!controller.signal.aborted) {
        setError(failure instanceof Error ? failure : new Error("Request failed."));
        if (failure instanceof ApiError && failure.status === 401) void session.refresh().catch(() => {});
      }
      return null;
    } finally { if (request.current === controller) { request.current = null; setPending(false); } }
  }
  return { run, pending, error, client, clearError: () => setError(null) };
}
