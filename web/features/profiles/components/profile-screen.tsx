"use client";
import { ReportControl } from "@/features/reporting/components/report-control";
import { Connections } from "@/features/follows/components/connections";
import styles from "./profiles.module.css";
import { useProfile } from "../hooks/use-profile";
import { LoadingState } from "@/components/ui/loading-state";
import { ErrorState } from "@/components/ui/error-state";
import type { ProfileProps } from "../interfaces/profile";
export function ProfileScreen({ handle }: ProfileProps) {
  const valid = /^[a-z0-9_]{3,30}$/i.test(handle);
  const profile = useProfile(handle);
  if (!valid) return <p>Invalid profile address.</p>;
  if (profile.isPending) return <LoadingState label="Loading profile…" />;
  if (profile.isError) return <ErrorState title="Profile unavailable" description="This profile may be missing, or temporarily unavailable." onRetry={() => void profile.refetch()} />;
  return <article className={styles.profile}><h1>{profile.data.displayName}</h1><div className={styles.identity}><p>@{profile.data.handle}</p><p>Member since <time dateTime={profile.data.createdAt}>{profile.data.createdAt.slice(0, 10)}</time></p></div>{profile.data.bio && <p className={styles.bio}>{profile.data.bio}</p>}{profile.data.profileTextHidden ? <p>Profile text is currently unavailable.</p> : <ReportControl target={{type:"profile",id:profile.data.id,authorId:profile.data.id}} available={!profile.isFetching}/>}<Connections userId={profile.data.id} handle={profile.data.handle} /></article>;
}
