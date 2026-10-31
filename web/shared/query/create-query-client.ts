import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-error";

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60_000,
        gcTime: 300_000,
        refetchOnWindowFocus: true,
        retry: (failureCount, error) => failureCount < 1 && (
          error instanceof TypeError || (error instanceof ApiError && error.status >= 500)
        ),
      },
      mutations: { retry: false },
    },
  });
}
