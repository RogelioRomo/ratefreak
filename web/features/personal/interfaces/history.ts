import type { LibraryItem } from "./personal";
export interface HistoryFilters { year?: number; month?: number; page: number; pageSize: number; }
export interface HistoryMonthTotal { month: number; count: number; }
export interface HistoryItem extends LibraryItem { completedAt: string; status: "completed"; }
export interface HistoryPage { year: number; month: number | null; availableYears: number[]; months: HistoryMonthTotal[]; annualTotal: number; totalCount: number; missingDateCount: number; items: HistoryItem[]; page: number; pageSize: number; }
export interface HistoryContentProps { accountId: string; }
export interface HistoryTimelineProps { items: HistoryItem[]; }
export interface LibraryNavigationProps { current: "library" | "history"; }
