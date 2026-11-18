import "server-only";
import type { CoverFetchWaiter } from "../interfaces/cover-fetch-waiter";

/** Bounds source-image working memory and queued work across optimizer misses. */
export class CoverFetchGate {
  private active = 0;
  private readonly waiting: CoverFetchWaiter[] = [];

  acquire(signal: AbortSignal): Promise<() => void> | null {
    signal.throwIfAborted();
    if (this.active < 8) {
      this.active++;
      return Promise.resolve(this.lease());
    }
    if (this.waiting.length >= 64) return null;
    return new Promise((accept, reject) => {
      const waiter: CoverFetchWaiter = { signal, accept, reject, abort: () => {} };
      waiter.abort = () => {
        const index = this.waiting.indexOf(waiter);
        if (index >= 0) this.waiting.splice(index, 1);
        reject(signal.reason);
      };
      this.waiting.push(waiter);
      signal.addEventListener("abort", waiter.abort, { once: true });
    });
  }

  private lease(): () => void {
    let released = false;
    return () => {
      if (released) return;
      released = true;
      const waiter = this.waiting.shift();
      if (waiter) {
        waiter.signal.removeEventListener("abort", waiter.abort);
        waiter.accept(this.lease());
      } else this.active--;
    };
  }
}
