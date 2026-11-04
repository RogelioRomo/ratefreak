import { useQuery } from "@tanstack/react-query";
import { getConnections } from "../api/follows";
import type { ConnectionFilters } from "../interfaces/follows";
export function useConnections(id: string, filters: ConnectionFilters | null) {
 return useQuery({ queryKey: ["connections", id.toLowerCase(), filters], queryFn: ({ signal }) => getConnections(id, filters!, signal), enabled: !!filters, retry: false });
}
