"use client";

import { createContext } from "react";
import type { SessionContextValue } from "../interfaces/session-context-value";

export const SessionContext = createContext<SessionContextValue | null>(null);
