import type { QueryClient } from "@tanstack/react-query";

export interface QueryProbeProps {
  onClient: (client: QueryClient) => void;
}
