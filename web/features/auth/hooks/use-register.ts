"use client";

import type { RegisterRequest } from "../interfaces/register-request";
import { register } from "../api/accounts";
import { useSensitiveMutation } from "./use-sensitive-mutation";
import { useSession } from "./use-session";

export function useRegister(onCode?: (code: string) => void) {
  const session = useSession();
  return useSensitiveMutation<RegisterRequest>((input) => session.runExclusive(async () => { const result = await register(input); onCode?.(result.recoveryCode); }));
}
