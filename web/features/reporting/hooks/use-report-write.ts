'use client';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useSession } from '@/features/auth/hooks/use-session';
import { ApiError } from '@/shared/api/api-error';
import type { ReportWriteResult } from '../interfaces/reporting';
export function useReportWrite(available = true) {
 const session = useSession();
 const identity = available && !session.isLoading && !session.isTransitioning && !session.error ? session.account?.id : undefined;
 const latest = useRef(identity), controller = useRef<AbortController | null>(null);
 const operation = useRef<((signal: AbortSignal) => Promise<unknown>) | null>(null);
 const [pending, setPending] = useState(false);
 const mutation = useMutation({ mutationFn: async () => { if (!operation.current || !controller.current) throw new Error('Report unavailable.'); await operation.current(controller.current.signal); }, retry: false, gcTime: 0 });
 useLayoutEffect(() => { latest.current = identity; controller.current?.abort(); }, [identity]);
 useEffect(() => () => { controller.current?.abort(); operation.current = null; }, []);
 async function run(execute: (signal: AbortSignal) => Promise<unknown>): Promise<ReportWriteResult> {
  if (!identity || controller.current) return { kind: 'uncertain' };
  const request = new AbortController(); controller.current = request; operation.current = execute; setPending(true);
  try {
   await mutation.mutateAsync();
   return request.signal.aborted || latest.current !== identity ? {kind:'uncertain'} : {kind:'saved'};
  } catch (error) {
   if (!request.signal.aborted && latest.current === identity && error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408) {
    if (error.status === 401) void session.refresh().catch(() => {});
    return {kind:'rejected',error};
   }
   return {kind:'uncertain'};
  } finally {
   if (controller.current === request) { controller.current = null; operation.current = null; setPending(false); mutation.reset(); }
  }
 }
 return {run,pending};
}
