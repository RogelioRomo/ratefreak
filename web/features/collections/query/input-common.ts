export function validText(value: string, max: number, multiline = false, blank = false): boolean {
 if (value.length > max || !blank && !value.trim()) return false;
 for (let i = 0; i < value.length; i++) { const c = value.charCodeAt(i); if (c >= 0xd800 && c <= 0xdbff) { const next = value.charCodeAt(++i); if (!(next >= 0xdc00 && next <= 0xdfff)) return false; } else if (c >= 0xdc00 && c <= 0xdfff) return false; }
 return !new RegExp(multiline ? "[\\x00-\\x08\\x0b\\x0c\\x0e-\\x1f\\x7f-\\x9f]" : "[\\x00-\\x1f\\x7f-\\x9f]").test(value);
}
export function positionValue(value: string): number | null { const n = Number(value); return /^\d+$/.test(value) && Number.isInteger(n) && n >= 0 && n <= 2147483647 ? n : null; }
