"use client";

import type { RegisterRequest } from "../interfaces/register-request";
import { register } from "../api/accounts";
import { useSensitiveMutation } from "./use-sensitive-mutation";
import { useSession } from "./use-session";

export function useRegister() {
  const session = useSession();
  return useSensitiveMutation<RegisterRequest>((input) => session.runExclusive(async () => { await register(input); }));
}
