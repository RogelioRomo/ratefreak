import "server-only";
import { closeSync, lstatSync, readSync } from "node:fs";
import { dirname, isAbsolute, resolve } from "node:path";
import { isTmdbPosterFilename } from "@/shared/artwork/tmdb-poster-paths";
import { secureDirectory, secureRead } from "./tmdb-paths";

const maximumPolicyBytes = 1024 * 1024;
const keys = new Set(["format", "terminated", "blockedIdentities", "blockedPosters"]);
const decoder = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true });

export type TmdbPolicy = {
  terminated: boolean;
  blockedIdentities: ReadonlySet<string>;
  blockedPosters: ReadonlySet<string>;
};

function uniqueValues(values: unknown, isValid: (value: unknown) => value is string): Set<string> | null {
  if (!Array.isArray(values) || values.length > 10_000 || !values.every(isValid)) return null;
  const unique = new Set(values);
  return unique.size === values.length ? unique : null;
}

function isIdentity(value: unknown): value is string {
  if (typeof value !== "string") return false;
  const id = /^(?:movie|tv):([1-9][0-9]{0,9})(?![\s\S])/.exec(value)?.[1];
  return id !== undefined && Number(id) <= 2_147_483_647;
}

function parsePolicy(text: string): TmdbPolicy | null {
  const document: unknown = JSON.parse(text);
  if (!document || typeof document !== "object" || Array.isArray(document)) return null;
  const object = document as Record<string, unknown>;
  if (object.format !== 1 || typeof object.terminated !== "boolean") return null;
  const blockedIdentities = uniqueValues(object.blockedIdentities, isIdentity);
  const blockedPosters = uniqueValues(object.blockedPosters, isTmdbPosterFilename);
  if (!blockedIdentities || !blockedPosters) return null;

  // JSON.parse has validated syntax and shape but silently collapses duplicate
  // keys. Scan whole string tokens so escaped names are compared after decoding.
  const seen = new Set<string>();
  for (const token of text.matchAll(/"(?:[^"\\]|\\[\s\S])*"(\s*:)?/g)) {
    if (!token[1]) continue;
    const key = JSON.parse(token[0].slice(0, -token[1].length)) as string;
    if (!keys.has(key) || seen.has(key)) return null;
    if (key === "format" && !/^\s*1\s*[,}]/.test(text.slice(token.index + token[0].length))) return null;
    seen.add(key);
  }
  return seen.size === keys.size ? { terminated: object.terminated, blockedIdentities, blockedPosters } : null;
}

export function readTmdbPolicy(path: string | undefined): TmdbPolicy | null {
  if (!path) return null;
  try {
    if (!isAbsolute(path) || resolve(path) !== path) return null;
    secureDirectory(dirname(path), "policy");
    const { descriptor, status } = secureRead(path, maximumPolicyBytes);
    try {
      const bytes = Buffer.alloc(status.size);
      if (readSync(descriptor, bytes, 0, bytes.length, 0) !== bytes.length) return null;
      const policy = parsePolicy(decoder.decode(bytes));
      const current = lstatSync(path);
      if (current.dev !== status.dev || current.ino !== status.ino || current.mtimeMs !== status.mtimeMs
        || current.ctimeMs !== status.ctimeMs || current.size !== status.size) return null;
      return policy;
    } finally {
      closeSync(descriptor);
    }
  } catch {
    return null;
  }
}
