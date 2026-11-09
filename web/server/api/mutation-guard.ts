import "server-only";
import type { ApiProxyConfig } from "@/server/interfaces/api-proxy-config";
import { ApiError } from "@/shared/api/api-error";

export function validateMutation(request: Request, config: ApiProxyConfig, multipart = false): void {
  if (!config.webOrigin || !config.proxySecret || !config.clientIpMode) throw new ApiError(503, { title: "API proxy is not configured." });
  if (request.headers.get("Origin") !== config.webOrigin) throw new ApiError(403, { title: "Request origin is not allowed." });
  const contentType = request.headers.get("Content-Type") ?? "";
  if (multipart ? !/^multipart\/form-data;\s*boundary=(?:"[A-Za-z0-9'()+_,./:=?-]{1,70}"|[A-Za-z0-9'()+_,./:=?-]{1,70})$/i.test(contentType) : contentType.split(";", 1)[0].trim().toLowerCase() !== "application/json") {
    throw new ApiError(415, { title: "A JSON request is required." });
  }
  if (!request.headers.get("X-CSRF-TOKEN")) throw new ApiError(400, { title: "A CSRF token is required." });
}
