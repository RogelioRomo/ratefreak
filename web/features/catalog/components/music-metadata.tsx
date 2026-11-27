import type { MusicMetadataProps } from '../interfaces/music-metadata-props';
import styles from './music-metadata.module.css';
export function MusicMetadata({ metadata, compact = false }: MusicMetadataProps) {
  const data = metadata && typeof metadata === 'object' ? metadata as Record<string, unknown> : {};
  const text = (key: string, max: number) => typeof data[key] === 'string' && data[key].length <= max ? data[key] as string : null;
  const artist = text('artistCredit', 2000), version = text('disambiguation', 1000), date = text('firstReleaseDate', 10);
  const length = data.lengthMilliseconds;
  const seconds = typeof length === 'number' && Number.isInteger(length) && length >= 0 && length <= 2147483647 ? Math.floor(length / 1000) : null;
  return <div className={styles.metadata}>
    {artist && <p>{artist}</p>}{version && <p>{version}</p>}
    {!compact && seconds !== null && <p>{Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, '0')}</p>}
    {!compact && date && /^\d{4}(?:-\d{2}(?:-\d{2})?)?$/.test(date) && <p>First released {date}</p>}
  </div>;
}
