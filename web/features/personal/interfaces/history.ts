import type { LibraryItem } from "./personal";
export interface HistoryFilters { year?: number; month?: number; page: number; pageSize: number; }
export interface HistoryMonthTotal { month: number; count: number; }
export interface HistoryPage { year: number; month: number | null; availableYears: number[]; months: HistoryMonthTotal[]; annualTotal: number; totalCount: number; missingDateCount: number; items: LibraryItem[]; page: number; pageSize: number; }
