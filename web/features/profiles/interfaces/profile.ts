import type { AccountProfile } from "@/features/auth/interfaces/account-profile";
export interface PublicProfile { id: string; handle: string; displayName: string; bio: string | null; createdAt: string }
export interface ProfileProps { handle: string }

export interface ProfileEditorProps { account: AccountProfile; available: boolean }

export interface ProfilePageProps { params: Promise<ProfileProps> }

export interface ProfileInput { displayName: string; bio: string | null }
