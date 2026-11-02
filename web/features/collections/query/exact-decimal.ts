import type { DecimalValue } from "../interfaces/exact-json";
const tokens = new WeakSet<object>();
const max = BigInt("79228162514264337593543950335");
function normalized(text: string): string {
  if (text.length > 65536 || !/^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/.test(text)) throw new Error("Enter a valid decimal number.");
  const [mantissa, exponent = "0"] = text.toLowerCase().split("e");
  const negative = mantissa.startsWith("-");
  const unsigned = negative ? mantissa.slice(1) : mantissa;
  const [whole, fraction = ""] = unsigned.split(".");
  let digits = (whole + fraction).replace(/^0+/, "");
  if (!digits) return "0";
  if (exponent.replace(/^[+-]?0*/, "").length > 6) throw new Error("Number is outside the supported decimal range.");
  const significant = digits.replace(/0+$/, "");
  const power = Number(exponent) - fraction.length + digits.length - significant.length;
  digits = significant;
  if (power < -28 || digits.length + Math.max(power,0) > 29) throw new Error("Number is outside the supported decimal range.");
  if (power > 0) digits += "0".repeat(power);
  if (BigInt(digits) > max) throw new Error("Number is outside the supported decimal range.");
  if (power < 0) { const padded = digits.padStart(-power+1,"0"); digits = `${padded.slice(0,power)}.${padded.slice(power)}`; }
  return (negative ? "-" : "") + digits;
}
class ExactDecimal implements DecimalValue {
  readonly text: string;
  constructor(text: string) {this.text = normalized(text); tokens.add(this); Object.freeze(this);}
}
export function decimalValue(text: string): DecimalValue { return new ExactDecimal(text); }
export function isDecimalValue(value: unknown): value is DecimalValue { return typeof value === "object" && value !== null && tokens.has(value); }
export function decimalText(value: DecimalValue): string { if(!isDecimalValue(value)) throw new Error("Invalid decimal token."); return value.text; }
export function compareDecimals(a: string, b: string): -1 | 0 | 1 {
  const left = normalized(a), right = normalized(b);
  const scale = Math.max(left.split(".")[1]?.length ?? 0, right.split(".")[1]?.length ?? 0);
  const coefficient = (value: string) => {const [whole, fraction = ""] = value.split("."); return BigInt(whole + fraction.padEnd(scale,"0"));};
  const x = coefficient(left), y = coefficient(right); return x < y ? -1 : x > y ? 1 : 0;
}
