"use client";
import { useState } from "react";
import { useSession } from "@/features/auth/hooks/use-session";
import { usePersonalWrite } from "@/features/personal/hooks/use-personal-write";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/shared/api/api-error";
import { setFollow } from "../api/follows";
import type { FollowActionsProps } from "../interfaces/follows";
import styles from "./follows.module.css";
export function FollowActions({ userId, handle, available }: FollowActionsProps) {
 const session = useSession(), write = usePersonalWrite(); const [message, setMessage] = useState("");
 if (session.account?.id.toLowerCase() === userId.toLowerCase()) return null;
 async function save(following: boolean) {
  setMessage(""); const result = await write.run(signal => setFollow(userId, following, signal));
  if (result) {
   setMessage(following ? "Follow saved." : "Unfollow saved.");
   void write.client.invalidateQueries({ queryKey: ["connections"] });
  } else setMessage("We couldn’t confirm the change. You can deliberately repeat either action safely.");
 }
 return <section className={styles.actions} hidden={!available} inert={!available} aria-label="Follow actions">
  <p>Follow or unfollow @{handle} with the actions below. Both are safe to repeat; your current relationship isn’t shown.</p>
  <div className={styles.buttons}><Button disabled={!available || write.pending} onClick={() => void save(true)}>Follow</Button><Button variant="secondary" disabled={!available || write.pending} onClick={() => void save(false)}>Unfollow</Button></div>
  {write.error instanceof ApiError && write.error.status === 429 ? <p role="alert">Too many changes. Wait before trying again.</p> : write.error instanceof ApiError && write.error.status === 404 ? <p role="alert">This account is no longer available.</p> : <p role="status">{message}</p>}
 </section>;
}
