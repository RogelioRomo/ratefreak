import { apiRequest } from "@/shared/api/request";
import { getCsrfToken } from "@/shared/api/csrf";
import { parseApiError } from "@/shared/api/parse-api-error";
export type DataOperation = "export" | "delete";
export interface AccountDataStatus {
    hasPassword: boolean;
    providers: string[];
    confirmedOperation: DataOperation | null;
    recoveryEventDays: number;
    resolvedModerationDays: number;
    backupDays: number;
}
const path = "users/me/data";
export function getAccountDataStatus(signal?: AbortSignal): Promise<AccountDataStatus> { return apiRequest(path, { signal }); }
export function confirmAccountData(operation: DataOperation, password: string): Promise<void> { return apiRequest(`${path}/confirm`, { method: "POST", body: { operation, password } }); }
export function startAccountDataProvider(operation: DataOperation, provider: string): Promise<{
    authorizationUrl: string;
}> { return apiRequest(`${path}/external/${provider}/start`, { method: "POST", body: { operation } }); }
export function deleteAccount(handle: string): Promise<void> { return apiRequest(`${path}/delete`, { method: "POST", body: { handle } }); }
export async function downloadAccountData(): Promise<void> {
    const token = await getCsrfToken();
    const response = await fetch(`/api/v1/${path}/export`, { method: "POST", credentials: "same-origin", cache: "no-store", headers: { Accept: "application/zip", "Content-Type": "application/json", "X-CSRF-TOKEN": token }, body: "{}" });
    if (!response.ok)
        throw await parseApiError(response);
    if (response.headers.get("Content-Type")?.split(";", 1)[0].trim() !== "application/zip")
        throw new Error("The export response was not an archive.");
    const blob = await response.blob();
    if (blob.size > 256 * 1024 * 1024)
        throw new Error("The export exceeds 256 MiB.");
    const disposition = response.headers.get("Content-Disposition") ?? "";
    const filename = /filename="([^"/\\]+)"/i.exec(disposition)?.[1] ?? "ratefreak-data.zip";
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    try {
        link.href = url;
        link.download = filename;
        document.body.append(link);
        link.click();
    }
    finally {
        link.remove();
        URL.revokeObjectURL(url);
    }
}
