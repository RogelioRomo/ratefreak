import {TargetScreen} from '@/features/moderation/components/target-screen';
import type {StaffPageProps} from '@/features/moderation/interfaces/moderation';
export const metadata={title:'Moderation inspection | RateFreak'};
export default async function Page({params}:StaffPageProps){const {id}=await params;return <TargetScreen id={id.toLowerCase()} segment="reviews"/>;}
