import { chmodSync, mkdirSync, mkdtempSync, realpathSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, expect, it } from "vitest";
import { readIgdbPolicy } from "./igdb-policy";

let directory: string;
let policyPath: string;
const valid = '{"format":1,"terminated":false,"blockedIdentities":["game:2147483647","game:2"],"blockedPosters":["x.jpg"]}';

beforeEach(() => {
  directory = mkdtempSync(join(realpathSync(tmpdir()), "igdb-policy-"));
  policyPath = join(directory, "policy.json");
});
afterEach(() => rmSync(directory, { force: true, recursive: true }));

it("reads the exact policy and immediately observes replacement policy values", () => {
  writeFileSync(policyPath, valid, { mode: 0o600 });
  expect(readIgdbPolicy(policyPath)).toEqual({
    terminated: false, blockedIdentities: new Set(["game:2147483647", "game:2"]), blockedPosters: new Set(["x.jpg"]),
  });
  writeFileSync(policyPath, valid.replace("false", "true"));
  expect(readIgdbPolicy(policyPath)?.terminated).toBe(true);
});

it.each([
  valid.replace('"terminated":false', '"terminated":true,"terminated":false'),
  valid.replace('"terminated":false', '"\\u0074erminated":true,"terminated":false'),
  valid.replace('"format":1', '"format":1.0'),
  valid.replace('"format":1', '"format":1e0'),
  valid.replace('"format":1', '"format":2'),
  valid.replace('"format":1', '"format":1,"extra":0'),
  valid.replace('"format":1,', ''),
  valid.replace('"game:2"', '"game:01"'),
  valid.replace('"game:2"', '"game:2147483648"'),
  valid.replace('"game:2"', '"game:0"'),
  valid.replace('"game:2"', '"game:1\\n"'),
  valid.replace('"game:2"', '"game:2147483647"'),
  valid.replace('"x.jpg"', '"x.jpg","x.jpg"'),
  valid.replace('"x.jpg"', '"..%2fprivate.jpg"'),
  valid.replace('"x.jpg"', '"x.jpg\\r"'),
  valid.replace('"x.jpg"', `"${"x".repeat(197)}.jpg"`),
  valid.replace('false', 'null'),
  valid.slice(0, -1) + ',}',
])("rejects a malformed or ambiguous policy %#", contents => {
  writeFileSync(policyPath, contents, { mode: 0o600 });
  expect(readIgdbPolicy(policyPath)).toBeNull();
});

it("enforces policy and collection size limits", () => {
  writeFileSync(policyPath, valid + " ".repeat(1024 * 1024), { mode: 0o600 });
  expect(readIgdbPolicy(policyPath)).toBeNull();
  writeFileSync(policyPath, valid.replace('["x.jpg"]', JSON.stringify(Array.from({ length: 10001 }, (_, i) => `${i}.jpg`))));
  expect(readIgdbPolicy(policyPath)).toBeNull();
});

it("rejects unprepared, relative, symlinked, and permissive policy paths", () => {
  expect(readIgdbPolicy(policyPath)).toBeNull();
  expect(readIgdbPolicy(undefined)).toBeNull();
  expect(readIgdbPolicy("policy.json")).toBeNull();
  writeFileSync(policyPath, valid, { mode: 0o644 });
  expect(readIgdbPolicy(policyPath)).toBeNull();
  chmodSync(policyPath, 0o600);
  const link = join(directory, "link.json");
  symlinkSync(policyPath, link);
  expect(readIgdbPolicy(link)).toBeNull();
  const linkDirectory = join(directory, "parent");
  symlinkSync(directory, linkDirectory, "dir");
  expect(readIgdbPolicy(join(linkDirectory, "policy.json"))).toBeNull();
  const writable = join(directory, "writable");
  mkdirSync(writable);
  chmodSync(writable, 0o777);
  const insecure = join(writable, "policy.json");
  writeFileSync(insecure, valid, { mode: 0o600 });
  expect(readIgdbPolicy(insecure)).toBeNull();
});
