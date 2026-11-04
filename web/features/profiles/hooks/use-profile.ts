import { useQuery } from "@tanstack/react-query";
import { getProfile } from "../api/profiles";
export function useProfile(handle: string) {
  return useQuery({ queryKey: ["public-profile", handle.toLowerCase()], enabled: /^[a-z0-9_]{3,30}$/i.test(handle), queryFn: ({ signal }) => getProfile(handle, signal), retry: false });
}
