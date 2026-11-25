import "server-only";
import {
  closeSync, constants, fsyncSync, lstatSync, openSync, readdirSync, readSync, renameSync, unlinkSync, writeFileSync,
} from "node:fs";
import { join } from "node:path";
import { isTmdbPosterFilename } from "@/shared/artwork/tmdb-poster-paths";
import { secureDirectory, secureRead } from "./tmdb-paths";
import { withTmdbPosterLock } from "./tmdb-poster-lock";
import { hasPosterSignature, posterBytesLimit, posterSignatureBytes, posterTypes, type TmdbPosterPicture } from "./tmdb-poster-source";

const recordMagic = Buffer.from("RFPOST1\0");
const headerLength = recordMagic.length + 8 + 1 + 4;
const capacity = { entries: 2048, bytes: 1024 ** 3 };
const lockName = ".tmdb-lock";
const temporaryName = ".tmdb-write";

type PosterAge = { fetchedAt: number; refreshAt: number; expiresAt: number };
export type StoredPoster = TmdbPosterPicture & PosterAge;
export type StoredPosterSchedule = PosterAge & { filename: string };
type InspectedPoster = PosterAge & { size: number };
type ManagedPoster = InspectedPoster & { filename: string };

function addUtcMonths(time: number, months: number): number {
  const date = new Date(time);
  const day = date.getUTCDate();
  date.setUTCDate(1);
  date.setUTCMonth(date.getUTCMonth() + months + 1);
  date.setUTCDate(0);
  date.setUTCDate(Math.min(day, date.getUTCDate()));
  return date.getTime();
}

export function posterAge(fetchedAt: number): PosterAge {
  return { fetchedAt, refreshAt: addUtcMonths(fetchedAt, 5), expiresAt: addUtcMonths(fetchedAt, 6) };
}

function fileError(error: unknown, code: string): boolean {
  return (error as NodeJS.ErrnoException | undefined)?.code === code;
}

function unlinkIfPresent(path: string) {
  try { unlinkSync(path); } catch (error) { if (!fileError(error, "ENOENT")) throw error; }
}

function readRecord(path: string, now: number, withBytes: true): StoredPoster | null;
function readRecord(path: string, now: number, withBytes: false): InspectedPoster | null;
function readRecord(path: string, now: number, withBytes: boolean): InspectedPoster | StoredPoster | null {
  try {
    const { descriptor, status } = secureRead(path, headerLength + posterBytesLimit);
    try {
      const prefix = Buffer.alloc(Math.min(status.size, headerLength + posterSignatureBytes));
      if (readSync(descriptor, prefix, 0, prefix.length, 0) !== prefix.length || prefix.length <= headerLength
        || !prefix.subarray(0, recordMagic.length).equals(recordMagic)) return null;
      const fetchedAt = Number(prefix.readBigInt64BE(recordMagic.length));
      const type = posterTypes[prefix[recordMagic.length + 8]];
      const size = prefix.readUInt32BE(recordMagic.length + 9);
      if (!type || size !== status.size - headerLength || !Number.isSafeInteger(fetchedAt) || fetchedAt < 0
        || fetchedAt > now || !hasPosterSignature(type, prefix.subarray(headerLength))) return null;
      const age = posterAge(fetchedAt);
      if (!Number.isFinite(age.expiresAt) || age.expiresAt <= now) return null;
      if (!withBytes) return { ...age, size: status.size };
      const bytes = Buffer.alloc(size);
      if (readSync(descriptor, bytes, 0, bytes.length, headerLength) !== bytes.length) return null;
      return { ...age, type, bytes };
    } finally {
      closeSync(descriptor);
    }
  } catch {
    return null;
  }
}

export class TmdbPosterStore {
  constructor(private readonly directory: string | undefined, private readonly now: () => number, private readonly limits = capacity) {}

  read(filename: string, now: number): { poster: StoredPoster | null; found: boolean } {
    const path = join(secureDirectory(this.directory, "cache"), filename);
    const poster = readRecord(path, now, true);
    if (poster) return { poster, found: true };
    try {
      lstatSync(path);
      return { poster: null, found: true };
    } catch (error) {
      if (!fileError(error, "ENOENT")) throw error;
      return { poster: null, found: false };
    }
  }

  async removeIf(filename: string, now: number, remove: (current: PosterAge | null) => boolean, signal: AbortSignal) {
    await this.mutate(signal, directory => {
      const path = join(directory, filename);
      if (remove(readRecord(path, now, false))) unlinkIfPresent(path);
    });
  }

  async reconcile(isBlocked: (filename: string) => boolean, signal: AbortSignal): Promise<StoredPosterSchedule[]> {
    return this.mutate(signal, directory => {
      const records = this.scan(directory, this.now(), isBlocked);
      return this.trim(directory, records);
    });
  }

  async write(filename: string, fetchedAt: number, picture: TmdbPosterPicture, mayPublish: () => boolean, signal: AbortSignal): Promise<boolean> {
    return this.mutate(signal, directory => {
      if (!mayPublish()) return false;
      const now = this.now();
      if (fetchedAt > now || posterAge(fetchedAt).expiresAt <= now) return false;
      const type = posterTypes.indexOf(picture.type);
      const size = headerLength + picture.bytes.length;
      if (type < 0 || size > this.limits.bytes || picture.bytes.length > posterBytesLimit
        || !hasPosterSignature(picture.type, picture.bytes)) throw new Error("Invalid TMDB poster cache write.");
      const destination = join(directory, filename);
      const records = this.scan(directory, now);
      const current = records.find(record => record.filename === filename);
      if (current && current.fetchedAt >= fetchedAt) return true;
      this.trim(directory, records, { filename, size });

      const temporary = join(directory, temporaryName);
      const header = Buffer.alloc(headerLength);
      recordMagic.copy(header);
      header.writeBigInt64BE(BigInt(fetchedAt), recordMagic.length);
      header.writeUInt8(type, recordMagic.length + 8);
      header.writeUInt32BE(picture.bytes.length, recordMagic.length + 9);
      const descriptor = openSync(temporary, constants.O_WRONLY | constants.O_CREAT | constants.O_EXCL | constants.O_NOFOLLOW, 0o600);
      try {
        try {
          writeFileSync(descriptor, header);
          writeFileSync(descriptor, picture.bytes);
          fsyncSync(descriptor);
        } finally {
          closeSync(descriptor);
        }
        signal.throwIfAborted();
        if (!mayPublish()) return false;
        renameSync(temporary, destination);
        return true;
      } finally {
        unlinkIfPresent(temporary);
      }
    });
  }

  private scan(directory: string, now: number, isBlocked: (filename: string) => boolean = () => false): ManagedPoster[] {
    const records: ManagedPoster[] = [];
    for (const file of readdirSync(directory, { withFileTypes: true })) {
      // Only delete leaf files, including dangling symlinks. Directories here
      // were created outside the cache and must be handled offline.
      if (!isTmdbPosterFilename(file.name) || file.isDirectory()) continue;
      const path = join(directory, file.name);
      const record = isBlocked(file.name) ? null : readRecord(path, now, false);
      if (record) records.push({ ...record, filename: file.name });
      else unlinkIfPresent(path);
    }
    return records;
  }

  private trim(directory: string, records: ManagedPoster[], incoming?: { filename: string; size: number }): ManagedPoster[] {
    records.sort((a, b) => a.fetchedAt - b.fetchedAt || a.filename.localeCompare(b.filename));
    // Include the complete temporary file in the byte quota even when it will
    // replace an existing entry. The existing file stays intact when space allows.
    let bytes = records.reduce((total, record) => total + record.size, incoming?.size ?? 0);
    const isReplacement = incoming && records.some(record => record.filename === incoming.filename);
    let entries = records.length + (incoming && !isReplacement ? 1 : 0);
    let evicted = 0;
    for (const record of records) {
      if (bytes <= this.limits.bytes && entries <= this.limits.entries) break;
      unlinkIfPresent(join(directory, record.filename));
      bytes -= record.size;
      if (record.filename !== incoming?.filename) entries--;
      evicted++;
    }
    if (bytes > this.limits.bytes || entries > this.limits.entries) throw new Error("TMDB poster cache quota exceeded.");
    return records.slice(evicted);
  }

  private async mutate<T>(signal: AbortSignal, operation: (directory: string) => T): Promise<T> {
    const directory = secureDirectory(this.directory, "cache");
    return withTmdbPosterLock(join(directory, lockName), signal, () => {
      // With the exclusive lock held there are no live temporary writers.
      unlinkIfPresent(join(directory, temporaryName));
      return operation(directory);
    });
  }
}
