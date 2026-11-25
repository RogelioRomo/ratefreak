import { chmodSync, mkdirSync, mkdtempSync, realpathSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, expect, it } from "vitest";
import { readTmdbPolicy } from "./tmdb-policy";

let directory: string;
let policyPath: string;
const valid = '{"format":1,"terminated":false,"blockedIdentities":["movie:2147483647","tv:1"],"blockedPosters":["x.webp"]}';

beforeEach(() => {
  directory = mkdtempSync(join(realpathSync(tmpdir()), "tmdb-policy-"));
  policyPath = join(directory, "policy.json");
});
afterEach(() => rmSync(directory, { force: true, recursive: true }));

it("reads the exact policy and immediately observes replacement policy values", () => {
  writeFileSync(policyPath, valid, { mode: 0o600 });
  expect(readTmdbPolicy(policyPath)).toEqual({
    terminated: false, blockedIdentities: new Set(["movie:2147483647", "tv:1"]), blockedPosters: new Set(["x.webp"]),
  });
  writeFileSync(policyPath, valid.replace("false", "true"));
  expect(readTmdbPolicy(policyPath)?.terminated).toBe(true);
});

it.each([
  valid.replace('"terminated":false', '"terminated":true,"terminated":false'),
  valid.replace('"terminated":false', '"\\u0074erminated":true,"terminated":false'),
  valid.replace('"format":1', '"format":1.0'),
  valid.replace('"format":1', '"format":1e0'),
  valid.replace('"format":1', '"format":2'),
  valid.replace('"format":1', '"format":1,"extra":0'),
  valid.replace('"format":1,', ''),
  valid.replace('"tv:1"', '"movie:01"'),
  valid.replace('"tv:1"', '"tv:2147483648"'),
  valid.replace('"tv:1"', '"tv:0"'),
  valid.replace('"tv:1"', '"tv:1\\n"'),
  valid.replace('"tv:1"', '"movie:2147483647"'),
  valid.replace('"x.webp"', '"x.webp","x.webp"'),
  valid.replace('"x.webp"', '"..%2fprivate.jpg"'),
  valid.replace('"x.webp"', '"x.webp\\r"'),
  valid.replace('"x.webp"', `"${"x".repeat(197)}.jpg"`),
  valid.replace('false', 'null'),
  valid.slice(0, -1) + ',}',
])("rejects a malformed or ambiguous policy %#", contents => {
  writeFileSync(policyPath, contents, { mode: 0o600 });
  expect(readTmdbPolicy(policyPath)).toBeNull();
});

it("enforces policy and collection size limits", () => {
  writeFileSync(policyPath, valid + " ".repeat(1024 * 1024), { mode: 0o600 });
  expect(readTmdbPolicy(policyPath)).toBeNull();
  writeFileSync(policyPath, valid.replace('["x.webp"]', JSON.stringify(Array.from({ length: 10001 }, (_, i) => `${i}.jpg`))));
  expect(readTmdbPolicy(policyPath)).toBeNull();
});

it("rejects unprepared, relative, symlinked, and permissive policy paths", () => {
  expect(readTmdbPolicy(policyPath)).toBeNull();
  expect(readTmdbPolicy(undefined)).toBeNull();
  expect(readTmdbPolicy("policy.json")).toBeNull();
  writeFileSync(policyPath, valid, { mode: 0o644 });
  expect(readTmdbPolicy(policyPath)).toBeNull();
  chmodSync(policyPath, 0o600);
  const link = join(directory, "link.json");
  symlinkSync(policyPath, link);
  expect(readTmdbPolicy(link)).toBeNull();
  const linkDirectory = join(directory, "parent");
  symlinkSync(directory, linkDirectory, "dir");
  expect(readTmdbPolicy(join(linkDirectory, "policy.json"))).toBeNull();
  const writable = join(directory, "writable");
  mkdirSync(writable);
  chmodSync(writable, 0o777);
  const insecure = join(writable, "policy.json");
  writeFileSync(insecure, valid, { mode: 0o600 });
  expect(readTmdbPolicy(insecure)).toBeNull();
});
