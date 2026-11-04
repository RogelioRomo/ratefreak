export function commentPage(params: URLSearchParams): number | null {
 for (const key of params.keys()) if (key !== "page" || params.getAll(key).length !== 1) return null;
 const page = params.get("page") ?? "1";
 return /^\d+$/.test(page) && +page >= 1 && +page <= 10000 ? +page : null;
}
export const discussionHref = (id: string, page = 1) => `/reviews/${id}${page > 1 ? `?page=${page}` : ""}`;
