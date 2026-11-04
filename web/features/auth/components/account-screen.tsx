"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useSession } from "../hooks/use-session";
import { AuthPanel } from "./auth-panel";
import { AccountDetails } from "./account-details";
import { LogoutControl } from "./logout-control";
import { LoadingState } from "@/components/ui/loading-state";

import { ProfileEditor } from "@/features/profiles/components/profile-editor";

export function AccountScreen() {
  const session = useSession();
  const router = useRouter();
  const [entryChecked, setEntryChecked] = useState(false);
  const { refresh } = session;
  useEffect(() => {
    let active = true;
    void refresh().finally(() => { if (active) setEntryChecked(true); });
    return () => { active = false; };
  }, [refresh]);
  useEffect(() => {
    if (entryChecked && session.account === null && !session.isLoading && !session.isTransitioning && !session.error) router.replace("/login");
  }, [entryChecked, session.account, session.isLoading, session.isTransitioning, session.error, router]);
  return <AuthPanel title="Your account." description="A space that’s yours.">
    {entryChecked ? <AccountDetails /> : <LoadingState label="Checking your session…" />}
    {entryChecked && <ProfileEditor />}
    <div hidden={!entryChecked}><LogoutControl /></div>
  </AuthPanel>;
}
