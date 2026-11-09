"use client";
import { Select } from "@/components/ui/select";
import { useId } from "react";
import type { MetadataFieldsProps } from "../interfaces/component-props";
import styles from "./collections.module.css";
export function MetadataFields({value,onChange,errors,disabled}: MetadataFieldsProps) {
  const prefix = useId();
  return <>
    <div className={styles.field}><label htmlFor={`${prefix}-name`}>Name</label><input id={`${prefix}-name`} name="name" value={value.name} disabled={disabled} onChange={event=>onChange({...value,name:event.target.value})} required aria-invalid={!!errors.name} aria-describedby={errors.name ? `${prefix}-name-error` : undefined}/>{errors.name && <p role="alert" id={`${prefix}-name-error`}>{errors.name.join(" ")}</p>}</div>
    <div className={styles.field}><label htmlFor={`${prefix}-description`}>Description</label><textarea id={`${prefix}-description`} name="description" rows={6} value={value.description} disabled={disabled} onChange={event=>onChange({...value,description:event.target.value})} aria-invalid={!!errors.description} aria-describedby={`${prefix}-description-hint${errors.description ? ` ${prefix}-description-error` : ""}`}/><p id={`${prefix}-description-hint`} className={styles.hint}>Optional. Up to 2000 characters.</p>{errors.description && <p role="alert" id={`${prefix}-description-error`}>{errors.description.join(" ")}</p>}</div>
    <div className={styles.field}><label htmlFor={`${prefix}-visibility`}>Visibility</label><Select id={`${prefix}-visibility`} name="visibility" value={value.visibility} disabled={disabled} onChange={event=>onChange({...value,visibility:event.target.value === "public" ? "public" : "private"})} aria-invalid={!!errors.visibility} aria-describedby={`${prefix}-visibility-hint`}><option value="private">Private</option><option value="public">Public</option></Select><p className={styles.hint} id={`${prefix}-visibility-hint`}>Private is visible only to you. Public shares this list. Each item’s visibility still applies.</p>{errors.visibility && <p role="alert">{errors.visibility.join(" ")}</p>}</div>
  </>;
}
