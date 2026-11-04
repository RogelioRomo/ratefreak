export type ConnectionDirection = "followers" | "following";
export interface ConnectionFilters { direction: ConnectionDirection; page: number }
export interface FollowUser { id: string; handle: string; displayName: string; followedAt: string }
export interface FollowPage { items: FollowUser[]; totalCount: number; page: number; pageSize: number }
export interface ConnectionsProps { userId: string; handle: string }
export interface FollowActionsProps extends ConnectionsProps { available: boolean }
