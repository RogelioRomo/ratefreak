"use client";

import { useCallback, useEffect, useRef } from "react";
import type { SessionSignal } from "../interfaces/session-signal";

export function useSessionSync(refresh: (identityChanged: boolean) => void): () => void {
  const channel = useRef<BroadcastChannel | null>(null);
  useEffect(() => {
    const revalidate = () => refresh(false);
    window.addEventListener("focus", revalidate);
    window.addEventListener("pageshow", revalidate);
    const broadcast = typeof BroadcastChannel !== "undefined" ? new BroadcastChannel("ratefreak-session") : null;
    channel.current = broadcast;
    if (broadcast) broadcast.onmessage = (event: MessageEvent<unknown>) => {
      if (event.data && typeof event.data === "object" && "type" in event.data && event.data.type === "identity-changed") refresh(true);
    };
    return () => {
      window.removeEventListener("focus", revalidate);
      window.removeEventListener("pageshow", revalidate);
      broadcast?.close();
      channel.current = null;
    };
  }, [refresh]);
  return useCallback(() => channel.current?.postMessage({ type: "identity-changed" } satisfies SessionSignal), []);
}
