import { isTmdbPosterFilename, tmdbPosterRoute } from "@/shared/artwork/tmdb-poster-paths";
import { tmdbPosterProblem } from "@/server/artwork/tmdb-poster-cache";
import { getTmdbPosterCache } from "@/server/artwork/tmdb-posters";

export async function GET(request: Request, { params }: { params: Promise<{ filename: string }> }): Promise<Response> {
  const { filename } = await params;
  const url = new URL(request.url);
  if (!isTmdbPosterFilename(filename) || url.href !== `${url.origin}${tmdbPosterRoute}${filename}`) {
    return tmdbPosterProblem(404);
  }
  return getTmdbPosterCache().get(filename, request.signal);
}
