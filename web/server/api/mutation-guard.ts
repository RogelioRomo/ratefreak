import "server-only";
import type { ApiProxyConfig } from "@/server/interfaces/api-proxy-config";
import { ApiError } from "@/shared/api/api-error";

export function validateMutation(request: Request, config: ApiProxyConfig): void {
  if (!config.webOrigin || !config.proxySecret || !config.clientIpMode) throw new ApiError(503, { title: "API proxy is not configured." });
  if (request.headers.get("Origin") !== config.webOrigin) throw new ApiError(403, { title: "Request origin is not allowed." });
  if (request.headers.get("Content-Type")?.split(";", 1)[0].trim().toLowerCase() !== "application/json") {
    throw new ApiError(415, { title: "A JSON request is required." });
  }
  if (!request.headers.get("X-CSRF-TOKEN")) throw new ApiError(400, { title: "A CSRF token is required." });
}
