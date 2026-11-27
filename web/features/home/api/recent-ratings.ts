import { apiRequest } from '@/shared/api/request';
import type { RecentRatingsPage } from '../interfaces/recent-ratings';
export function getRecentRatings(signal?: AbortSignal): Promise<RecentRatingsPage> {
  return apiRequest<RecentRatingsPage>('users/me/ratings/recent', { signal });
}
