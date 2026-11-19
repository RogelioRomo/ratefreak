import { apiRequest } from '@/shared/api/request';
import type { FeedFilters, FeedPage } from '../interfaces/feed';
import { feedParams } from '../query/filters';
export function getFeed(filters: FeedFilters, signal?: AbortSignal): Promise<FeedPage> {
  const params=feedParams(filters).toString();return apiRequest<FeedPage>('feed'+(params?'?'+params:''),{signal});
}
