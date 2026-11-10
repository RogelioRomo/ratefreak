export interface AccountProfile {
  id: string;
  handle: string;
  displayName: string;
  email: string;
  createdAt: string;
  bio?: string | null;
  profileTextHidden?: boolean;
}
