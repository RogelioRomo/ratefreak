"use client";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { ApiError } from "@/shared/api/api-error";
import type { CommentWriteResult } from "../interfaces/comments";
export function useCommentWrite() {
 const session = useSession();
 const controller = useRef<AbortController | null>(null);
 const identity = session.error || session.isLoading || session.isTransitioning ? undefined : session.account?.id;
 const latestIdentity = useRef(identity);
 const [pending, setPending] = useState(false);
 useLayoutEffect(() => { latestIdentity.current = identity; controller.current?.abort(); }, [identity]);
 useEffect(() => () => { controller.current?.abort(); }, []);
 async function run<T>(operation: (signal: AbortSignal) => Promise<T>): Promise<CommentWriteResult<T>> {
  if (!identity || controller.current) return { kind: "uncertain" };
  const request = new AbortController(); controller.current = request; setPending(true);
  try {
   const value = await operation(request.signal);
   if (request.signal.aborted || latestIdentity.current !== identity) return { kind: "uncertain" };
   return { kind: "saved", value };
  } catch (error) {
   if (!request.signal.aborted && latestIdentity.current === identity && error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408) {
    if (error.status === 401) void session.refresh().catch(() => {});
    return { kind: "rejected", error };
   }
   return { kind: "uncertain" };
  } finally { if (controller.current === request) { controller.current = null; setPending(false); } }
 }
 return { run, pending };
}
