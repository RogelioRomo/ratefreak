import 'server-only';
import { isMediaId } from '@/features/catalog/query/filters';
export function reportingRoute(method: string, path: readonly string[]): boolean {
 return method === 'POST' && path.length === 3 && ['reviews', 'comments', 'media', 'categories', 'users'].includes(path[0]) && isMediaId(path[1]) && path[2] === 'reports';
}
