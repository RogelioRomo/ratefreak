import type { ChildrenProps } from "@/shared/interfaces/children-props";
export interface LoginContinuationProps { returnTo?: string | null; }
export interface AuthEntryProps extends ChildrenProps, LoginContinuationProps {}
export interface LoginPageProps { searchParams: Promise<Record<string, string | string[] | undefined>>; }
