import "server-only";

export function validateCatalogQuery(params: URLSearchParams): URLSearchParams {
  const result = new URLSearchParams();
  for (const [key, value] of params) {
    if (!["q", "type", "page", "pageSize"].includes(key) || params.getAll(key).length !== 1) throw new Error("Invalid catalog query.");
    if (key === "q") {
      if (value.length > 200 || value.includes("\0")) throw new Error("Invalid search.");
      if (value.trim()) result.set(key, value.trim());
    } else if (key === "type") {
      if (!["movie", "tv_show", "album", "video_game"].includes(value)) throw new Error("Invalid media type.");
      result.set(key, value);
    } else {
      const max = key === "page" ? 10000 : 100;
      if (!/^\d+$/.test(value) || Number(value) < 1 || Number(value) > max) throw new Error("Invalid pagination.");
      result.set(key, String(Number(value)));
    }
  }
  result.sort();
  // Stable public ordering, independent of incoming key order.
  const ordered = new URLSearchParams();
  for (const key of ["q", "type", "page", "pageSize"]) if (result.has(key)) ordered.set(key, result.get(key)!);
  return ordered;
}
