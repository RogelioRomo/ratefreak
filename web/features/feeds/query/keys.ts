import type { QueryClient } from '@tanstack/react-query';
export const feedKeys = {
  root: ['feeds'] as const,
  page: (account: string, tab: string, type: string, cursor: string | null) => ['feeds',account,tab,type,cursor] as const,
};
export async function resetFeeds(client: QueryClient): Promise<void> {
  await client.cancelQueries({queryKey:feedKeys.root});
  await client.resetQueries({queryKey:feedKeys.root});
}
