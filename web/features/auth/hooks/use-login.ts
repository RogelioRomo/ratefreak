"use client";

import type { LoginRequest } from "../interfaces/login-request";
import { login } from "../api/accounts";
import { useSensitiveMutation } from "./use-sensitive-mutation";
import { useSession } from "./use-session";

export function useLogin() {
  const session = useSession();
  return useSensitiveMutation<LoginRequest>((input) => session.changeIdentity(() => login(input)));
}
