import type { AccountProfile } from "./account-profile";

export interface SessionContextValue {
  account: AccountProfile | null | undefined;
  isLoading: boolean;
  isTransitioning: boolean;
  error: Error | null;
  refresh: () => Promise<void>;
  runExclusive: <T>(operation: () => Promise<T>) => Promise<T>;
  changeIdentity: (operation: () => Promise<AccountProfile | null>) => Promise<void>;
}
