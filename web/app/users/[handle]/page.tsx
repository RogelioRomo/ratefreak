import { Suspense } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { ProfileScreen } from "@/features/profiles/components/profile-screen";
import type { ProfilePageProps } from "@/features/profiles/interfaces/profile";
export const metadata = { title: "Public profile | RateFreak" };
async function Content({ params }: ProfilePageProps) { const { handle } = await params; return <ProfileScreen handle={handle} />; }
export default function ProfilePage(props: ProfilePageProps) { return <Suspense fallback={<LoadingState label="Loading profile…" />}><Content {...props} /></Suspense>; }
