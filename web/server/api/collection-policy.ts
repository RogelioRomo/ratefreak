import "server-only";
import { isMediaId } from "@/features/catalog/query/filters";
export function collectionRoute(method: string, path: readonly string[]): boolean {
  if(path[0] !== "collections") return false;
  if(path.length === 1) return ["GET","POST"].includes(method);
  if(!isMediaId(path[1])) return false;
  if(path.length === 2) return ["GET","PATCH"].includes(method);
  if(path[2] === "items") return path.length === 3 ? ["GET","POST"].includes(method) : path.length === 4 && isMediaId(path[3]) && ["GET","PATCH","DELETE"].includes(method);
  if(path[2] === "fields") return path.length === 3 ? method === "POST" : isMediaId(path[3] ?? "") && (path.length === 4 ? ["PATCH","DELETE"].includes(method) : path.length === 5 && path[4] === "deletion-impact" && method === "GET");
  return false;
}
export function validateCollectionQuery(params: URLSearchParams): URLSearchParams {
  const result = new URLSearchParams();
  for (const key of params.keys()) if (!["ownOnly", "page", "pageSize"].includes(key) || params.getAll(key).length !== 1) throw new Error("Invalid collection filters.");
  for (const key of ["ownOnly", "page", "pageSize"]) {
    const value = params.get(key);
    if (value === null) continue;
    if (key === "ownOnly" ? !["true", "false"].includes(value) : !/^\d+$/.test(value) || +value < 1 || +value > (key === "page" ? 10000 : 100)) throw new Error("Invalid collection filters.");
    result.set(key, key === "ownOnly" ? value : String(+value));
  }
  return result;
}

export function collectionQuery(method: string, path: readonly string[], params: URLSearchParams): URLSearchParams | null {
  if(!collectionRoute(method,path)) return null;
  if(method === "GET" && path.length === 1) return validateCollectionQuery(params);
  const items = method === "GET" && path.length === 3 && path[2] === "items";
  const remove = method === "DELETE" && path.length === 4;
  if(!items && !remove) return null;
  const allowed = items ? ["page","pageSize"] : path[2] === "fields" ? ["expectedRevision","confirm"] : ["expectedRevision"];
  for(const key of params.keys())if(!allowed.includes(key)||params.getAll(key).length!==1)throw new Error("Invalid collection query.");
  const result = new URLSearchParams();
  for(const key of allowed){const value=params.get(key);if(value===null){if(remove)throw new Error("Missing revision/confirmation.");continue;}if(key === "confirm"){if(value !== "true")throw new Error("Confirmation required.");}else if(!/^\d+$/.test(value)||!Number.isSafeInteger(+value)||+value<1||+value>(key === "page" ? 10000 : key === "pageSize" ? 100 : Number.MAX_SAFE_INTEGER))throw new Error("Invalid bounds.");result.set(key,key === "confirm"?value:String(+value));}
  return result;
}
