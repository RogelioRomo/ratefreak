"use client";
import { useContext } from "react";
import { SessionContext } from "@/features/auth/session/session-context";
export function useCatalogViewer() {
  const session = useContext(SessionContext);
  return { viewer: session ? session.account?.id ?? (session.account === null ? "anonymous" : "checking") : "anonymous", active: !session || !session.isTransitioning && !session.isLoading && !session.error && session.account !== undefined };
}
