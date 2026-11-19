import 'server-only';
import { readFeedFilters, feedParams } from '@/features/feeds/query/filters';
export function feedRoute(method: string, path: readonly string[]): boolean { return method === 'GET' && path.length === 1 && path[0] === 'feed'; }
export function feedQuery(params: URLSearchParams): URLSearchParams {
  const parsed = readFeedFilters(params); if (!parsed) throw new Error('Invalid feed filters.');
  return feedParams(parsed);
}
