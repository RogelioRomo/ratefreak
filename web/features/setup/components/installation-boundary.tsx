"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { usePathname, useRouter } from "next/navigation";
import type { ChildrenProps } from "@/shared/interfaces/children-props";
import { createQueryClient } from "@/shared/query/create-query-client";
import { LoadingState } from "@/components/ui/loading-state";
import { Button } from "@/components/ui/button";
import { AuthPanel } from "@/features/auth/components/auth-panel";
import { RecoveryCodeDisplay } from "@/features/auth/components/recovery-code-display";
import { getInstallationStatus } from "../api/installation";
import { SetupForm } from "./setup-form";
import { SetupShell } from "./setup-shell";

export function InstallationBoundary({ children }: ChildrenProps) {
  // Session transitions clear the main query client. Installation state has its own
  // nonpersistent client so logging in/out cannot unmount the application boundary.
  const [client] = useState(createQueryClient);
  const [code, setCode] = useState<string | null>(null);
  const [acknowledged, setAcknowledged] = useState(false);
  const router = useRouter(); const pathname = usePathname();
  const status = useQuery({ queryKey: ["installation"], queryFn: ({ signal }) => getInstallationStatus(signal), retry: false, staleTime: query => query.state.data?.required === false ? Infinity : 60_000,
    refetchOnWindowFocus: query => query.state.data?.required === false ? false : "always",
    refetchOnReconnect: query => query.state.data?.required !== false }, client);
  useEffect(() => { client.mount(); return () => client.unmount(); }, [client]);
  useEffect(() => {
    if (code || !status.data || (status.isError && status.data.required)) return;
    if (status.data.required && !acknowledged && pathname !== "/setup") router.replace("/setup");
    if (!status.data.required && pathname === "/setup") router.replace(acknowledged ? "/login" : "/");
  }, [code, acknowledged, pathname, router, status.data, status.isError]);
  function finish() {
    setAcknowledged(true); setCode(null);
    void status.refetch().then(result => { if (result.data && !result.data.required) router.replace("/login"); });
  }
  if (code) return <SetupShell><AuthPanel title="Your installation is ready." description="Save the administrator’s recovery code before continuing."><RecoveryCodeDisplay code={code} title="Save your recovery code." doneLabel="Continue to login" onDone={finish} /></AuthPanel></SetupShell>;
  if (status.isPending) return <SetupShell><LoadingState label="Checking installation…" /></SetupShell>;
  if (status.data?.required === false) return pathname === "/setup" ? <SetupShell><LoadingState label="Opening RateFreak…" /></SetupShell> : children;
  if (status.isError) return <SetupShell><div role="alert"><p>We couldn’t check installation status. Please try again.</p><Button onClick={() => { void status.refetch(); }}>Retry setup check</Button></div></SetupShell>;
  if (acknowledged) return <SetupShell><p>Setup was completed. Check its status to continue.</p><Button onClick={() => { void status.refetch(); }}>Retry setup check</Button></SetupShell>;
  return <SetupShell><AuthPanel title="Make this installation yours." description="Create the first administrator account to get started.">
    {status.data.configured ? <SetupForm onCreated={setCode} onCheckStatus={() => { void status.refetch(); }} /> : <><p>The site owner needs to configure a deployment secret before setup can continue.</p><Button onClick={() => { void status.refetch(); }}>Retry setup check</Button></>}
  </AuthPanel></SetupShell>;
}
