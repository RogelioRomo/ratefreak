"use client";

import { logout } from "../api/accounts";
import { useMutation } from "@tanstack/react-query";
import { useSession } from "./use-session";

export function useLogout() {
  const session = useSession();
  return useMutation({
    mutationFn: () => session.changeIdentity(async () => { await logout(); return null; }),
    retry: false,
    gcTime: 0,
  });
}
