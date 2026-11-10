import {TargetScreen} from '@/features/moderation/components/target-screen';
import type {StaffPageProps} from '@/features/moderation/interfaces/moderation';
export default async function ProfileModerationPage({params}:StaffPageProps){const {id}=await params;return <TargetScreen id={id} segment="profiles"/>;}
