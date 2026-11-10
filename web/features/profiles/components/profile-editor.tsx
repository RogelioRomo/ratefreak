"use client";
import type { ProfileEditorProps } from "../interfaces/profile";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import { usePersonalWrite } from "@/features/personal/hooks/use-personal-write";
import { fieldErrors } from "@/features/auth/components/form-error";
import { ApiError } from "@/shared/api/api-error";
import { saveProfile } from "../api/profiles";
import { Button } from "@/components/ui/button";
import { TextField } from "@/components/ui/text-field";
import profileStyles from "./profiles.module.css";
import styles from "@/features/auth/components/auth.module.css";
export function ProfileEditor() {
  const session = useSession();
  const client = useQueryClient();
  const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  return cached ? <Editor key={cached.id} account={cached} available={!!session.account && !session.error && !session.isTransitioning} /> : null;
}
function Editor({ account, available }: ProfileEditorProps) {
  const write = usePersonalWrite();
  const [message, setMessage] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const blocked = uncertain && !(write.error instanceof ApiError && [400, 429].includes(write.error.status));
  const errors = fieldErrors(write.error, "bio");
  return <form hidden={!available} inert={!available} aria-label="Edit public profile" className={`${styles.form} ${profileStyles.editor}`} onSubmit={async event => {
    event.preventDefault(); if (write.pending || blocked || !available) return;
    const data = new FormData(event.currentTarget); setMessage("");
    const result = await write.run(signal => saveProfile({ displayName: String(data.get("displayName")).trim(), bio: String(data.get("bio")).trim() || null }, signal));
    if (result) {
      setUncertain(false);
      write.client.setQueryData(sessionQueryKey, result.value);
      void write.client.invalidateQueries({ queryKey: ["public-profile"] });
      void write.client.invalidateQueries({ queryKey: ["connections"] });
      void write.client.invalidateQueries({ queryKey: ["comments"] });
      void write.client.invalidateQueries({ queryKey: ["review-discussion"] });
      void write.client.invalidateQueries({ queryKey: ["community"] });
      void write.client.invalidateQueries({ queryKey: ["collections"] });
      setMessage("Profile saved.");
    } else setUncertain(true);
  }}><h2>Edit public profile</h2><p>{account.profileTextHidden ? "Your profile text is hidden by moderation. You can save corrections, but staff must restore it before your name and bio become public again." : "Your display name and bio are visible to everyone."}</p>
    {write.error && <p role="alert">{write.error instanceof ApiError && write.error.status === 400 ? "Please check your profile details." : write.error instanceof ApiError && write.error.status === 429 ? "Too many changes. Please wait before trying again." : "We couldn’t confirm the save. Reload your account to check before saving again."}</p>}
    {blocked && <p role="alert">Check your account by reloading before saving again.</p>}
    <fieldset disabled={!available || write.pending || blocked}>
      <TextField name="displayName" label="Display name" defaultValue={account.displayName} required maxLength={80} errors={fieldErrors(write.error, "displayName")} />
      <label htmlFor="profile-bio">Bio</label><textarea className={profileStyles.bioInput} id="profile-bio" name="bio" defaultValue={account.bio ?? ""} maxLength={500} rows={5} aria-invalid={errors?.length ? true : undefined} aria-describedby={errors?.length ? "profile-bio-error" : undefined} />
      {errors?.length && <p id="profile-bio-error">{errors.join(" ")}</p>}
      <Button type="submit">{write.pending ? "Saving…" : "Save profile"}</Button>
    </fieldset><p role="status">{message}</p></form>;
}
