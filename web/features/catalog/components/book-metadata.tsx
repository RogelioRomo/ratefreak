import { providerIdentity } from "@/features/providers/query/identity";
import type { BookMetadataProps } from "../interfaces/book-metadata-props";
import styles from "./book-metadata.module.css";

export function BookMetadata({ metadata, externalId }: BookMetadataProps) {
  const data = metadata && typeof metadata === "object" ? metadata as Record<string, unknown> : {};
  const authors = Array.isArray(data.authors) ? data.authors.slice(0, 100).flatMap(author => {
    if (!author || typeof author !== "object" || typeof author.name !== "string" || !author.name.trim() || author.name.length > 300) return [];
    return [author.name];
  }) : [];
  const year = typeof data.firstPublicationYear === "number" && Number.isInteger(data.firstPublicationYear) && data.firstPublicationYear > 0 && data.firstPublicationYear <= 9999 ? data.firstPublicationYear : null;
  const source = externalId ? providerIdentity("openlibrary", "book", externalId) : null;
  return <div className={styles.metadata}>
    {authors.length > 0 && <p>By {authors.join(", ")}</p>}
    <p>{year ? <>First published <time dateTime={String(year).padStart(4, "0")}>{year}</time></> : "Publication year unavailable"}</p>
    {source && <p><a href={`https://openlibrary.org/works/${source.externalId}`} target="_blank" rel="noopener noreferrer">View on Open Library</a></p>}
  </div>;
}
