import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
export interface AvatarState { avatarUrl: string | null; revision: number; }
export interface AvatarProps { reference?: string | null; label?: string; size?: "small" | "large"; }
export interface AvatarEditorProps { account: AccountProfile; available: boolean; }
