import { fetchOpenLibraryCover } from "@/server/artwork/open-library-cover";
import type { OpenLibraryCoverContext } from "@/server/interfaces/open-library-cover-context";

export async function GET(request: Request, context: OpenLibraryCoverContext): Promise<Response> {
  if (new URL(request.url).search) return new Response(null, { status: 404, headers: { "Cache-Control": "no-store" } });
  return fetchOpenLibraryCover((await context.params).coverId, request.signal);
}
