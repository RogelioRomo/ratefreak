import "server-only";
import { parseHistoryFilters, historyParams } from "@/features/personal/query/history";
export function historyRoute(method: string, path: readonly string[]): boolean { return method === "GET" && path.length === 4 && path.join("/") === "users/me/library/history"; }
export function historyQuery(params: URLSearchParams): URLSearchParams {
  const filters = parseHistoryFilters(params, true);
  if (!filters) throw new Error("Invalid history filters.");
  return historyParams(filters, true);
}
