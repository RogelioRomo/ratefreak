import type { FeedFilters } from '../interfaces/feed';
const types = ['movie','tv_show','album','video_game','book','custom'];
export function readFeedFilters(params: URLSearchParams): FeedFilters | null {
  for (const key of params.keys()) if (!['tab','type','cursor'].includes(key) || params.getAll(key).length !== 1) return null;
  const tab = params.get('tab') ?? 'following'; const type = params.get('type'); const cursor = params.get('cursor');
  if (!['following','community'].includes(tab) || type !== null && !types.includes(type) || cursor !== null && (!/^[A-Za-z0-9_-]+$/.test(cursor) || cursor.length > 1024)) return null;
  return {tab: tab as FeedFilters['tab'], type: (type ?? '') as FeedFilters['type'], cursor};
}
export function feedParams(filters: FeedFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.tab !== 'following') params.set('tab',filters.tab);
  if (filters.type) params.set('type',filters.type);
  if (filters.cursor) params.set('cursor',filters.cursor);
  return params;
}
export function feedHref(filters: FeedFilters): string { const params=feedParams(filters).toString();return params ? '/?'+params : '/'; }
