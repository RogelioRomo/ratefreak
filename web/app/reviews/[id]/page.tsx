import { Suspense } from "react";
import { LoadingState } from "@/components/ui/loading-state";
import { DiscussionScreen } from "@/features/comments/components/discussion-screen";
import type { DiscussionPageProps } from "@/features/comments/interfaces/comments";
export const metadata = { title: "Review discussion | RateFreak" };
async function Content({ params }: DiscussionPageProps) { const { id } = await params; return <DiscussionScreen reviewId={id.toLowerCase()} />; }
export default function DiscussionPage(props: DiscussionPageProps) { return <Suspense fallback={<LoadingState label="Loading discussion…" />}><Content {...props} /></Suspense>; }
