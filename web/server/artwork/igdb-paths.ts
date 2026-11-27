import "server-only";
import { constants, closeSync, fstatSync, lstatSync, openSync, type Stats } from "node:fs";
import { isAbsolute, join, parse, resolve, sep } from "node:path";

const uid = process.geteuid?.() ?? process.getuid?.();

export function secureDirectory(path: string | undefined, kind: "cache" | "policy"): string {
  if (!path || !isAbsolute(path) || resolve(path) !== path) throw new Error("Invalid IGDB storage path.");
  const root = parse(path).root;
  let component = root;
  let status = lstatSync(component);
  for (const name of path.slice(root.length).split(sep).filter(Boolean)) {
    component = join(component, name);
    status = lstatSync(component);
    // A root-owned sticky directory such as /tmp protects its child paths.
    const stickyRoot = status.uid === 0 && (status.mode & 0o1000) !== 0;
    if (!status.isDirectory() || (uid !== undefined && (
      (status.uid !== 0 && status.uid !== uid) || ((status.mode & 0o022) !== 0 && !stickyRoot)
    ))) throw new Error("Unsafe IGDB storage directory.");
  }
  if (path === root || (uid !== undefined && (status.uid !== uid
    || (kind === "cache" ? (status.mode & 0o7777) !== 0o700 : (status.mode & 0o077) !== 0)))) {
    throw new Error("IGDB storage directory is not private.");
  }
  return path;
}

export function secureRead(path: string, maxBytes: number) {
  const valid = (status: Stats) => status.isFile() && status.nlink === 1 && status.size > 0 && status.size <= maxBytes
    && (uid === undefined || (status.uid === uid && (status.mode & 0o7777) === 0o600));
  const before = lstatSync(path);
  if (!valid(before)) throw new Error("Invalid IGDB storage file.");
  const descriptor = openSync(path, constants.O_RDONLY | constants.O_NOFOLLOW | constants.O_NONBLOCK);
  try {
    const status = fstatSync(descriptor);
    if (!valid(status) || before.dev !== status.dev || before.ino !== status.ino) {
      throw new Error("Invalid IGDB storage file.");
    }
    return { descriptor, status };
  } catch (error) {
    closeSync(descriptor);
    throw error;
  }
}
