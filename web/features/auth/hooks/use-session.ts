"use client";

import { useContext } from "react";
import { SessionContext } from "../session/session-context";

export function useSession() {
  const context = useContext(SessionContext);
  if (!context) throw new Error("useSession requires SessionProvider.");
  return context;
}
