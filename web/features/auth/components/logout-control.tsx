"use client";

import { Button } from "@/components/ui/button";
import { useLogout } from "../hooks/use-logout";
import { useSession } from "../hooks/use-session";
import { FormError } from "./form-error";
import styles from "./auth.module.css";

export function LogoutControl() {
  const mutation = useLogout();
  const session = useSession();
  if (!session.account && !mutation.isPending) return null;
  return (
    <div className={styles.logout}>
      <Button variant="secondary" disabled={mutation.isPending || session.isTransitioning} onClick={() => {
        void mutation.mutateAsync().catch(() => {});
      }}>{mutation.isPending ? "Logging out…" : "Log out"}</Button>
      <FormError error={mutation.error} />
    </div>
  );
}
