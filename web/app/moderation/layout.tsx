import {Suspense} from 'react';
import {StaffBoundary} from '@/features/moderation/components/staff-boundary';
import {LoadingState} from '@/components/ui/loading-state';
import type {StaffBoundaryProps} from '@/features/moderation/interfaces/moderation';
export default function ModerationLayout({children}:StaffBoundaryProps){return <Suspense fallback={<LoadingState label="Loading moderation…"/>}><StaffBoundary>{children}</StaffBoundary></Suspense>;}
