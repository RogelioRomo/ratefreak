export interface CoverFetchWaiter {
  signal: AbortSignal;
  accept: (release: () => void) => void;
  reject: (reason: unknown) => void;
  abort: () => void;
}
