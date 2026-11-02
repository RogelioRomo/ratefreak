import { expect, it } from "vitest";
import { decimalValue, decimalText, compareDecimals } from "./exact-decimal";
it.each(['0','-0','0.000','0e999999999999999999'])('normalizes zero %s',text=>expect(decimalText(decimalValue(text))).toBe('0'));
it.each([['1.2300','1.23'],['1e-28','0.0000000000000000000000000001'],['79228162514264337593543950335','79228162514264337593543950335'],['0.1234567890123456789012345678','0.1234567890123456789012345678']])('preserves exact %s', (text,want)=>expect(decimalText(decimalValue(text))).toBe(want));
it.each(['NaN','Infinity','01','+1','1.','1e-29','79228162514264337593543950336','1e1000000','0.12345678901234567890123456789'])('rejects unrepresentable %s',text=>expect(()=>decimalValue(text)).toThrow());
it('compares inclusively without floating point rounding',()=>{expect(compareDecimals('0.1234567890123456789012345678','0.1234567890123456789012345679')).toBe(-1);expect(compareDecimals('-1.2','-1.20')).toBe(0);expect(compareDecimals('79228162514264337593543950335','9007199254740993')).toBe(1);});
