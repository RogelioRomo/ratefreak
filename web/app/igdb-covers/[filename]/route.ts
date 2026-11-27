import { isIgdbCoverFilename, igdbCoverRoute } from "@/shared/artwork/igdb-cover-paths";
import { igdbCoverProblem } from "@/server/artwork/igdb-cover-cache";
import { getIgdbCoverCache } from "@/server/artwork/igdb-covers";

export async function GET(request: Request, { params }: { params: Promise<{ filename: string }> }): Promise<Response> {
  const { filename } = await params;
  const url = new URL(request.url);
  if (!isIgdbCoverFilename(filename) || url.href !== `${url.origin}${igdbCoverRoute}${filename}`) {
    return igdbCoverProblem(404);
  }
  return getIgdbCoverCache().get(filename, request.signal);
}
