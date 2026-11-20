import Link from "next/link";
import { MediaArtwork } from "@/features/catalog/components/media-artwork";
import { mediaLabels } from "@/features/catalog/query/filters";
import type { HistoryItem, HistoryTimelineProps } from "../interfaces/history";
import { completionLabel, monthNames } from "../query/history";
import styles from "./history.module.css";

export function HistoryTimeline({ items }: HistoryTimelineProps) {
  const groups = new Map<string, HistoryItem[]>();
  for (const item of items) { const month = item.completedAt.slice(0, 7); const group = groups.get(month) ?? []; group.push(item); groups.set(month, group); }
  return <section aria-label="Completion timeline" className={styles.timeline}>{[...groups].map(([key, entries]) => {
    const [year, month] = key.split("-").map(Number);
    return <section key={key} className={styles.monthGroup}><h3>{monthNames[month - 1]} {year}</h3><ul className={styles.entries}>{entries.map(item => <li key={item.media.id}>
      <div><MediaArtwork reference={item.media.imageReference} title={item.media.title} type={item.media.type} sizes="6rem" /></div>
      <div className={styles.entryDetails}><Link href={`/catalog/${item.media.id}`}>{item.media.title}</Link><p>{mediaLabels[item.media.type]}</p><p>Completed <time dateTime={item.completedAt}>{completionLabel(item.completedAt)}</time></p><p>{item.ownRating == null ? "Not rated" : `Your rating: ${item.ownRating.toFixed(1)} / 5`}</p></div>
    </li>)}</ul></section>;
  })}</section>;
}
