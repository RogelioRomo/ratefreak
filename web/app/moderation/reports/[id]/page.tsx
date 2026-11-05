import {ReportScreen} from '@/features/moderation/components/report-screen';
import type {StaffPageProps} from '@/features/moderation/interfaces/moderation';
export const metadata={title:'Moderation inspection | RateFreak'};
export default async function Page({params}:StaffPageProps){const {id}=await params;return <ReportScreen id={id.toLowerCase()}/>;}
