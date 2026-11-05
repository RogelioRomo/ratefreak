"use client";
import Link from "next/link";
import { ReportControl } from "@/features/reporting/components/report-control";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { ReviewContextProps } from "../interfaces/comments";
import styles from "./comments.module.css";
export function ReviewContext({ review, available = true }: ReviewContextProps) {
 const [revealed, setRevealed] = useState(false);
 return <section className={styles.context} aria-label="Original review"><h2>Review by <Link href={`/users/${review.author.handle}`}>{review.author.displayName}</Link></h2><p className={styles.meta}>@{review.author.handle} · <time dateTime={review.createdAt}>{review.createdAt.slice(0, 10)}</time></p>
 {review.isSpoiler && <Button variant="secondary" aria-expanded={revealed} onClick={() => setRevealed(!revealed)}>{revealed ? "Hide review spoilers" : "Show review spoilers"}</Button>}
 {(!review.isSpoiler || revealed) && <p className={styles.body}>{review.body}</p>}
 <ReportControl target={{type:"review",id:review.id,authorId:review.author.id}} available={available}/><div className={styles.actions}><Link href={`/catalog/${review.mediaId}`}>Back to title</Link></div></section>;
}
