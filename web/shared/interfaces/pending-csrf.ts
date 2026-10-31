export interface PendingCsrf {
  controller: AbortController;
  promise: Promise<string>;
}
