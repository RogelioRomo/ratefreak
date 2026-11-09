import "server-only";

export function validateCatalogQuery(params: URLSearchParams): URLSearchParams {
  const result = new URLSearchParams();
  for (const [key, value] of params) {
    if (!["q", "type", "categoryId", "ownOnly", "page", "pageSize"].includes(key) || params.getAll(key).length !== 1) throw new Error("Invalid catalog query.");
    if (key === "q") {
      if (value.length > 200 || value.includes("\0")) throw new Error("Invalid search.");
      if (value.trim()) result.set(key, value.trim());
    } else if (key === "type") {
      if (!["movie", "tv_show", "album", "video_game", "custom"].includes(value)) throw new Error("Invalid media type.");
      result.set(key, value);
    } else if (key === "categoryId") {
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)) throw new Error("Invalid category."); result.set(key, value.toLowerCase());
    } else if (key === "ownOnly") {
      if (!["true", "false"].includes(value)) throw new Error("Invalid ownership filter."); result.set(key, value);
    } else {
      const max = key === "page" ? 10000 : 100;
      if (!/^\d+$/.test(value) || Number(value) < 1 || Number(value) > max) throw new Error("Invalid pagination.");
      result.set(key, String(Number(value)));
    }
  }
  result.sort();
  // Stable public ordering, independent of incoming key order.
  const ordered = new URLSearchParams();
  for (const key of ["q", "type", "categoryId", "ownOnly", "page", "pageSize"]) if (result.has(key)) ordered.set(key, result.get(key)!);
  return ordered;
}
