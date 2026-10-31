import type { ProxyContext } from "@/server/interfaces/proxy-context";
import { proxyProblem, proxyRequest } from "@/server/api/proxy";

export async function GET(request: Request, context: ProxyContext): Promise<Response> {
  return proxyRequest(request, (await context.params).path);
}

function rejectMethod(): Response {
  return proxyProblem(405, "Method not allowed.");
}

export const POST = rejectMethod;
export const PUT = rejectMethod;
export const PATCH = rejectMethod;
export const DELETE = rejectMethod;
export const HEAD = rejectMethod;
export const OPTIONS = rejectMethod;
