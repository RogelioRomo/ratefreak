"use client";
import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/features/auth/hooks/use-session";
import { sessionQueryKey } from "@/features/auth/session/query-keys";
import { usePersonalWrite } from "@/features/personal/hooks/use-personal-write";
import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
import type { AvatarEditorProps } from "../interfaces/avatar";
import { ApiError } from "@/shared/api/api-error";
import { Button } from "@/components/ui/button";
import { removeAvatar, uploadAvatar } from "../api/avatars";
import { Avatar } from "./avatar";
import styles from "./avatar.module.css";

export function AvatarEditor() {
  const session = useSession();
  const client = useQueryClient();
  const cached = client.getQueryData<AccountProfile | null>(sessionQueryKey);
  return cached ? <Editor key={cached.id} account={cached} available={session.account?.id === cached.id && !session.error && !session.isTransitioning && !session.isLoading} /> : null;
}
function Editor({ account, available }: AvatarEditorProps) {
  const write = usePersonalWrite();
  const session = useSession();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [validation, setValidation] = useState("");
  const [message, setMessage] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const [checking, setChecking] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  const status = write.error instanceof ApiError ? write.error.status : null;
  const error = validation || (write.error ? status === 409 ? "Your avatar changed elsewhere. Check the current avatar before trying again." : status === 400 || status === 413 || status === 415 ? "Choose a static JPEG, PNG or WebP up to 5 MiB with supported image dimensions." : status === 429 ? "Too many changes. Wait before trying again." : status === 503 ? "Avatar storage is unavailable. Please try again later." : "We couldn’t confirm that change. Check the current avatar before trying again." : "");
  const contested = uncertain && !(write.error instanceof ApiError && [400, 413, 415, 429].includes(write.error.status));
  const locked = !available || write.pending || contested || checking;
  function reset() { setFile(null); setPreview(null); setValidation(""); if (input.current) input.current.value = ""; }
  async function save(remove: boolean) {
    if (locked || (!remove && !file)) return;
    setMessage("");
    const result = await write.run(signal => remove ? removeAvatar(account.avatarRevision ?? 0, account.id, signal) : uploadAvatar(file!, account.avatarRevision ?? 0, account.id, signal));
    if (result) {
      await write.client.cancelQueries({ queryKey: sessionQueryKey });
      if (write.client.getQueryData<AccountProfile | null>(sessionQueryKey)?.id !== account.id) return;
      write.client.setQueryData<AccountProfile | null>(sessionQueryKey, current => current?.id === account.id && (current.avatarRevision ?? 0) <= result.value.revision ? { ...current, avatarUrl: result.value.avatarUrl, avatarRevision: result.value.revision } : current);
      setUncertain(false); reset(); void session.refresh(); setMessage(remove ? "Avatar removed." : "Avatar saved.");
      for (const key of ["public-profile", "connections", "comments", "review-discussion", "community", "collections", "notifications"]) void write.client.invalidateQueries({ queryKey: [key] });
    } else setUncertain(true);
  }
  return <section hidden={!available} inert={!available} className={styles.editor} aria-labelledby="avatar-heading">
    <h2 id="avatar-heading">Profile image</h2>
    <div>{file && preview ? <span className={styles.avatar} data-size="large">
      {/* eslint-disable-next-line @next/next/no-img-element -- local temporary preview, released when the selected file changes. */}
      <img className={styles.preview} src={preview} alt="Selected avatar preview" onError={() => { setValidation("This image could not be previewed. Choose another image."); setFile(null); setPreview(null); if (input.current) input.current.value = ""; }} />
    </span> : <Avatar reference={account.avatarUrl} size="large" label="Current avatar" />}</div>
    <p className={styles.hint}>Your avatar is public. JPEG, PNG or WebP, up to 5 MiB. Images are cropped to a square.</p>
    {account.profileTextHidden && <p>Your profile is hidden by moderation. Your avatar, name and bio stay hidden until staff restore them.</p>}
    {error && <p role="alert" id="avatar-error" className={styles.error}>{error}</p>}
    <fieldset disabled={locked}>
      <label htmlFor="avatar-file">Choose avatar image</label>
      <input ref={input} id="avatar-file" type="file" accept="image/jpeg,image/png,image/webp" aria-invalid={validation ? true : undefined} aria-describedby={error ? "avatar-error" : undefined} onChange={event => {
        if (locked) return;
        setUncertain(false);
        const selected = event.currentTarget.files?.[0]; setMessage(""); write.clearError(); setValidation("");
        if (!selected) { setFile(null); setPreview(null); return; }
        if (selected.size > 5 * 1024 * 1024 || !["image/jpeg", "image/png", "image/webp"].includes(selected.type)) {
          setValidation("Choose a JPEG, PNG or WebP image up to 5 MiB."); setFile(null); setPreview(null); event.currentTarget.value = ""; return;
        }
        setFile(selected); setPreview(URL.createObjectURL(selected));
      }} />
      <div className={styles.actions}>
        <Button type="button" disabled={locked || !file} onClick={() => void save(false)}>{write.pending ? "Saving…" : "Save avatar"}</Button>
        <Button type="button" variant="secondary" disabled={locked || !account.avatarUrl} onClick={() => void save(true)}>Remove avatar</Button>
      </div>
    </fieldset>
    {contested && <Button type="button" variant="secondary" disabled={!available || checking} onClick={async () => {
      setChecking(true);
      try {
        await session.refresh();
        const state = write.client.getQueryState(sessionQueryKey);
        if (state?.status === "success" && write.client.getQueryData<AccountProfile | null>(sessionQueryKey)?.id === account.id) { setUncertain(false); write.clearError(); reset(); }
      } finally { setChecking(false); }
    }}>{checking ? "Checking…" : "Check current avatar"}</Button>}
    <p role="status">{message}</p>
  </section>;
}
