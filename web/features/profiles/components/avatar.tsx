"use client";
import { useState } from "react";
import { IconUser } from "@tabler/icons-react";
import type { AvatarProps } from "../interfaces/avatar";
import styles from "./avatar.module.css";

export function Avatar({ reference, label, size = "small" }: AvatarProps) {
  const [failed, setFailed] = useState<string | null>(null);
  const src = reference && /^\/api\/v1\/(?:users\/(?:me|[0-9a-f-]{36})|moderation\/(?:reports|profiles)\/[0-9a-f-]{36})\/avatar\/[0-9a-f-]{36}$/i.test(reference) ? reference : null;
  const show = src && failed !== src;
  return <span className={styles.avatar} data-size={size} aria-hidden={label ? undefined : true} role={!show && label ? "img" : undefined} aria-label={!show ? label : undefined}>
    {show
      // eslint-disable-next-line @next/next/no-img-element -- checked no-store account uploads must bypass the public image optimizer.
      ? <img src={src} alt={label ?? ""} loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailed(src)} />
      : <IconUser size={size === "large" ? 40 : 20} stroke={1.5} aria-hidden="true" />}
  </span>;
}
