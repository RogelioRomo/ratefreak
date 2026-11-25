import "server-only";
import { spawn } from "node:child_process";
import { closeSync, constants, fstatSync, lstatSync, openSync } from "node:fs";

const acquireLock = `
import fcntl
import time

deadline = time.monotonic() + 1
while True:
    try:
        fcntl.flock(3, fcntl.LOCK_EX | fcntl.LOCK_NB)
        break
    except BlockingIOError:
        if time.monotonic() >= deadline:
            raise SystemExit(1)
        time.sleep(0.025)
`;

function checkLockFile(path: string, descriptor: number) {
  const status = fstatSync(descriptor);
  const current = lstatSync(path);
  const uid = process.geteuid?.() ?? process.getuid?.();
  if (!status.isFile() || status.nlink !== 1 || status.dev !== current.dev || status.ino !== current.ino
    || (uid !== undefined && (status.uid !== uid || (status.mode & 0o7777) !== 0o600))) {
    throw new Error("Invalid TMDB poster cache lock.");
  }
}

function acquire(descriptor: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    // fd 3 and the parent descriptor share one open file description. The
    // helper exits after flock; the parent owns the lock until closing its fd.
    const child = spawn("python3", ["-I", "-S", "-c", acquireLock], {
      stdio: ["ignore", "ignore", "ignore", descriptor],
      signal,
      timeout: 2000,
      killSignal: "SIGKILL",
    });
    let failure: Error | undefined;
    child.once("error", error => { failure = error; });
    child.once("close", code => {
      if (failure) reject(failure);
      else if (code === 0) resolve();
      else reject(new Error("Could not acquire the TMDB poster cache lock."));
    });
  });
}

export async function withTmdbPosterLock<T>(path: string, signal: AbortSignal, operation: () => T): Promise<T> {
  signal.throwIfAborted();
  // Keep this inode stable while any workers are running. A process exit
  // closes its descriptor and releases the lock without filesystem cleanup.
  const descriptor = openSync(path, constants.O_RDWR | constants.O_CREAT | constants.O_NOFOLLOW | constants.O_NONBLOCK, 0o600);
  try {
    checkLockFile(path, descriptor);
    await acquire(descriptor, signal);
    signal.throwIfAborted();
    checkLockFile(path, descriptor);
    return await operation();
  } finally {
    closeSync(descriptor);
  }
}
