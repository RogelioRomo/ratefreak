"use client";
import { useContext } from "react";
import { SessionContext } from "@/features/auth/session/session-context";
import { ReportControl } from "@/features/reporting/components/report-control";
import type { CatalogReportControlsProps } from "../interfaces/custom-catalog";
export function CatalogReportControls({ item }: CatalogReportControlsProps) {
 const session = useContext(SessionContext);
 if (!session || item.visibility !== "public" || !item.creatorUserId) return null;
 return <><ReportControl target={{ type: "item", id: item.id, authorId: item.creatorUserId }} />{item.category?.scope === "public" && !item.category.isSeeded && <ReportControl target={{ type: "category", id: item.category.id, authorId: item.category.creatorUserId ?? "" }} />}</>;
}
